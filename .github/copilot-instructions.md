# Copilot code-review instructions

This repo is a BepInEx 6 (IL2CPP) Harmony mod for *Survival Log*. Plugins derive from `BasePlugin` and call the game through Il2CppInterop proxy assemblies. Review with these traps in mind; a general C# review misses most of them.

## IL2CPP Harmony traps

- **Getter/setter patches often never fire.** il2cpp inlines trivial accessors, so a `MethodType.Getter`/`.Setter` patch silently does nothing. Flag a new getter patch used as the only mechanism. The reliable change is mutating the backing field at a load hook.
- **Inlined methods.** il2cpp also inlines some private methods into their only caller, and a patch of such a method attaches with no error but never runs. A method with no caller in the native code of the game is inlined or called virtually. Flag a new patch target that is not shown to run in the game.
- **No `is`/`as` across the interop boundary.** Flag `is`, `as`, or a direct cast on a game type. The correct form is `x.TryCast<T>()` then a null check.
- **No `foreach` over game collections.** The interop enumerator lacks the pattern. Expect `GetEnumerator()` / `MoveNext()` / `Current`, or a count plus an indexer.
- **Never read an `Il2CppSystem.ValueTuple<...>` result of a game method,** direct or as a list element. The interop layer reads the fields wrongly and gives garbage with no error. Expect a method that returns a class, a dictionary, or an `Il2CppStructArray`, or the value calculated in the mod.
- **Each patch is a Postfix.** The mod only reads the game and marks the part of the HUD that changed. Flag a Prefix, a patch that writes `__result`, or a patch that changes an argument.
- **Guard game lookups.** Singletons, config lookups, and the web view return null often. Flag an unchecked dereference inside a patch.
- **A patch must not break the game.** A game read sits in a try/catch that logs each distinct error once (`Plugin.WarnOnce`) and leaves that part of the HUD as the game shows it. Flag a game read with no guard.
- **Each patch attaches on its own.** `Plugin.Load` attaches each patch class with `CreateClassProcessor(type).Patch()` in its own try/catch, so a target missing after a game update turns off only its own part. Flag a `PatchAll()`.
- **Window states come from `GetState`, data states from `GetData`.** `ReduxUISystem.GetState<T>` reads only the window states (`State_Web_*`) and gives null for a data state. Read a `State_Data_*` state (`State_Data_Buff`, `State_Data_Player`) with `ReduxUISystem.GetData<T>`. Flag `GetState` of a data state.
- **Read the items from the bag mirror.** Never call `ItemManager.GetAllAvailableItems`: that call walks the home scope and makes each later item move reach about fifty item managers, which the player sees as a delay of seconds on a drag. Flag that call, and any other walk of the home scope through `ItemManager`.

## Web UI

- The game UI is web pages in one web view: a root page hosts each window as an iframe. The HUD is the iframe `CoreUI1`. New DOM comes from a script run in the root page through `ExecuteJavaScript`, which reaches the HUD through `iframe.contentWindow`.
- The HUD page can be rebuilt (a new iframe, or a new root page after a browser rebuild). Flag a page hook that installs once and assumes it stays. The page script keeps its parts with one observer of the HUD and puts them back after the game's own render.
- A page hook wraps the game's function and calls the original first. Flag a hook that replaces the original, or page code with no try/catch around the added DOM work.
- Text that goes into a script string must have no quote or backslash, or must be escaped (`JsonText.Str` or `AppendStr` of the JSON library in `src/Shared/json/`).
- **Send once.** The page script (`src/Web/page/*.ts`, TypeScript built by Vite into one file that the plugin embeds) goes to the root page only when the root page answers that it has no script. The data goes only when it changed. The game hooks only mark a part (`PageTick.Mark`); `PageTick` (a Postfix of `WebUILayer.OnUpdate`) reads the marked parts once in the next frame, and the game-free `src/Web/PushSchedule.cs` decides each send, sends at most one call for each frame, and is the only retry. Flag a send of the script or the data from a game hook or on a hover, a game read in a marking Postfix, and a retry loop (`setTimeout`) in the page script.
- **The data holds no value of the game clock.** The game time goes beside the data, as the anchor of the page clock, and the page counts the time stripes, the time left, and the coming losses from the end times and the game speed. A value that changes each game minute in `PageJson` would make each frame a new send. Flag such a field.
- **A hover asks the game for nothing.** The tooltips of the stat bars and the buff badges are built in the page from the last data. Flag a send to the game on a hover.
- **Styles live in CSS files.** `src/Web/tokens.css` holds the `--stui-` tokens and `src/Web/page.css` the rules; stylelint checks that a rule uses a token, not a raw color. The script sets classes (`stui-*`). Flag a raw color or an inline style in the page script, except the CSS variables that place a part (`--stui-at`, `--stui-left`, and the like).
- **A missing page part logs once.** When a node of the HUD that the mod needs is not there, the page script leaves that part as the game shows it, keeps the other parts working, and `check()` reports the missing part, which the plugin logs once. Flag page code that throws or stops all parts for one missing node.

