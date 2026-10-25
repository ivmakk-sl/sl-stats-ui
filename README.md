# Stats UI

See stat bonuses, coming losses, buff timers, and carried weight on your HUD.

Plan your next meal or medicine dose using the bonuses and losses that affect your stats. Hover over a stat bar or buff badge for the details without opening another window.

## Features

- **Gain multipliers**: a small number such as `x1.65` beside Satiety, Morale, Stamina, or Life shows how much you gain compared with normal. Green means a bonus, red means a penalty, and no number means normal gains.
- **Coming loss**: dark shading with thin diagonal lines shows the remaining losses from your current timed buffs and debuffs. It includes repeated losses and losses when effects end, with stacked effects and loss modifiers applied. Future gains are not included.
- **Well-Fed line**: a faint line on the Satiety bar shows where Well-Fed starts. The line is hidden if your maximum Satiety is below that value.
- **Buff timers**: a thin stripe along the bottom of each timed buff or debuff badge shows how much of its duration remains. It shrinks with game time, pauses with the game, and turns amber when less than 15% remains.
- **Buff and debuff tooltips**: hover over a badge for effect values such as "Life each hour -3" or "Morale gain +50%", a countdown and time bar for timed effects, and a layer count for stacked effects. The game's description stays below. Some buffs and debuffs have no detailed effect lines.
- **Cures in debuff tooltips**: the tooltip of a debuff lists the items that remove it under "Cure with:" and the items that stop its effects under "Suppress with:", for example Painkillers for Stomachache, or Morphine Injection and Bandage for Bleeding. Each line shows the item's icon, its name, and how many you have in your backpack and home storage (`x2`). Items you have none of show dimmed, after the items you have. Up to 10 items show.
- **Stat bar tooltips**: hover over a bar to see its value and modifiers to gains, natural recovery, and losses. Food modifiers and coming losses appear below, with the amount and timing of each loss. The Satiety tooltip also shows when Well-Fed starts and ends.
- **Carried weight**: the Inventory button shows your carried weight in kilograms (for example, `8.6`) and a bar with a gap where Encumbered starts. The number and bar are green below that weight, amber near it, and red above it. The button keeps its icon and works as usual.
- **Clearer stat bars**: a thin dark outline keeps the numbers readable over the fill. The Life bar shows the game's heart icon.

Stats UI only changes the display: stat gains and losses, buff and debuff effects, weights, and timers stay the same. It adds nothing to your save and does not edit game files. You can remove it at any time.

Supports English and Chinese and follows your display language.

Nexus page: https://www.nexusmods.com/survivallog/mods/27

## Reading the numbers

The `x1.65` beside a stat means a 65% bonus to gains, up to the game's `x3` cap. Natural recovery, food effects, and losses have separate modifiers. The stat tooltip shows these modifiers as percentages and leaves out those with no change from normal. The game's description of the stat appears at the bottom by default.

The loss preview includes stacked effects, cold damage reduction from Cold-Resistant Constitution, and changes in loss multipliers as timed effects expire. The shading stops at zero on the bar. Effects without an end time, debuffs that Analgesia suppresses, and effects the mod cannot read are excluded; natural decay and later actions or meals are outside the preview.

Buff and debuff tooltips show each effect's own values, including its layers and any reductions to cold damage or carrying penalties that apply to that effect. The stat tooltip's Coming loss section also includes the stat's loss modifier. The game's original buff description remains below the effect lines, even when its stated value differs. A layer count appears only when an effect has more than one layer. A debuff that Analgesia suppresses shows "Suppressed by Analgesia" in place of its effect values, because the game applies none of its effects then.

The cure lists come from the game's own item data, so a med that a game update adds shows up with no mod update. "Suppress with:" lists Morphine Injection for each debuff that Analgesia can stop, and for Disease Outbreak and Outbreak Worsening the meds whose effect cancels them while it lasts (Fever Meds and Antibiotics). The count is the same as the "Usable at home" count of Item Totals: the backpack, home storage on unlocked floors, and the workbench drawer, without a Brewing Barrel or other storage that the game leaves out of its own count. Item Totals does not need to be installed.

The Well-Fed line uses the game's threshold. Carried weight is rounded up to one decimal place, as in the game's storage window; the number has no unit label. The weight bar fills towards your weight limit, with its gap marking the weight where Encumbered starts.

## Requirements

- Survival Log 1.1.18599 or later.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\StatsUI.dll`

## Uninstall

Delete `StatsUI.dll` from the `BepInEx\plugins` folder.

## Configuration

To change mod settings, edit `BepInEx\config\com.ivmakk.survivallog.statsui.cfg` while the game is closed (see [CONFIG.md](CONFIG.md) for every setting). Changes apply at the next game start.

## Troubleshooting

Tested on Survival Log 1.1.19611 (Steam build `26157732`) with BepInEx `6.0.0-be.788`.

If a part is missing, check `BepInEx\LogOutput.log` for `Stats UI loaded.` and any warnings or errors. A warning names a part of the HUD that the mod could not find; the other parts keep working.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies are game-derived and are not part of this repo. The .NET 8 SDK is required.

The build also builds the page script (TypeScript and CSS in `src/Web/`) with Vite, so Node is required too. The mod root has a `mise.toml` for Node, and the npm packages install once after a clone:

```
mise trust
npm ci
dotnet build src/StatsUI.csproj -c Release
```

`Directory.Build.props` sets `GameDir` to the default Steam install path. If the game is in another place, override it without an edit of the file: set a `GameDir` environment variable, or pass `-p:GameDir=...` on the build. The output DLL is at `src\bin\Release\StatsUI.dll`.

The C# unit tests do not need the game:

```
dotnet test tests/StatsUI.Tests
```

Page tests use the game's web UI files (Vitest with jsdom). Set `SL_WEBUI_DIR` to a copy of those files, or `SL_GAME_DIR` to a non-default game install. Run these checks from the mod root:

```
npm test            # builds the page script, then runs the page tests
npm run lint        # stylelint on the CSS files
npm run typecheck   # TypeScript check of the page script and its tests
```

## Package

Add `-p:Package=true` to a Release build to also write the ready-to-install zip at `dist\StatsUI-<version>.zip`, laid out as `BepInEx\plugins\StatsUI.dll` so a user extracts it at the game root. A plain build skips this step.

```
dotnet build src/StatsUI.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
