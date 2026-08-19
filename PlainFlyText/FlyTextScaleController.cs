using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PlainFlyText;

// Writes directly to the AgentScreenLog agent's flytext/pop-up-text scale fields -
// the same backing value FFXIV's own native "FlyTextDispSize"/"PopUpTextDispSize"
// character-configuration settings write to (confirmed via
// IGameConfig.UiConfig.TryGetUInt("FlyTextDispSize", ...) reading the 3-tier
// Standard/Large/Maximum -> 1.0/1.2/1.4 setting in FlyTextFilter's source), just
// with continuous float control instead of the native UI's 3 discrete tiers.
//
// This replaces several failed attempts to reach flytext size through the addon's
// own node tree (window-level SetScale, per-node ScaleX/Y, AtkTextNode.FontSize +
// ResizeNodeForCurrentText - see FlyTextSpeedHook's history comment) - none of
// those visibly worked, which makes sense in hindsight: the native code reads
// *this* agent field to compute what it draws, so poking the downstream rendered
// nodes was fighting against a system that resets from here, not adjusting the
// actual input.
//
// Offsets are resolved at runtime via signature scan (the offset value is embedded
// in specific instruction bytes, not a stable named struct field) reused verbatim
// from Aireil/FlyTextFilter (github.com/Aireil/FlyTextFilter,
// FlyTextFilter/FlyTextHandler.cs) - real, shipped, third-party code we're reusing
// rather than a guess of our own. Like every signature-based lever in this plugin,
// it's unofficial and can break on a future game patch; failure degrades to "the
// size slider does nothing" rather than crashing.
internal sealed unsafe class FlyTextScaleController
{
    private byte flyTextScaleOffset;
    private short popupTextScaleOffset;
    private float? nativeFlyTextScale;
    private float? nativePopupTextScale;

    public bool IsAvailable { get; private set; }

    public void Initialize(ISigScanner sigScanner, IPluginLog log)
    {
        try
        {
            flyTextScaleOffset = *(byte*)sigScanner.ScanModule("?? BA ?? ?? ?? ?? F3 0F 59 05 ?? ?? ?? ?? 48 8B CF F3 4C 0F 2C C0");
            popupTextScaleOffset = *(short*)sigScanner.ScanModule("?? ?? ?? ?? BA ?? ?? ?? ?? F3 0F 59 05 ?? ?? ?? ?? 49 8B CD 48 8B 84 24 ?? ?? ?? ?? 48 89 87");
            IsAvailable = true;
            log.Information("PlainFlyText: flytext scale offsets resolved.");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "PlainFlyText: failed to resolve flytext scale offsets; the size slider will have no effect.");
            IsAvailable = false;
        }
    }

    // Applies an override scale, capturing the user's own native value the first
    // time (so we know what to restore if they turn scaling back off) rather than
    // assuming "native" means 1.0 - the user may already have "Large"/"Maximum"
    // set in Character Configuration, and we shouldn't clobber that.
    public void Apply(float scale)
    {
        var agent = GetAgentScreenLog();
        if (agent == 0)
        {
            return;
        }

        var flyTextPtr = (float*)(agent + flyTextScaleOffset);
        var popupTextPtr = (float*)(agent + popupTextScaleOffset);

        nativeFlyTextScale ??= *flyTextPtr;
        nativePopupTextScale ??= *popupTextPtr;

        *flyTextPtr = scale;
        *popupTextPtr = scale;
    }

    // Restores whatever the user's own native scale was before Apply() first
    // touched it. No-op if Apply() was never called.
    public void RestoreNative()
    {
        if (nativeFlyTextScale == null && nativePopupTextScale == null)
        {
            return;
        }

        var agent = GetAgentScreenLog();
        if (agent == 0)
        {
            return;
        }

        if (nativeFlyTextScale != null)
        {
            *(float*)(agent + flyTextScaleOffset) = nativeFlyTextScale.Value;
        }

        if (nativePopupTextScale != null)
        {
            *(float*)(agent + popupTextScaleOffset) = nativePopupTextScale.Value;
        }
    }

    private static nint GetAgentScreenLog()
    {
        var framework = Framework.Instance();
        if (framework == null)
        {
            return 0;
        }

        return (nint)framework->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ScreenLog);
    }
}
