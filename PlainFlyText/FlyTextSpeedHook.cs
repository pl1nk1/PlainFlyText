using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PlainFlyText;

// Hooks the native FlyText addon's Update(float delta) virtual function - resolved via
// FFXIVClientStructs' own maintained AddonFlyText.StaticVirtualTablePointer, not a
// hand-rolled signature, so this rides on FFXIVClientStructs' upkeep across game
// patches rather than our own guess.
//
// Handles two independent, additive tweaks from inside the same hook:
//  - Speed: multiplies the delta passed to the addon's own native update logic.
//  - Size (experimental): walks the addon's node tree AFTER calling the native
//    Update and writes ScaleX/ScaleY directly on leaf nodes (ChildCount == 0 -
//    i.e. nodes with no children of their own, which should be the actual
//    text/image glyph nodes rather than container/collision nodes). Only leaves
//    are touched deliberately, to avoid compounding scale through ancestor nodes
//    if a container and its child were both scaled (1.5x container * 1.5x child
//    would render as 2.25x, not 1.5x).
//
//    This replaced an earlier attempt that called AtkUnitBase.SetScale on the
//    whole window - that call demonstrably reaches the vtable (confirmed via
//    FFXIVClientStructs' own decompiled source) but visibly did nothing, which
//    suggests FlyText's individual pop-ups aren't driven by the window's own
//    scale during rendering. Diagnostic logging below reports how many leaf
//    nodes get touched each pass, since we can't test this live - if the count
//    is 0 or stays flat regardless of on-screen flytext activity, that's a sign
//    this hierarchy assumption is wrong too and needs rethinking with real data
//    from /xllog rather than another blind guess.
//
// Neither tweak touches position, font, or the label-blanking behavior in
// Plugin.cs - speed only ever changes the delta value, and size only ever writes
// node-local scale fields, so everything else about flytext stays native.
internal sealed unsafe class FlyTextSpeedHook : IDisposable
{
    private const int MaxRecursionDepth = 8;
    private const int MaxNodesPerPass = 4000;
    private const float DiagnosticLogIntervalSeconds = 2f;

    private readonly Configuration config;
    private readonly IGameGui gameGui;

    private Hook<UpdateDelegate>? hook;
    private IPluginLog? log;
    private float diagnosticLogTimer;

    public FlyTextSpeedHook(Configuration config, IGameGui gameGui)
    {
        this.config = config;
        this.gameGui = gameGui;
    }

    public bool IsAvailable { get; private set; }

    private delegate void UpdateDelegate(AddonFlyText* thisPtr, float delta);

    public void Initialize(IGameInteropProvider gameInteropProvider, IPluginLog pluginLog)
    {
        log = pluginLog;

        try
        {
            var updateAddress = (nint)AddonFlyText.StaticVirtualTablePointer->Update;
            hook = gameInteropProvider.HookFromAddress<UpdateDelegate>(updateAddress, Detour);
            hook.Enable();
            IsAvailable = true;
            log.Information("PlainFlyText: flytext hook installed.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to install the flytext hook; the speed/size sliders will have no effect.");
            IsAvailable = false;
            hook = null;
        }
    }

    public void Dispose()
    {
        // Reset any leaf scaling to native before unhooking, so we don't leave
        // flytext stuck scaled if the plugin unloads/disables while enabled.
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon != null && addon->RootNode != null)
        {
            var resetCount = 0;
            ApplyScaleToLeaves(addon->RootNode, 1.0f, 0, ref resetCount);
        }

        hook?.Disable();
        hook?.Dispose();
    }

    private void Detour(AddonFlyText* thisPtr, float delta)
    {
        hook!.Original(thisPtr, delta * config.SpeedMultiplier);

        var targetScale = config.SizeScalingEnabled ? config.SizeMultiplier : 1.0f;
        var scaledCount = 0;

        if (thisPtr->RootNode != null)
        {
            ApplyScaleToLeaves(thisPtr->RootNode, targetScale, 0, ref scaledCount);
        }

        if (config.SizeScalingEnabled)
        {
            diagnosticLogTimer += delta;
            if (diagnosticLogTimer >= DiagnosticLogIntervalSeconds)
            {
                diagnosticLogTimer = 0f;
                log?.Information(
                    "PlainFlyText: size scaling pass touched {Count} leaf node(s) (root ChildCount={ChildCount}, target={Target}x).",
                    scaledCount,
                    thisPtr->RootNode != null ? thisPtr->RootNode->ChildCount : (ushort)0,
                    targetScale);
            }
        }
        else
        {
            diagnosticLogTimer = 0f;
        }
    }

    private static void ApplyScaleToLeaves(AtkResNode* node, float scale, int depth, ref int scaledCount)
    {
        if (node == null || depth > MaxRecursionDepth || scaledCount > MaxNodesPerPass)
        {
            return;
        }

        if (node->ChildCount == 0)
        {
            node->ScaleX = scale;
            node->ScaleY = scale;
            scaledCount++;
            return;
        }

        var child = node->ChildNode;
        while (child != null)
        {
            ApplyScaleToLeaves(child, scale, depth + 1, ref scaledCount);
            child = child->NextSiblingNode;
        }
    }
}
