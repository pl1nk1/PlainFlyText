# PlainFlyText

A [Dalamud](https://dalamud.dev) plugin for Final Fantasy XIV that removes the
skill/ability-name label from floating combat text ("flytext"), leaving only the
number. Optionally goes further and replaces native flytext entirely with a
custom-rendered version, for every character on screen: your own font (any `.ttf`
you provide), text size, alignment, and how long numbers linger before fading.

## Installing

1. In-game, open the Dalamud settings (`/xlsettings`) → **Experimental** tab →
   **Custom Plugin Repositories**.
2. Add this URL and click the `+`:
   ```
   https://raw.githubusercontent.com/pl1nk1/PlainFlyText/main/pluginmaster.json
   ```
3. Save, then find "Plain Fly Text" in the plugin installer (`/xlplugins`) and install it.
4. Run `/plainflytext` to open settings. Custom rendering is off by default; the
   plugin behaves exactly like the label-blanking-only version until you turn it on.

## How it works

Two independent mechanisms, both scoped to the same `FlyTextKind` set (`Damage*`,
`AutoAttackOrDot*`, `Healing*`, `HpDrain`, `MpDrain` — see
[FlyTextKindSet.cs](PlainFlyText/FlyTextKindSet.cs)):

- **Label removal (always on):** hooks Dalamud's documented `IFlyTextGui.FlyTextCreated`
  event and blanks the `Text1`/`Text2` fields, leaving the native-drawn number/color/
  animation untouched. See [Plugin.cs](PlainFlyText/Plugin.cs).
- **Custom rendering (opt-in):** when enabled, additionally hooks a deeper native
  function, `AddScreenLogWithKind`, to capture each hit's world position (signature
  reused from [cultbaus/CBT](https://github.com/cultbaus/CBT) — see
  [ScreenLogHook.cs](PlainFlyText/ScreenLogHook.cs)), suppresses the native draw for
  that entry (`handled = true`), and draws a replacement via a Dalamud ImGui overlay
  ([OverlayWindow.cs](PlainFlyText/OverlayWindow.cs)) using a font loaded through
  Dalamud's font-atlas API ([FontManager.cs](PlainFlyText/FontManager.cs)). This is
  unofficial memory signature scanning and **can break on future game patches** — if
  it fails to resolve at startup, the plugin logs a warning and automatically falls
  back to label-removal-only mode; it never crashes or breaks native flytext.

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
