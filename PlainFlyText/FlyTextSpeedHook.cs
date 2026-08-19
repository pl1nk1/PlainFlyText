using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace PlainFlyText;

// Hooks the native FlyText addon's Update(float delta) virtual function - resolved via
// FFXIVClientStructs' own maintained AddonFlyText.StaticVirtualTablePointer, not a
// hand-rolled signature, so this rides on FFXIVClientStructs' upkeep across game
// patches rather than our own guess.
//
// Handles two independent, additive tweaks from inside the same hook:
//  - Speed: multiplies the delta passed to the addon's own native update logic.
//  - Size (experimental): re-applies AtkUnitBase.SetScale AFTER calling the native
//    Update, once per tick. This has to happen from inside this hook rather than a
//    separate per-frame subscription (that's what an earlier version did) - the
//    native Update appears to re-apply its own HUD-layout-driven scale each frame,
//    which raced with and silently overwrote a scale set from outside. Applying ours
//    immediately after hook.Original() returns guarantees we're the last write for
//    that frame instead of racing native code for ordering.
//
// Neither tweak touches rendering, position, font, or the label-blanking behavior in
// Plugin.cs - speed only ever changes the delta value, and size only ever changes the
// window's Scale transform, so everything else about flytext stays native.
internal sealed unsafe class FlyTextSpeedHook : IDisposable
{
    private readonly Configuration config;
    private readonly IGameGui gameGui;

    private Hook<UpdateDelegate>? hook;

    public FlyTextSpeedHook(Configuration config, IGameGui gameGui)
    {
        this.config = config;
        this.gameGui = gameGui;
    }

    public bool IsAvailable { get; private set; }

    private delegate void UpdateDelegate(AddonFlyText* thisPtr, float delta);

    public void Initialize(IGameInteropProvider gameInteropProvider, IPluginLog log)
    {
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
        // Reset scale to native before unhooking, so we don't leave flytext stuck
        // scaled if the plugin unloads/disables while size scaling was enabled.
        var addon = gameGui.GetAddonByName<AddonFlyText>("FlyText");
        if (addon != null)
        {
            addon->SetScale(1.0f, false);
        }

        hook?.Disable();
        hook?.Dispose();
    }

    private void Detour(AddonFlyText* thisPtr, float delta)
    {
        hook!.Original(thisPtr, delta * config.SpeedMultiplier);

        var targetScale = config.SizeScalingEnabled ? config.SizeMultiplier : 1.0f;
        thisPtr->SetScale(targetScale, false);
    }
}
