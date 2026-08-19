using System;
using System.Numerics;
using Dalamud.Game.Gui.FlyText;

namespace PlainFlyText;

// One in-flight custom-rendered flytext entry. Deliberately holds no native pointers:
// WorldPosition is a plain Vector3 value copied once, synchronously, inside the native
// hook detour (see ScreenLogHook), then re-projected to screen space fresh every frame
// via IGameGui.WorldToScreen. This avoids ever dereferencing a Character* after the hook
// call returns, at the (accepted) cost of not tracking a moving target - which matches
// how native flytext already behaves (anchored at the hit point, not target-following).
internal sealed class ActiveFlyText
{
    public required FlyTextKind Kind { get; init; }

    public required int Val1 { get; init; }

    public required Vector3 WorldPosition { get; init; }

    // Approximate native palette for the kinds we handle, not pixel-perfect - see
    // FlyTextKindSet/ScreenLogHook for why this isn't correlated with the native color.
    public required Vector4 Color { get; init; }

    public float TimeElapsed { get; set; }

    public float Alpha(float duration)
        => Math.Clamp(1f - (TimeElapsed / duration), 0f, 1f);

    public float YOffset(float riseSpeed)
        => -riseSpeed * TimeElapsed;

    public string FormatText()
        => Val1.ToString("N0");
}
