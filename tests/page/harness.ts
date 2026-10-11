// The test frame of the page tests: the game's real CoreUI1.html in jsdom, a blank root window that runs the
// bundle as C# does, and the messages that the game's reducers send to the page.
//
// The script runs in the root page and reaches CoreUI1 through iframe.contentWindow, which is the shape here: a
// blank root window with the iframe list given, and the real web page of the game in its own jsdom window.
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';
import vm from 'node:vm';
import { JSDOM } from 'jsdom';
import type { TestContext } from 'vitest';
import type { BuffData, PageData, StatData } from '../../src/Web/page/types';

// The jsdom windows carry the page globals of the game, which have no types.
export type Win = any;

const HERE = path.dirname(url.fileURLToPath(import.meta.url));
// The web pages of the game: SL_WEBUI_DIR when it is set, else the web UI mirror of the game context (game/webui)
// three folders above the mod when there is one, else the web UI folder of the installed game.
const MIRROR = path.join(HERE, '..', '..', '..', '..', 'game', 'webui');
const GAME_DIR = process.env.SL_GAME_DIR || 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const WEB_UI = process.env.SL_WEBUI_DIR
  || (fs.existsSync(MIRROR) ? MIRROR : path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI'));
export const CORE_UI1_HTML = path.join(WEB_UI, 'UI', 'CoreUI1', 'CoreUI1.html');
// The bundle that Vite builds from src/Web/page/ (npm test builds it first).
const PAGE_JS = path.join(HERE, '..', '..', 'obj', 'page', 'page.js');
// The source file with the FEATURES table.
export const FEATURES_PATH = path.join(HERE, '..', '..', 'src', 'Web', 'page', 'core.ts');

export const pageJs = fs.readFileSync(PAGE_JS, 'utf8');
export const coreUi1Html = fs.readFileSync(CORE_UI1_HTML, 'utf8');

// The game page in its own jsdom window, with its scripts run. CoreUI1 starts its own requestAnimationFrame
// loops at load, so the window closes when its test ends.
export async function loadCoreWindow(t: TestContext): Promise<Win> {
  const dom = new JSDOM(coreUi1Html, {
    url: url.pathToFileURL(CORE_UI1_HTML).href,
    runScripts: 'dangerously',
    resources: 'usable',
    pretendToBeVisual: true,
  });
  t.onTestFinished(() => dom.window.close());
  await new Promise<void>((resolve, reject) => {
    if (dom.window.document.readyState === 'complete') return resolve();
    dom.window.addEventListener('load', () => resolve());
    setTimeout(() => reject(new Error('CoreUI1.html did not fire load within 10s')), 10000);
  });
  return dom.window;
}

// The root page that C# runs the script in. The iframe list is given, because jsdom loads no iframe from a file
// URL. setFrame swaps the CoreUI1 window, as a save load builds a new frame in the same root page.
export function rootWindow(t: TestContext, frame: Win | null): Win {
  const root = new JSDOM('<!doctype html><html><body></body></html>', { url: 'file:///Root.html', pretendToBeVisual: true }).window as Win;
  t.onTestFinished(() => root.close());
  const box = { current: frame };
  const real = root.document.querySelectorAll.bind(root.document);
  root.document.querySelectorAll = (sel: string) =>
    sel === 'iframe' ? (box.current ? [{ contentWindow: box.current }] : []) : real(sel);
  root.setFrame = (w: Win | null) => { box.current = w; };
  vm.createContext(root);
  return root;
}

// The bundle and one call in the root window, as C# sends them. vm.runInContext, so the bare `window` of the
// bundle is the root window.
export function runPageJs(root: Win, callExpr: string): string {
  return vm.runInContext(pageJs + ';' + callExpr, root, { filename: 'page.js' });
}

// A call in the root window with no new send of the bundle.
export function call(root: Win, callExpr: string): string {
  return vm.runInContext(callExpr, root);
}

// The data of a push with no stat, buff, or weight data: each switch on, no texts.
export function emptyData(): PageData {
  return {
    switches: { weightReadout: true, timeStripes: true, extendedTooltips: true, statTooltipGameText: true },
    texts: {},
    stats: [],
    buffs: [],
    weight: null,
  };
}

// now: the game time of the anchor, in game seconds.
export function setData(root: Win, data: PageData, now?: number): string {
  return call(root, 'window.__statsui.setData(' + JSON.stringify(data) + (now === undefined ? '' : ',' + now) + ')');
}

export function check(root: Win, now?: number): string {
  return call(root, 'window.__statsui.check(' + (now === undefined ? '' : now) + ')');
}

export function wait(win: Win, ms: number): Promise<void> {
  return new Promise((resolve) => win.setTimeout(resolve, ms));
}

// One stat row of WebUI_CoreUI1_AttrMsg, as Reducer_Web_CoreUI1 builds it.
export interface AttrRow {
  attrName: string;
  attrIcon: string;
  value: number;
  barValue: string;
  barColor: string;
  tipName: string;
  tipValue: string;
  tipDesc: string;
  buffsJson: string;
}

// The four stat rows of a character with no buff, in the game's order and with the game's icons.
export function attrRows(): AttrRow[] {
  const row = (attrName: string, attrIcon: string, now: number, max: number): AttrRow => ({
    attrName, attrIcon, value: now / max, barValue: now + '/' + max, barColor: 'green',
    tipName: attrName, tipValue: now + '/' + max, tipDesc: 'The text of ' + attrName + '.', buffsJson: '[]',
  });
  return [
    row('Satiety', '🍖', 76, 135),
    row('Morale', '🧠', 60, 100),
    row('Stamina', '⚡', 80, 100),
    row('Life', '❤', 90, 100),
  ];
}

// Sends the stat rows as the game does, then waits for Vue to draw them.
export async function postAttr(core: Win, rows: AttrRow[]): Promise<void> {
  core.postMessage({ type: 'WebUI_CoreUI1_AttrMsg', data: { attrList: rows } }, '*');
  await wait(core, 30);
}

// Shows the bag button and the phone button of the HUD, as WebUI_CoreUI1_BtnActiveMsg does after a save load.
export async function postButtons(core: Win): Promise<void> {
  core.postMessage({ type: 'WebUI_CoreUI1_BtnActiveMsg', data: { bagActive: true, phoneActive: true } }, '*');
  await wait(core, 30);
}

// The stats part of a character with no ratio: no gain mark on any row.
export function plainStats(): StatData[] {
  return [0, 1, 2, 3].map(() => ({ mark: null, gain: 0, loss: 0, naturalGain: 0, naturalLoss: 0, lossRest: 0, lossMin: -0.5, lossMax: 2, threshold: null, food: [] }));
}

// One badge of buffsJson, as the game's HUD reducer builds it.
export interface JsonBuff { name: string; isGood: boolean; buffConfigId: number }

// Puts badges into the stat rows (row index to the badges of that row).
export function withBadges(rows: AttrRow[], badges: Record<number, JsonBuff[]>): AttrRow[] {
  for (const k of Object.keys(badges)) rows[Number(k)].buffsJson = JSON.stringify(badges[Number(k)]);
  return rows;
}

// A buff entry of the buffs part.
export function buffData(id: number, row: number, end: number, full: number, good = true): BuffData {
  return { id, row, good, layers: 1, maxLayers: 0, end, full, desc: 'The text of buff ' + id + '.', losses: null, lines: [] };
}

// The badges of one stat row in page order, without a leaving badge.
export function badgesOf(core: Win, row: number): Win[] {
  return [...core.document.querySelectorAll('.status-row')[row].querySelectorAll('.buff-container > .buff')]
    .filter((b: Win) => !b.classList.contains('bf-leave-active'));
}
