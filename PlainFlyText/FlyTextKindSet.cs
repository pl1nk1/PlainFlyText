using System.Collections.Generic;
using Dalamud.Game.Gui.FlyText;

namespace PlainFlyText;

internal static class FlyTextKindSet
{
    // FlyTextKind values that show a redundant skill/ability-name label (Text1) and/or
    // subtitle (Text2) next to a damage/healing/resource number (Val1). Confirmed against
    // Dalamud's own FlyTextKind doc comments (decompiled from the installed Dalamud.dll):
    // e.g. Damage = "Val1 in serif font, Text2 in sans-serif as subtitle with sans-serif
    // Text1 to the left of the Val1." Both Text1 and Text2 need blanking, not just Text1.
    //
    // Shared between Plugin.OnFlyTextCreated (the safe, documented event used to blank
    // labels / suppress native drawing) and ScreenLogHook (the native hook used to
    // capture position for custom rendering) so both paths agree on exactly which kinds
    // are "ours".
    internal static readonly HashSet<FlyTextKind> NumberWithLabel =
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
}
