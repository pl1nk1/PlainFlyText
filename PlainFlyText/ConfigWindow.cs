using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace PlainFlyText;

internal sealed class ConfigWindow : Window
{
    private static readonly string[] AlignmentLabels = ["Left", "Center", "Right"];

    private readonly Configuration config;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly FontManager fontManager;
    private readonly ScreenLogHook screenLogHook;

    public ConfigWindow(Configuration config, IDalamudPluginInterface pluginInterface, FontManager fontManager, ScreenLogHook screenLogHook)
        : base("PlainFlyText Settings##PlainFlyText")
    {
        this.config = config;
        this.pluginInterface = pluginInterface;
        this.fontManager = fontManager;
        this.screenLogHook = screenLogHook;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 280),
            MaximumSize = new Vector2(800, 800),
        };
        Size = new Vector2(420, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        if (!screenLogHook.IsAvailable)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f),
                "Custom rendering unavailable: the native flytext hook failed to load.");
            ImGui.TextWrapped("Using default (blank-label) mode instead. See /xllog for details.");
            ImGui.Separator();
        }

        ImGui.BeginDisabled(!screenLogHook.IsAvailable);

        var enabled = config.CustomRenderingEnabled;
        if (ImGui.Checkbox("Enable custom flytext rendering", ref enabled))
        {
            config.CustomRenderingEnabled = enabled;
            changed = true;
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        var fontPath = config.FontPath;
        if (ImGui.InputText("Font file (.ttf)", ref fontPath, 260))
        {
            config.FontPath = fontPath;
        }

        if (!string.IsNullOrEmpty(config.FontPath) && !File.Exists(config.FontPath))
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "File not found. Falling back to the default font.");
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            fontManager.EnsureFont(config.FontPath, config.FontSizePx);
            changed = true;
        }

        var fontSize = config.FontSizePx;
        if (ImGui.SliderFloat("Font size", ref fontSize, 8, 72))
        {
            config.FontSizePx = fontSize;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            fontManager.EnsureFont(config.FontPath, config.FontSizePx);
            changed = true;
        }

        var alignmentIndex = (int)config.Alignment;
        if (ImGui.Combo("Alignment", ref alignmentIndex, AlignmentLabels, AlignmentLabels.Length))
        {
            config.Alignment = (FlyTextAlignment)alignmentIndex;
            changed = true;
        }

        var duration = config.Duration;
        if (ImGui.SliderFloat("Linger duration (s)", ref duration, 0.5f, 8f))
        {
            config.Duration = duration;
            changed = true;
        }

        var riseSpeed = config.RiseSpeed;
        if (ImGui.SliderFloat("Rise speed (px/s)", ref riseSpeed, 0, 400))
        {
            config.RiseSpeed = riseSpeed;
            changed = true;
        }

        if (changed)
        {
            pluginInterface.SavePluginConfig(config);
        }
    }
}
