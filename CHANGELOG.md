# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- A setting takes effect while you play, with no game restart: at once in an in-game config manager, or after an edit of the config file at the next save load or when you open Settings.

## [1.1.0] - 2026-10-25

### Added

- The tooltip of a suppressed debuff (for example Bleeding while Analgesia is active) shows "Suppressed by Analgesia" in place of its effect values.
- The tooltip of a debuff lists the items that cure it ("Cure with:") and the items that suppress it ("Suppress with:"), each with its icon and how many you have in your backpack and home storage. Items you have none of show dimmed. Turn it off with `CuresInTooltips` (see [CONFIG.md](CONFIG.md)).

### Fixed

- A debuff that Analgesia suppresses no longer shows coming losses on the stat bars or in the stat tooltips: the game applies none of its effects while it is suppressed.
- The mod texts follow a change of the game language in the settings again. Survival Log 1.1.18599 changed how the game switches its language.
- With a community translation of the game (Settings > Language), the mod texts show in English, and Stats UI writes no warning to the log.

### Removed

- Support for game versions before 1.1.18599 (the 10/13 Update). Stats UI now needs Survival Log 1.1.18599 or later.

## [1.0.0] - 2026-10-11

### Added

- Gain multipliers beside Satiety, Morale, Stamina, and Life show bonuses in green and penalties in red, such as `x1.65` for 65% more gain.
- Dark shading with thin diagonal lines on each stat bar shows coming losses from current timed buffs and debuffs.
- A faint line on the Satiety bar marks where Well-Fed starts.
- Timer stripes on timed buff and debuff badges shrink with game time, pause with the game, and turn amber when less than 15% remains.
- Buff and debuff tooltips show effect values, remaining time with a time bar, the game's description, and layer counts for stacked effects.
- Stat bar tooltips show modifiers to gains, natural recovery, and losses, food modifiers, and coming losses with their timing. The Satiety tooltip also shows when Well-Fed starts and ends.
- The Inventory button shows carried weight in kilograms and a weight bar with a gap where Encumbered starts.
- A thin dark outline makes stat bar numbers easier to read, and the Life bar shows the game's heart icon.
- Settings let you hide the carried weight, buff timers, extended tooltips, or the game's stat descriptions (see [CONFIG.md](CONFIG.md)).
- English and Chinese text follows your display language.
