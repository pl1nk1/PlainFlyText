# PlainFlyText

A minimal [Dalamud](https://dalamud.dev) plugin for Final Fantasy XIV that removes the
skill/ability-name label from floating combat text ("flytext"), leaving only the
number. Color, size, and crit/direct-hit bounce animation are untouched.

## Installing

1. In-game, open the Dalamud settings (`/xlsettings`) → **Experimental** tab →
   **Custom Plugin Repositories**.
2. Add this URL and click the `+`:
   ```
   https://raw.githubusercontent.com/pl1nk1/PlainFlyText/main/pluginmaster.json
   ```
3. Save, then find "Plain Fly Text" in the plugin installer (`/xlplugins`) and install it.

## How it works

Hooks Dalamud's `IFlyTextGui.FlyTextCreated` event and blanks the `Text1`/`Text2`
fields for the `FlyTextKind` values that carry a redundant label next to a
damage/healing/resource number (`Damage*`, `AutoAttackOrDot*`, `Healing*`, `HpDrain`,
`MpDrain`). See [PlainFlyText/Plugin.cs](PlainFlyText/Plugin.cs).

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