## Structure and tests

- **Feature folders.** `src/Plugin.cs` holds only the plugin: the config, the patch list, and `Load`. `src/Stats/` reads the stat bars (the gain and loss ratios, the threshold), `src/Buffs/` reads the buffs and debuffs and builds their effect lines, `src/Weight/` reads the carried weight, `src/Cures/` builds the cures and the suppressors of each debuff from the item and buff config (`CuresLogic.cs` holds each rule and has unit tests; `CuresReader.cs` only copies the config fields), and `src/Web/` holds the page script, its CSS, and the C# that sends it.
- **Pure logic is separated and tested.** Logic that does not need the running game (the gain mark, the coming loss and its limits, the effect lines, the weight readout, the send schedule, the page JSON) lives in its own file with no BepInEx or Il2Cpp reference, unit-tested under `tests/StatsUI.Tests`. Flag new pure logic in a patch class, and new pure logic with no test. The page script has Vitest tests against the game's own HUD page under `tests/page/`.
- **The per-frame tick is guarded.** `PageTick` runs inside one try/catch and returns at once while nothing is marked and no check is due, and each distinct warning logs once. Flag work in it that runs on each frame while nothing is pending.
- `netstandard2.1` has no JSON library and the game's Newtonsoft stub cannot be used, so the JSON library in `src/Shared/json/` (`JsonText`, `FlatJson`) is on purpose. Do not suggest `System.Text.Json`.
- **`src/Shared/` is a library copy.** Each folder `src/Shared/<lib>/` is a copy of a shared library of the modding workspace, at the version that its `VERSION` file names, with a hash of each file and a header line in each `.cs`, `.ts`, and `.css` file. It is never edited in this repo: a fix goes into the shared library and comes back with a sync, which also writes `VERSION`. Flag a change of a file under `src/Shared/` with no change of its `VERSION` file. The item pool library (`src/Shared/item-pool/`) gives the item totals of the cures: `Load` calls `ItemPool.Setup` and adds `ItemPool.PatchTypes` to its patch list, and the tick reads the pool again after an item change only while a debuff of the buff list has a cure or a suppressor (`CuresReader.NeedsPool`). Flag a pool read on a hover or in each frame.
- **Mod texts come from the i18n files.** Each text that the mod shows is in `src/i18n/en.json` (each text, the last fallback) and `zh.json`, and the code asks for it by name through the i18n library (`src/Shared/i18n/`). Only the library's `GameTexts`, used by `src/ModTexts.cs`, reads the display language and picks the file, and a language switch of the game marks the texts for a new send (`TextsOnApplyLanguageKey`). The page code reads each mod text that C# sent through the library's `word` and fills it with the library's `fill`, and a tooltip draws only after the first data. Flag a check of the display language anywhere else, an English or Chinese text in C# or page code, and a page fallback text.
- **Patches** prefer a postfix, and tie the `Harmony` instance to the plugin GUID.

## Game rules the mod keeps

- **Display only.** The mod must not change a stat, a buff, a debuff, a weight, or a time of the game. The numbers come from the same values and ratios that the game uses, with the game's limits. Flag any write of game state.
- **No save data and no game file.** The mod writes nothing to the save and changes no file of the game, so the player can remove it at any time. Flag any such write.
- **The HUD keeps the game's behavior.** The stat panel and the backpack button keep the clicks and the hover behavior of the game, apart from the tooltips that the mod replaces when `ExtendedTooltips` is on. Flag a handler that stops a click or a hover of the game.

## Release and config hygiene

- **Config entries.** `General` / `Verbose` (default `false`), and in `Display`: `WeightReadout`, `TimeStripes`, `ExtendedTooltips`, `StatTooltipGameText`, and `CuresInTooltips` (each default `true`, read once at load; `CuresInTooltips` shows only while `ExtendedTooltips` is on). Diagnostic tracing goes on `LogDebug` behind `Verbose`; `LogInfo` stays quiet apart from the load line. Flag a new entry with no `CONFIG.md` row.
- **The plugin GUID never changes.** It is `com.ivmakk.survivallog.statsui`, the BepInEx identity and the config file name. Flag any edit to it.
- **The version is in two places that must agree:** `<Version>` in the csproj and the `BepInPlugin` attribute.
- **No committed build output.** Flag `bin/`, `obj/`, `dist/`, `node_modules/`, or a game DLL in the diff. The build runs Vite, so it needs `npm ci` at the mod root; a change of the npm packages commits `package-lock.json`. Game `<Reference>` entries keep `<Private>false</Private>`.
- **Changelog matches the change.** A player-visible change adds an `[Unreleased]` entry to `CHANGELOG.md` in player-facing wording. An internal-only refactor gets none.
