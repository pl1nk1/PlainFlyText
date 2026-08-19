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

    // Overrides the AgentScreenLog scale field FFXIV's own Character Configuration
    // "Flying/Pop-up Text Size" settings write to - see FlyTextScaleController. Off
    // by default; SizeMultiplier only applies while SizeScalingEnabled is true.
    public bool SizeScalingEnabled { get; set; } = false;

    public float SizeMultiplier { get; set; } = 1.0f;

    // Overrides where the player's own flytext groups anchor on screen - see
    // FlyTextPositionController. Null = not yet captured; the config window seeds
    // these from the addon's current live position the first time the toggle is
    // enabled, rather than defaulting to an arbitrary guess like (0, 0).
    public bool PositionOverrideEnabled { get; set; } = false;

    public float? HealingPositionX { get; set; }

    public float? HealingPositionY { get; set; }

    public float? StatusDamagePositionX { get; set; }

    public float? StatusDamagePositionY { get; set; }
}
