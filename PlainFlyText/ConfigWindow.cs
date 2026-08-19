using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace PlainFlyText;

internal sealed class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly FlyTextSpeedHook speedHook;
    private readonly FlyTextScaleController scaleController;
    private readonly FlyTextPositionController positionController;
    private readonly string versionLabel;

    public ConfigWindow(
        Configuration config,
        IDalamudPluginInterface pluginInterface,
        FlyTextSpeedHook speedHook,
        FlyTextScaleController scaleController,
        FlyTextPositionController positionController)
        : base(BuildTitle(pluginInterface))
    {
        this.config = config;
        this.pluginInterface = pluginInterface;
        this.speedHook = speedHook;
        this.scaleController = scaleController;
        this.positionController = positionController;

        versionLabel = $"v{pluginInterface.Manifest.AssemblyVersion}" + (pluginInterface.IsDev ? " (Dev)" : "");

        Size = new Vector2(440, 520);
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

        if (!scaleController.IsAvailable)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Size control unavailable: the scale offsets failed to resolve.");
            ImGui.TextWrapped("The slider below will have no effect. See /xllog for details.");
        }

        ImGui.BeginDisabled(!scaleController.IsAvailable);

        var sizeEnabled = config.SizeScalingEnabled;
        if (ImGui.Checkbox("Enable size scaling", ref sizeEnabled))
        {
            config.SizeScalingEnabled = sizeEnabled;
            pluginInterface.SavePluginConfig(config);

            if (sizeEnabled)
            {
                scaleController.Apply(config.SizeMultiplier);
            }
            else
            {
                scaleController.RestoreNative();
            }
        }

        ImGui.TextWrapped("Overrides the same value FFXIV's own Character Configuration \"Flying " +
                           "Text Size\"/\"Pop-up Text Size\" settings use, with finer control than " +
                           "their 3 preset sizes. Applies to all flytext, not just damage/healing.");

        ImGui.BeginDisabled(!config.SizeScalingEnabled);

        var size = config.SizeMultiplier;
        if (ImGui.SliderFloat("Size", ref size, 0.5f, 3.0f, "%.2fx"))
        {
            config.SizeMultiplier = size;
            pluginInterface.SavePluginConfig(config);
            scaleController.Apply(size);
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (ImGui.Button("Reset size to your native setting"))
        {
            config.SizeScalingEnabled = false;
            pluginInterface.SavePluginConfig(config);
            scaleController.RestoreNative();
        }

        ImGui.Separator();

        if (!positionController.IsAvailable)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Position control unavailable: the position offset/hook failed to resolve.");
            ImGui.TextWrapped("The controls below will have no effect. See /xllog for details.");
        }

        ImGui.BeginDisabled(!positionController.IsAvailable);

        var positionEnabled = config.PositionOverrideEnabled;
        if (ImGui.Checkbox("Enable position override", ref positionEnabled))
        {
            config.PositionOverrideEnabled = positionEnabled;

            if (positionEnabled)
            {
                // Seed the sliders from wherever flytext currently sits, rather
                // than defaulting to an arbitrary guess, the first time this is
                // turned on.
                if (config.HealingPositionX == null && positionController.TryCaptureCurrent(out var hx, out var hy, out var sx, out var sy))
                {
                    config.HealingPositionX = hx;
                    config.HealingPositionY = hy;
                    config.StatusDamagePositionX = sx;
                    config.StatusDamagePositionY = sy;
                }

                positionController.Apply();
            }
            else
            {
                positionController.RestoreNative();
            }

            pluginInterface.SavePluginConfig(config);
        }

        ImGui.TextWrapped("Moves where your OWN flytext anchors on screen - healing numbers and " +
                           "status/damage numbers separately. Doesn't reposition other characters' " +
                           "flytext or individual hits; those aren't controlled by this mechanism.");

        ImGui.BeginDisabled(!config.PositionOverrideEnabled);

        var viewportSize = ImGuiHelpers.MainViewport.Size;

        var healingX = config.HealingPositionX ?? 0f;
        var healingY = config.HealingPositionY ?? 0f;
        ImGui.TextUnformatted("Healing position");
        var healingChanged = ImGui.SliderFloat("Healing X", ref healingX, 0f, viewportSize.X, "%.0f");
        healingChanged |= ImGui.SliderFloat("Healing Y", ref healingY, 0f, viewportSize.Y, "%.0f");
        if (healingChanged)
        {
            config.HealingPositionX = healingX;
            config.HealingPositionY = healingY;
            pluginInterface.SavePluginConfig(config);
            positionController.Apply();
        }

        var statusDamageX = config.StatusDamagePositionX ?? 0f;
        var statusDamageY = config.StatusDamagePositionY ?? 0f;
        ImGui.TextUnformatted("Status/damage position");
        var statusDamageChanged = ImGui.SliderFloat("Status/Damage X", ref statusDamageX, 0f, viewportSize.X, "%.0f");
        statusDamageChanged |= ImGui.SliderFloat("Status/Damage Y", ref statusDamageY, 0f, viewportSize.Y, "%.0f");
        if (statusDamageChanged)
        {
            config.StatusDamagePositionX = statusDamageX;
            config.StatusDamagePositionY = statusDamageY;
            pluginInterface.SavePluginConfig(config);
            positionController.Apply();
        }

        if (ImGui.Button("Capture current position"))
        {
            if (positionController.TryCaptureCurrent(out var hx, out var hy, out var sx, out var sy))
            {
                config.HealingPositionX = hx;
                config.HealingPositionY = hy;
                config.StatusDamagePositionX = sx;
                config.StatusDamagePositionY = sy;
                pluginInterface.SavePluginConfig(config);
            }
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (ImGui.Button("Reset position to native"))
        {
            config.PositionOverrideEnabled = false;
            pluginInterface.SavePluginConfig(config);
            positionController.RestoreNative();
        }
    }
}
