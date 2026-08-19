using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace PlainFlyText;

// Controls where the player's own flytext groups render on screen, via a struct
// embedded directly in the AddonFlyText instance itself (unlike scale, which lives
// on a persistent agent - see FlyTextScaleController). Reused verbatim from
// Aireil/FlyTextFilter (github.com/Aireil/FlyTextFilter, FlyTextHandler.cs's
// SetPositions/ApplyPositions and the AddonFlyTextOnSetup reapply hook).
//
// Only groups [0] (healing, on self) and [1] (status/damage, on self) are exposed -
// matches the reference plugin's own scope: this moves where the player's own
// flytext anchors on screen, not per-character/per-hit positioning.
//
// Because this value lives on the addon (not a persistent agent), it needs
// reapplying whenever the addon reinitializes (e.g. a HUD Layout reload) - hence
// the extra OnSetup hook here, which scale doesn't need.
internal sealed unsafe class FlyTextPositionController : IDisposable
{
    private readonly Configuration config;
    private readonly IGameGui gameGui;

    private short flyTextArrayOffset;
    private Hook<OnSetupDelegate>? onSetupHook;
    private IPluginLog? log;
    private (float X, float Y)? nativeHealingPosition;
    private (float X, float Y)? nativeStatusDamagePosition;

    public FlyTextPositionController(Configuration config, IGameGui gameGui)
    {
        this.config = config;
        this.gameGui = gameGui;
    }

    public bool IsAvailable { get; private set; }

    private delegate void* OnSetupDelegate(void* a1, void* a2, void* a3);

    public void Initialize(ISigScanner sigScanner, IGameInteropProvider gameInteropProvider, IPluginLog pluginLog)
    {
        log = pluginLog;

        try
        {
            flyTextArrayOffset = *(short*)sigScanner.ScanModule("?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? 33 ED C7");

            var onSetupAddress = sigScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 80 89");
            onSetupHook = gameInteropProvider.HookFromAddress<OnSetupDelegate>(onSetupAddress, OnSetupDetour);
            onSetupHook.Enable();

            IsAvailable = true;
            log.Information("PlainFlyText: flytext position offset/hook resolved.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to resolve flytext position offset/hook; position controls will have no effect.");
            IsAvailable = false;
            onSetupHook = null;
        }
    }

    public void Dispose()
    {
        RestoreNative();
        onSetupHook?.Disable();
        onSetupHook?.Dispose();
    }

    // Reads the addon's current live position for both groups - used to seed the
    // config sliders with something sensible instead of an arbitrary guess.
    public bool TryCaptureCurrent(out float healingX, out float healingY, out float statusDamageX, out float statusDamageY)
    {
        healingX = healingY = statusDamageX = statusDamageY = 0f;

        var flyTextArray = GetFlyTextArray();
        if (flyTextArray == null)
        {
            return false;
        }

        var healing = (*flyTextArray)[0];
        var statusDamage = (*flyTextArray)[1];
        if (healing == null || statusDamage == null)
        {
            return false;
        }

        healingX = healing->X;
        healingY = healing->Y;
        statusDamageX = statusDamage->X;
        statusDamageY = statusDamage->Y;
        return true;
    }

    public void Apply()
    {
        if (!config.PositionOverrideEnabled)
        {
            return;
        }

        var flyTextArray = GetFlyTextArray();
        if (flyTextArray == null)
        {
            return;
        }

        var healing = (*flyTextArray)[0];
        if (healing != null && config.HealingPositionX != null && config.HealingPositionY != null)
        {
            nativeHealingPosition ??= (healing->X, healing->Y);
            healing->X = config.HealingPositionX.Value;
            healing->Y = config.HealingPositionY.Value;
        }

        var statusDamage = (*flyTextArray)[1];
        if (statusDamage != null && config.StatusDamagePositionX != null && config.StatusDamagePositionY != null)
        {
            nativeStatusDamagePosition ??= (statusDamage->X, statusDamage->Y);
            statusDamage->X = config.StatusDamagePositionX.Value;
            statusDamage->Y = config.StatusDamagePositionY.Value;
        }
    }

    public void RestoreNative()
    {
        var flyTextArray = GetFlyTextArray();
        if (flyTextArray == null)
        {
            return;
        }

        if (nativeHealingPosition != null)
        {
            var healing = (*flyTextArray)[0];
            if (healing != null)
            {
                healing->X = nativeHealingPosition.Value.X;
                healing->Y = nativeHealingPosition.Value.Y;
            }
        }

        if (nativeStatusDamagePosition != null)
        {
            var statusDamage = (*flyTextArray)[1];
            if (statusDamage != null)
            {
                statusDamage->X = nativeStatusDamagePosition.Value.X;
                statusDamage->Y = nativeStatusDamagePosition.Value.Y;
            }
        }
    }

    private FlyTextArray* GetFlyTextArray()
    {
        if (!IsAvailable)
        {
            return null;
        }

        var addon = (nint)gameGui.GetAddonByName<AddonFlyText>("_FlyText");
        return addon == 0 ? null : (FlyTextArray*)(addon + flyTextArrayOffset);
    }

    private void* OnSetupDetour(void* a1, void* a2, void* a3)
    {
        var result = onSetupHook!.Original(a1, a2, a3);

        try
        {
            Apply();
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PlainFlyText: exception reapplying flytext position after addon setup.");
        }

        return result;
    }
}
