using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace PlainFlyText;

// Scales the native FlyText addon's animation speed by hooking its Update(float delta)
// virtual function - resolved via FFXIVClientStructs' own maintained
// AddonFlyText.StaticVirtualTablePointer, not a hand-rolled signature, so this rides
// on FFXIVClientStructs' upkeep across game patches rather than our own guess.
//
// This deliberately does NOT touch rendering, position, font, or the label-blanking
// behavior in Plugin.cs at all - it only ever multiplies the delta passed to the
// addon's own native update logic, so everything else about flytext stays 100% native.
internal sealed unsafe class FlyTextSpeedHook : IDisposable
{
    private readonly Configuration config;

    private Hook<UpdateDelegate>? hook;

    public FlyTextSpeedHook(Configuration config)
    {
        this.config = config;
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
            log.Information("PlainFlyText: flytext speed hook installed.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to install the flytext speed hook; the speed slider will have no effect.");
            IsAvailable = false;
            hook = null;
        }
    }

    public void Dispose()
    {
        hook?.Disable();
        hook?.Dispose();
    }

    private void Detour(AddonFlyText* thisPtr, float delta)
    {
        hook!.Original(thisPtr, delta * config.SpeedMultiplier);
    }
}
