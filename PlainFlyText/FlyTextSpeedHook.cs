using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PlainFlyText;

// Hooks two native FlyText addon virtual functions - both resolved via
// FFXIVClientStructs' own maintained AddonFlyText.StaticVirtualTablePointer, not
// hand-rolled signatures, so this rides on FFXIVClientStructs' upkeep across game
// patches rather than our own guess.
//
//  - Update(float delta): multiplies the delta passed to the addon's own native
//    update logic, for the speed slider. Confirmed working.
//  - Draw(): for the size slider (experimental), walks the addon's node tree
//    (descending into Component nodes' own AtkUldManager.NodeList - see
//    ApplyScale) and, for actual AtkTextNode leaves, scales the text-specific
//    FontSize byte field rather than the generic AtkResNode ScaleX/ScaleY.
//
//    Two earlier attempts wrote ScaleX/ScaleY instead (first from Update, then
//    from Draw to rule out a timing race) - both write-confirmed correctly
//    (read back as the target value immediately after writing) but produced no
//    visible size change either way. That points away from a timing bug and
//    toward FontSize being what actually drives glyph rasterization size for
//    this render path, with generic node Scale doing something else (bounds/
//    hit-testing) that doesn't affect the drawn glyph bitmap.
//
//    FontSize scaling needs a captured baseline per node (see baselineFontSize)
//    rather than repeatedly multiplying the live value - Draw fires every frame
//    for the same still-alive node, and multiplying an already-scaled value
//    again next frame would compound exponentially. Baselines are captured the
//    first time a node is seen and dropped once a node is no longer visited
//    (it was destroyed/recycled), since AtkResNode pointers get reused for
//    unrelated later entries.
//
// Neither tweak touches position, font *file*, or the label-blanking behavior in
// Plugin.cs.
internal sealed unsafe class FlyTextSpeedHook : IDisposable
{
    private const int MaxRecursionDepth = 8;
    private const int MaxNodesPerPass = 4000;
    private const double DiagnosticLogIntervalSeconds = 2.0;
    private const byte MinFontSize = 4;
    private const byte MaxFontSize = 100;

    private readonly Configuration config;
    private readonly IGameGui gameGui;
    private readonly Dictionary<nint, byte> baselineFontSize = new();
    private readonly HashSet<nint> seenThisPass = new();

    private Hook<UpdateDelegate>? updateHook;
    private Hook<DrawDelegate>? drawHook;
    private IPluginLog? log;
    private DateTime lastDiagnosticLog = DateTime.MinValue;

    public FlyTextSpeedHook(Configuration config, IGameGui gameGui)
    {
        this.config = config;
        this.gameGui = gameGui;
    }

    public bool IsAvailable { get; private set; }

    private delegate void UpdateDelegate(AddonFlyText* thisPtr, float delta);

    private delegate void DrawDelegate(AddonFlyText* thisPtr);

    public void Initialize(IGameInteropProvider gameInteropProvider, IPluginLog pluginLog)
    {
        log = pluginLog;

        try
        {
            var vtable = AddonFlyText.StaticVirtualTablePointer;

            updateHook = gameInteropProvider.HookFromAddress<UpdateDelegate>((nint)vtable->Update, UpdateDetour);
            updateHook.Enable();

            drawHook = gameInteropProvider.HookFromAddress<DrawDelegate>((nint)vtable->Draw, DrawDetour);
            drawHook.Enable();

            IsAvailable = true;
            log.Information("PlainFlyText: flytext hooks installed.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to install the flytext hooks; the speed/size sliders will have no effect.");
            IsAvailable = false;
            updateHook = null;
            drawHook = null;
        }
    }

    public void Dispose()
    {
        // Restore any tracked nodes to their captured baseline font size before
        // unhooking, so we don't leave flytext stuck at a scaled size if the
        // plugin unloads/disables while enabled.
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon != null && addon->RootNode != null)
        {
            var stats = default(TraversalStats);
            ApplyScale(addon->RootNode, 1.0f, restoreOnly: true, 0, ref stats);
        }

