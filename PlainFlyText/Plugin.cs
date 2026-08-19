using System;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PlainFlyText;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/plainflytext";

    [PluginService] internal static IFlyTextGui FlyTextGui { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem;
    private readonly FontManager fontManager;
    private readonly OverlayWindow overlayWindow;
    private readonly ScreenLogHook screenLogHook;
    private readonly ConfigWindow configWindow;
    private readonly Action openConfigUiHandler;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        windowSystem = new WindowSystem("PlainFlyText");

        fontManager = new FontManager(PluginInterface.UiBuilder.FontAtlas);
        fontManager.EnsureFont(config.FontPath, config.FontSizePx);

        overlayWindow = new OverlayWindow(config, fontManager, GameGui);
        windowSystem.AddWindow(overlayWindow);

        screenLogHook = new ScreenLogHook();
        screenLogHook.Initialize(GameInteropProvider, Log);
        if (!screenLogHook.IsAvailable)
        {
            // Never leave a stale "enabled" config active against a hook that failed
            // to load - always fall back to the safe blank-label-only behavior.
            config.CustomRenderingEnabled = false;
        }

        screenLogHook.FlyTextCaptured += overlayWindow.Add;

        configWindow = new ConfigWindow(config, PluginInterface, fontManager, screenLogHook);
        windowSystem.AddWindow(configWindow);

        FlyTextGui.FlyTextCreated += OnFlyTextCreated;

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;

        openConfigUiHandler = () => configWindow.IsOpen = true;
        PluginInterface.UiBuilder.OpenConfigUi += openConfigUiHandler;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open PlainFlyText settings.",
        });
    }

    private void OnFlyTextCreated(
        ref FlyTextKind kind,
        ref int val1,
        ref int val2,
        ref SeString text1,
        ref SeString text2,
        ref uint color,
        ref uint icon,
        ref uint damageTypeIcon,
        ref float yOffset,
        ref bool handled)
    {
        if (!FlyTextKindSet.NumberWithLabel.Contains(kind))
        {
            return;
        }

        text1 = SeString.Empty;
        text2 = SeString.Empty;
        // val1, val2, color, icon, damageTypeIcon, yOffset, kind: untouched, so if
        // custom rendering is off/unavailable the number/color/crit-bounce are
        // unaffected - only the redundant label is removed, same as before.

        if (config.CustomRenderingEnabled && screenLogHook.IsAvailable)
        {
            // Our overlay owns this entry now; suppress the native draw entirely.
            handled = true;
        }
    }

    private void OnCommand(string command, string args) => configWindow.IsOpen = true;

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= openConfigUiHandler;
        FlyTextGui.FlyTextCreated -= OnFlyTextCreated;
        screenLogHook.FlyTextCaptured -= overlayWindow.Add;

        screenLogHook.Dispose();
        fontManager.Dispose();
        windowSystem.RemoveAllWindows();
    }
}
