using System;
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
//  - Draw(): for the size slider (experimental), walks the addon's node tree and
//    writes ScaleX/ScaleY directly on leaf nodes (ChildCount == 0, and - critically
//    - descending into Component nodes' own AtkUldManager.NodeList first, since
//    those report ChildCount==0 themselves despite wrapping real children in a
//    separate array; see ApplyScaleToLeaves). Applied immediately before calling
//    the native Draw so our value is current at the exact moment rendering reads
//    node state - this used to be applied from inside Update instead, which
//    write-confirmed correctly (read back as the target value right after
//    writing) but still rendered at native size, suggesting something between
//    Update and Draw was resetting it. Diagnostic logging (throttled) reports
//    what got touched each pass, since none of this can be verified without
//    live testing.
//
// Neither tweak touches position, font, or the label-blanking behavior in
// Plugin.cs - speed only changes the delta value, and size only ever writes
// node-local scale fields, so everything else about flytext stays native.
internal sealed unsafe class FlyTextSpeedHook : IDisposable
{
    private const int MaxRecursionDepth = 8;
    private const int MaxNodesPerPass = 4000;
    private const float DiagnosticLogIntervalSeconds = 2f;

    private readonly Configuration config;
    private readonly IGameGui gameGui;

    private Hook<UpdateDelegate>? updateHook;
    private Hook<DrawDelegate>? drawHook;
    private IPluginLog? log;
    private float diagnosticLogTimer;

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
        // Reset any leaf scaling to native before unhooking, so we don't leave
        // flytext stuck scaled if the plugin unloads/disables while enabled.
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon != null && addon->RootNode != null)
        {
            var stats = default(TraversalStats);
            ApplyScaleToLeaves(addon->RootNode, 1.0f, 0, ref stats);
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
        var targetScale = config.SizeScalingEnabled ? config.SizeMultiplier : 1.0f;
        var stats = default(TraversalStats);

        var directChildren = 0;
        if (thisPtr->RootNode != null)
        {
            var sibling = thisPtr->RootNode->ChildNode;
            while (sibling != null && directChildren < MaxNodesPerPass)
            {
                directChildren++;
                sibling = sibling->NextSiblingNode;
            }

            ApplyScaleToLeaves(thisPtr->RootNode, targetScale, 0, ref stats);
        }

        if (config.SizeScalingEnabled)
        {
            // Draw() has no delta parameter, so throttle off ImGui's frame time
            // instead - close enough for a diagnostic log interval.
            diagnosticLogTimer += 1f / 60f;
            if (diagnosticLogTimer >= DiagnosticLogIntervalSeconds)
            {
                diagnosticLogTimer = 0f;
                log?.Information(
                    "PlainFlyText: [Draw] root.ChildCount={ChildCount} vs walked {Walked} direct child(ren); " +
                    "full traversal visited {Visited} node(s) ({Components} component(s) entered), max depth " +
                    "{Depth}, {Leaves} leaf/leaves scaled to {Target}x. Sample leaf: type={LeafType} scale={LeafScale}.",
                    thisPtr->RootNode != null ? thisPtr->RootNode->ChildCount : (ushort)0,
                    directChildren,
                    stats.TotalVisited,
                    stats.ComponentsEntered,
                    stats.MaxDepth,
                    stats.LeavesScaled,
                    targetScale,
                    stats.SampleLeafType,
                    stats.SampleLeafScale);
            }
        }
        else
        {
            diagnosticLogTimer = 0f;
        }

        drawHook!.Original(thisPtr);
    }

    private struct TraversalStats
    {
        public int TotalVisited;
        public int LeavesScaled;
        public int ComponentsEntered;
        public int MaxDepth;
        public NodeType SampleLeafType;
        public float SampleLeafScale;
    }

    private static void ApplyScaleToLeaves(AtkResNode* node, float scale, int depth, ref TraversalStats stats)
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

        // Component nodes (Type >= 1000, per FFXIVClientStructs' own NodeType doc
        // comment) wrap a sub-widget whose real children live in the component's
        // own AtkUldManager.NodeList - a completely separate array, not the plain
        // ChildNode/NextSiblingNode chain. Confirmed via diagnostic logging: a
        // component wrapper reported ChildCount==0 despite the addon's root
        // claiming dozens of total descendants reachable only through it.
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
                    ApplyScaleToLeaves(nodeList[i], scale, depth + 1, ref stats);
                }
            }

            return;
        }

        if (node->ChildCount == 0)
        {
            node->ScaleX = scale;
            node->ScaleY = scale;

            if (stats.LeavesScaled == 0)
            {
                stats.SampleLeafType = node->Type;
                stats.SampleLeafScale = node->ScaleX;
            }

            stats.LeavesScaled++;
            return;
        }

        var child = node->ChildNode;
        while (child != null)
        {
            ApplyScaleToLeaves(child, scale, depth + 1, ref stats);
            child = child->NextSiblingNode;
        }
    }
}
