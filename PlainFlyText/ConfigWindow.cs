using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace PlainFlyText;

internal sealed class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly FlyTextSpeedHook speedHook;

    public ConfigWindow(Configuration config, IDalamudPluginInterface pluginInterface, FlyTextSpeedHook speedHook)
        : base("PlainFlyText Settings##PlainFlyText")
    {
        this.config = config;
        this.pluginInterface = pluginInterface;
        this.speedHook = speedHook;

        Size = new Vector2(380, 160);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (!speedHook.IsAvailable)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Speed control unavailable: the native hook failed to load.");
            ImGui.TextWrapped("The slider below will have no effect. See /xllog for details.");
            ImGui.Separator();
        }

        var speed = config.SpeedMultiplier;
        if (ImGui.SliderFloat("Speed", ref speed, 0.25f, 3.0f, "%.2fx"))
        {
            config.SpeedMultiplier = speed;
            pluginInterface.SavePluginConfig(config);
        }

        ImGui.TextWrapped("Lower = flytext lingers and floats up more slowly. Higher = faster. " +
                           "Applies to all flytext (misses, buffs, EXP, etc.), not just damage/healing numbers.");

        if (ImGui.Button("Reset to native (1.00x)"))
        {
            config.SpeedMultiplier = 1.0f;
            pluginInterface.SavePluginConfig(config);
        }
    }
}
