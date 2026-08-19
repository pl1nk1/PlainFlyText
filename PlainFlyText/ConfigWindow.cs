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
    private readonly string versionLabel;

    public ConfigWindow(Configuration config, IDalamudPluginInterface pluginInterface, FlyTextSpeedHook speedHook)
        : base(BuildTitle(pluginInterface))
    {
        this.config = config;
        this.pluginInterface = pluginInterface;
        this.speedHook = speedHook;

        versionLabel = $"v{pluginInterface.Manifest.AssemblyVersion}" + (pluginInterface.IsDev ? " (Dev)" : "");

        Size = new Vector2(420, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    // Version is baked into the window title (##id suffix keeps window identity
    // stable across version bumps) so it's visible even before opening the window
    // if the title bar is glanced at, e.g. in a taskbar/window list.
    private static string BuildTitle(IDalamudPluginInterface pluginInterface)
        => $"PlainFlyText Settings - v{pluginInterface.Manifest.AssemblyVersion}"
           + (pluginInterface.IsDev ? " (Dev)" : string.Empty)
           + "##PlainFlyText";

    public override void Draw()
    {
        ImGui.TextDisabled(versionLabel);
        ImGui.Separator();

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
            "Experimental: scales the font size of flytext's individual text nodes " +
            "directly, based on each node's own native size at the moment it's first " +
            "seen. Check /xllog for diagnostic output while this is on. Try it and see.");

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
