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

        Size = new Vector2(420, 320);
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

        if (ImGui.Button("Reset speed to native (1.00x)"))
        {
            config.SpeedMultiplier = 1.0f;
            pluginInterface.SavePluginConfig(config);
        }

        ImGui.Separator();

        var sizeEnabled = config.SizeScalingEnabled;
        if (ImGui.Checkbox("Enable size scaling (experimental)", ref sizeEnabled))
        {
            config.SizeScalingEnabled = sizeEnabled;
            pluginInterface.SavePluginConfig(config);
        }

        ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f),
            "Caveat: this scales the whole flytext window, not each number individually. " +
            "Numbers may visibly drift away from the character they belong to, especially " +
            "at larger values or further from screen center. Try it and see.");

        ImGui.BeginDisabled(!config.SizeScalingEnabled);

        var size = config.SizeMultiplier;
        if (ImGui.SliderFloat("Size", ref size, 0.5f, 3.0f, "%.2fx"))
        {
            config.SizeMultiplier = size;
            pluginInterface.SavePluginConfig(config);
        }

        ImGui.EndDisabled();

        if (ImGui.Button("Reset size to native (1.00x)"))
        {
            config.SizeMultiplier = 1.0f;
            pluginInterface.SavePluginConfig(config);
        }
    }
}
