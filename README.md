# PlainFlyText

A [Dalamud](https://dalamud.dev) plugin for Final Fantasy XIV that removes the
skill/ability-name label from floating combat text ("flytext"), leaving only the
number. Color, size, and crit/direct-hit bounce animation are untouched. Also adds
an optional speed slider for native flytext's animation.

## Installing

1. In-game, open the Dalamud settings (`/xlsettings`) → **Experimental** tab →
   **Custom Plugin Repositories**.
2. Add this URL and click the `+`:
   ```
   https://raw.githubusercontent.com/pl1nk1/PlainFlyText/main/pluginmaster.json
   ```
3. Save, then find "Plain Fly Text" in the plugin installer (`/xlplugins`) and install it.
4. Run `/plainflytext` to open settings and adjust the speed slider (default 1.00x,
   i.e. unchanged native behavior).

## How it works

Two independent, small hooks - neither replaces native rendering or position:

- **Label removal (always on):** hooks Dalamud's documented `IFlyTextGui.FlyTextCreated`
  event and blanks the `Text1`/`Text2` fields for the `FlyTextKind` values that carry
  a redundant label next to a damage/healing/resource number (`Damage*`,
  `AutoAttackOrDot*`, `Healing*`, `HpDrain`, `MpDrain`). Native draws everything else
  unchanged. See [Plugin.cs](PlainFlyText/Plugin.cs).
- **Speed slider (optional, default 1.00x = no-op):** hooks the native FlyText
  addon's `Update(float delta)` function - resolved via FFXIVClientStructs' own
  maintained `AddonFlyText.StaticVirtualTablePointer`, not a hand-rolled signature -
  and multiplies the per-frame delta by a configurable value. Lower = flytext lingers
  and floats up more slowly; higher = faster. This affects **all** flytext (misses,
  buffs, EXP, crafting, etc.), not just damage/healing numbers, since it's a single
  per-addon time-scale rather than something scoped to individual entries. See
  [FlyTextSpeedHook.cs](PlainFlyText/FlyTextSpeedHook.cs). If the hook fails to
  resolve at startup (unofficial native address, can break on a game patch), it logs
  a warning and the slider simply has no effect - native behavior continues
  unaffected.

## Releasing an update

Bump the code, then push a tag matching `vX.Y.Z` (e.g. `v1.0.1`) on `main`. The
[release workflow](.github/workflows/release.yml) builds the plugin, publishes a
GitHub Release with the install zip, and updates `pluginmaster.json` on `main` so
existing installs pick up the update automatically.

**Maintenance note:** `PlainFlyText/PlainFlyText.json`'s `DalamudApiLevel` is a
static value (currently `15`, matching the Dalamud build installed on the dev
machine at the time this was written). If Dalamud bumps its API level and the
plugin installer starts reporting this plugin as incompatible, bump that number to
match and cut a new release.
