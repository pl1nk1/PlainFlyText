# PlainFlyText

A [Dalamud](https://dalamud.dev) plugin for Final Fantasy XIV that removes the
skill/ability-name label from floating combat text ("flytext"), leaving only the
number. Native rendering is otherwise untouched. Also adds optional speed, size, and
position controls for native flytext.

## Installing

1. In-game, open the Dalamud settings (`/xlsettings`) → **Experimental** tab →
   **Custom Plugin Repositories**.
2. Add this URL and click the `+`:
   ```
   https://raw.githubusercontent.com/pl1nk1/PlainFlyText/main/pluginmaster.json
   ```
3. Save, then find "Plain Fly Text" in the plugin installer (`/xlplugins`) and install it.
4. Run `/plainflytext` to open settings. All sliders default to off/native.

## How it works

Label removal is always on; everything else is opt-in and independent of the others:

- **Label removal:** hooks Dalamud's documented `IFlyTextGui.FlyTextCreated` event
  and blanks the `Text1`/`Text2` fields for the `FlyTextKind` values that carry a
  redundant label next to a damage/healing/resource number (`Damage*`,
  `AutoAttackOrDot*`, `Healing*`, `HpDrain`, `MpDrain`). Native draws everything else
  unchanged. See [Plugin.cs](PlainFlyText/Plugin.cs).
- **Speed slider (default 1.00x = no-op):** hooks the native FlyText addon's
  `Update(float delta)` function - resolved via FFXIVClientStructs' own maintained
  `AddonFlyText.StaticVirtualTablePointer`, not a hand-rolled signature - and
  multiplies the per-frame delta by a configurable value. Lower = flytext lingers
  and floats up more slowly; higher = faster. Affects **all** flytext (misses,
  buffs, EXP, crafting, etc.), not just damage/healing numbers, since it's a single
  per-addon time-scale rather than something scoped to individual entries. See
  [FlyTextSpeedHook.cs](PlainFlyText/FlyTextSpeedHook.cs).
- **Size slider:** overrides a scale field on the `AgentScreenLog` game agent - the
  same backing value FFXIV's own Character Configuration "Flying/Pop-up Text Size"
  settings write to (3 discrete tiers), just with continuous float control. See
  [FlyTextScaleController.cs](PlainFlyText/FlyTextScaleController.cs). Captures your
  actual native setting before ever overriding it, so turning it back off restores
  your real preference instead of resetting to 1.0x.
- **Position controls:** moves where your *own* flytext anchors on screen - healing
  numbers and status/damage numbers separately - via a struct embedded in the
  FlyText addon itself. Doesn't reposition other characters' flytext or individual
  hits. See [FlyTextPositionController.cs](PlainFlyText/FlyTextPositionController.cs).

The size and position mechanisms (offsets, struct layout, and the position
addon-setup reapply hook) are reused verbatim from
[Aireil/FlyTextFilter](https://github.com/Aireil/FlyTextFilter), a real, shipped
plugin - not derived by us. All native offsets/signatures across this plugin are
unofficial and can break on a future game patch; each one fails gracefully (logs a
warning, the corresponding control simply has no effect) rather than crashing or
breaking native flytext.

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
