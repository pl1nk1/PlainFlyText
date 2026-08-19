using Dalamud.Configuration;

namespace PlainFlyText;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Scales the native FlyText addon's per-frame animation delta. 1.0 = native/
    // unchanged. Lower = slower (numbers linger and float up more slowly); higher =
    // faster. Applies to ALL flytext (misses, buffs, EXP, crafting, etc.), not just
    // the damage/healing numbers - it's a single per-addon time-scale, not something
    // that can be scoped to individual entries without much deeper native work.
    public float SpeedMultiplier { get; set; } = 1.0f;

    // Scales the whole native FlyText addon window - see FlyTextScaleController for
    // the positioning caveat. Off by default; SizeMultiplier only applies while
    // SizeScalingEnabled is true.
    public bool SizeScalingEnabled { get; set; } = false;

    public float SizeMultiplier { get; set; } = 1.0f;
}