        updateHook?.Disable();
        updateHook?.Dispose();
        drawHook?.Disable();
        drawHook?.Dispose();
    }

    private void UpdateDetour(AddonFlyText* thisPtr, float delta)
    {
        updateHook!.Original(thisPtr, delta * config.SpeedMultiplier);
    }

    private void DrawDetour(AddonFlyText* thisPtr)
    {
        var sizeEnabled = config.SizeScalingEnabled;
        var targetScale = sizeEnabled ? config.SizeMultiplier : 1.0f;
        var stats = default(TraversalStats);

        seenThisPass.Clear();

        if (thisPtr->RootNode != null)
        {
            ApplyScale(thisPtr->RootNode, targetScale, restoreOnly: !sizeEnabled, 0, ref stats);
        }

        // Drop baselines for nodes we didn't see this pass - they were destroyed
        // or their pointer got recycled for something else. Left-behind entries
        // here would apply a stale, wrong baseline if that memory becomes a new
        // text node later.
        if (baselineFontSize.Count > 0)
        {
            var stale = new List<nint>();
            foreach (var ptr in baselineFontSize.Keys)
            {
                if (!seenThisPass.Contains(ptr))
                {
                    stale.Add(ptr);
                }
            }

            foreach (var ptr in stale)
            {
                baselineFontSize.Remove(ptr);
            }
        }

        if (sizeEnabled)
        {
            var now = DateTime.UtcNow;
            if ((now - lastDiagnosticLog).TotalSeconds >= DiagnosticLogIntervalSeconds)
            {
                lastDiagnosticLog = now;
                log?.Information(
                    "PlainFlyText: [Draw] visited {Visited} node(s) ({Components} component(s) entered), max depth " +
                    "{Depth}, {TextLeaves} text leaf/leaves font-scaled to {Target}x (tracked baselines: {Tracked}). " +
                    "Sample: type={LeafType} baseline={Baseline} newSize={NewSize}.",
                    stats.TotalVisited,
                    stats.ComponentsEntered,
                    stats.MaxDepth,
                    stats.TextLeavesScaled,
                    targetScale,
                    baselineFontSize.Count,
                    stats.SampleLeafType,
                    stats.SampleBaseline,
                    stats.SampleNewSize);
            }
        }

        drawHook!.Original(thisPtr);
    }

    private struct TraversalStats
    {
        public int TotalVisited;
        public int TextLeavesScaled;
        public int ComponentsEntered;
        public int MaxDepth;
        public NodeType SampleLeafType;
        public byte SampleBaseline;
        public byte SampleNewSize;
    }

    private void ApplyScale(AtkResNode* node, float scale, bool restoreOnly, int depth, ref TraversalStats stats)
    {
        if (node == null || depth > MaxRecursionDepth || stats.TotalVisited > MaxNodesPerPass)
        {
            return;
        }

        stats.TotalVisited++;
        if (depth > stats.MaxDepth)
        {
            stats.MaxDepth = depth;
        }

        // Component nodes (Type >= 1000) wrap a sub-widget whose real children
        // live in the component's own AtkUldManager.NodeList, not the plain
        // ChildNode/NextSiblingNode chain every other node type uses.
        if ((ushort)node->Type >= 1000)
        {
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
            {
                stats.ComponentsEntered++;
                var nodeList = component->UldManager.NodeList;
                var count = component->UldManager.NodeListCount;
                for (var i = 0; i < count; i++)
                {
                    ApplyScale(nodeList[i], scale, restoreOnly, depth + 1, ref stats);
                }
            }

            return;
        }

        if (node->Type == NodeType.Text)
        {
            var textNode = (AtkTextNode*)node;
            var ptr = (nint)node;
            seenThisPass.Add(ptr);

            if (!baselineFontSize.TryGetValue(ptr, out var baseline))
            {
                baseline = textNode->FontSize;
                baselineFontSize[ptr] = baseline;
            }

            var newSize = restoreOnly
                ? baseline
                : (byte)Math.Clamp((int)Math.Round(baseline * scale), MinFontSize, MaxFontSize);

            textNode->FontSize = newSize;

            if (stats.TextLeavesScaled == 0)
            {
                stats.SampleLeafType = node->Type;
                stats.SampleBaseline = baseline;
                stats.SampleNewSize = newSize;
            }

            stats.TextLeavesScaled++;
            return;
        }

        if (node->ChildCount == 0)
        {
            return;
        }

        var child = node->ChildNode;
        while (child != null)
        {
            ApplyScale(child, scale, restoreOnly, depth + 1, ref stats);
            child = child->NextSiblingNode;
        }
    }
}
