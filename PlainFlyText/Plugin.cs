using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
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

    // FlyTextKind values that show a redundant skill/ability-name label (Text1) and/or
    // subtitle (Text2) next to a damage/healing/resource number (Val1). Confirmed against
    // Dalamud's own FlyTextKind doc comments (decompiled from the installed Dalamud.dll):
    // e.g. Damage = "Val1 in serif font, Text2 in sans-serif as subtitle with sans-serif
    // Text1 to the left of the Val1." Both Text1 and Text2 need blanking, not just Text1.
    private static readonly HashSet<FlyTextKind> NumberWithLabelKinds =
    [
        FlyTextKind.AutoAttackOrDot,
        FlyTextKind.AutoAttackOrDotDh,
        FlyTextKind.AutoAttackOrDotCrit,
        FlyTextKind.AutoAttackOrDotCritDh,
        FlyTextKind.Damage,
        FlyTextKind.DamageDh,
        FlyTextKind.DamageCrit,
        FlyTextKind.DamageCritDh,
        FlyTextKind.Healing,
        FlyTextKind.HealingCrit,
        FlyTextKind.HpDrain,
        FlyTextKind.MpDrain,
    ];

    private readonly Configuration config;
    private readonly WindowSystem windowSystem;
    private readonly FlyTextSpeedHook speedHook;
    private readonly ConfigWindow configWindow;
    private readonly Action openConfigUiHandler;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        windowSystem = new WindowSystem("PlainFlyText");

        speedHook = new FlyTextSpeedHook(config, GameGui);
        speedHook.Initialize(GameInteropProvider, Log);

        configWindow = new ConfigWindow(config, PluginInterface, speedHook);
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
        if (!NumberWithLabelKinds.Contains(kind))
        {
            return;
        }

        text1 = SeString.Empty;
        text2 = SeString.Empty;
        // val1, val2, color, icon, damageTypeIcon, yOffset, kind, handled: untouched,
        // so the number itself, its color-coding, and crit/DH bounce are unaffected.
    }

    private void OnCommand(string command, string args) => configWindow.IsOpen = true;

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= openConfigUiHandler;
        FlyTextGui.FlyTextCreated -= OnFlyTextCreated;

        speedHook.Dispose();
        windowSystem.RemoveAllWindows();
    }
}
