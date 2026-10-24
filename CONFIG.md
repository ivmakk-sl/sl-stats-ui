# Configuration

Every setting lives in `BepInEx\config\com.ivmakk.survivallog.statsui.cfg`, written the first time you run the game with the mod installed. Edit it with any text editor while the game is closed. Changes apply at the next game start.

Gain multipliers beside stat names, coming loss shading, the Well-Fed line, the text outline, and the heart icon on the Life bar are always on.

## Display

| Setting | Default | Values | What it does |
|---|---|---|---|
| `WeightReadout` | `true` | `true` / `false` | Shows your carried weight and a weight bar on the HUD's Inventory button. `false` restores the game's button. |
| `TimeStripes` | `true` | `true` / `false` | Shows a timer stripe along the bottom of each timed buff and debuff badge. `false` hides these stripes. |
| `ExtendedTooltips` | `true` | `true` / `false` | Shows effect values and timers when you hover over stat bars or buff and debuff badges. `false` restores the game's tooltips. |
| `StatTooltipGameText` | `true` | `true` / `false` | Keeps the game's stat description at the bottom of each stat bar tooltip. `false` hides that description. Applies only when `ExtendedTooltips` is `true`; buff and debuff descriptions stay visible. |
| `CuresInTooltips` | `true` | `true` / `false` | Lists the items that cure or suppress a debuff ("Cure with:" and "Suppress with:") in its tooltip, with how many you have in your backpack and home storage. `false` hides these lists. Applies only when `ExtendedTooltips` is `true`. |

## General

| Setting | Default | Values | What it does |
|---|---|---|---|
| `Verbose` | `false` | `true` / `false` | Writes detailed diagnostic messages about HUD updates and displayed values at Debug level. Leave `false` in normal play. |

To record these messages in `BepInEx\LogOutput.log`, also include `Debug` in `LogLevels` under `[Logging.Disk]` in `BepInEx\config\BepInEx.cfg`.
