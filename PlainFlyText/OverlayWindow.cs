using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace PlainFlyText;

internal sealed class OverlayWindow : Window
{
    private const int MaxEntries = 512;

    private static readonly ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.AlwaysUseWindowPadding
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoTitleBar;

    private readonly Configuration config;
    private readonly FontManager fontManager;
    private readonly IGameGui gameGui;
    private readonly List<ActiveFlyText> entries = [];

    public OverlayWindow(Configuration config, FontManager fontManager, IGameGui gameGui)
        : base("PlainFlyTextOverlay##PlainFlyText", OverlayFlags, forceMainWindow: true)
    {
        this.config = config;
        this.fontManager = fontManager;
        this.gameGui = gameGui;

        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public void Add(ActiveFlyText entry)
    {
        if (entries.Count >= MaxEntries)
        {
            entries.RemoveAt(0);
        }

        entries.Add(entry);
    }

    public override void PreDraw()
    {
        Position = Vector2.Zero;
        Size = ImGuiHelpers.MainViewport.Size;
        base.PreDraw();
    }

    public override void Draw()
    {
        if (!config.CustomRenderingEnabled)
        {
            entries.Clear();
            return;
        }

        if (entries.Count == 0)
        {
            return;
        }

        var dt = ImGui.GetIO().DeltaTime;
        var drawList = ImGui.GetWindowDrawList();

        using var fontScope = fontManager.CurrentFont?.Push();

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            entry.TimeElapsed += dt;

            if (entry.TimeElapsed >= config.Duration)
            {
                entries.RemoveAt(i);
                continue;
            }

            if (!gameGui.WorldToScreen(entry.WorldPosition, out var screenPos))
            {
                continue;
            }

            var text = entry.FormatText();
            var textSize = ImGui.CalcTextSize(text);
            var anchor = screenPos + new Vector2(0, entry.YOffset(config.RiseSpeed));

            var drawPos = config.Alignment switch
            {
                FlyTextAlignment.Left => anchor,
                FlyTextAlignment.Center => anchor - new Vector2(textSize.X / 2f, 0),
                FlyTextAlignment.Right => anchor - new Vector2(textSize.X, 0),
                _ => anchor,
            };

            var color = entry.Color with { W = entry.Color.W * entry.Alpha(config.Duration) };
            drawList.AddText(drawPos, ImGui.GetColorU32(color), text);
        }
    }
}
