using Dalamud.Configuration;

namespace PlainFlyText;

internal enum FlyTextAlignment
{
    Left,
    Center,
    Right,
}

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Master toggle. Defaults off so a fresh install behaves exactly like the
    // original blank-label-only plugin until the user opts in.
    public bool CustomRenderingEnabled { get; set; } = false;

    // Absolute path to a user-supplied .ttf. Empty = fall back to Dalamud's default
    // font. We never bundle/redistribute font files ourselves (copyright).
    public string FontPath { get; set; } = string.Empty;

    public float FontSizePx { get; set; } = 24f;

    public FlyTextAlignment Alignment { get; set; } = FlyTextAlignment.Center;

    // Linger/fade duration, in seconds.
    public float Duration { get; set; } = 3.0f;

    // Upward drift speed, in pixels/second.
    public float RiseSpeed { get; set; } = 120f;
}
