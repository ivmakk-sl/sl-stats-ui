// The apply pass over the CoreUI1 frame, its body observer, the step of the page, the page clock, and the counts that
// check() reports.
import { ensureStyle, findFrame, makeClock } from './core';
import { applyBuffTip, installBuffTip, stepBuffTip } from './buffTip';
import { applyComingLoss, stepComingLoss } from './comingLoss';
import { applyGainMarks } from './gainMark';
import { applyStripes, stepStripes } from './stripes';
import { applyTextStyle } from './textStyle';
import { applyStatTip, installStatTip, stepStatTip } from './statTip';
import { applyThreshold } from './threshold';
import { applyWeight } from './weight';
import type { CoreState, CoreWindow, PageData } from './types';

// The data of the last setData, null until C# pushed once.
export const view: { data: PageData | null } = { data: null };

// The game time in game seconds, from the anchors of C#.
export const clock = makeClock();

// The step of the shown times. A buff shows its time in game minutes, and a game minute is one real second at
// speed 1, so a quarter of a second is as slow as the shown unit allows at the higher speeds.
const STEP_MS = 250;
let stepTimer: ReturnType<typeof setInterval> | null = null;

// The parts of the HUD that hold each node of the mod. A change anywhere else in the page (the clock, the other HUD
// parts) needs no apply pass.
const WATCHED = '.status-panel, .vertical-tools';

// The missing parts of the last apply pass, which check() names too.
let lastMissing: string[] = [];

// The runs of the body observer, the apply passes, and the time of the passes in milliseconds since the last
// check(), for the Verbose log of C#.
let observerRuns = 0;
let passes = 0;
let passMs = 0;

// The parts inside the watched parts whose changes need no apply pass: the contents of the game's tooltips (drawn on
// each hover) and the tooltips of the mod (added and removed on each hover).
const IGNORED = '.buff-tooltip, .status-tooltip, .stui-buff-tip, .stui-stat-tip';

function isIgnored(node: Node): boolean {
  return node.nodeType === 1 && (node as Element).matches(IGNORED);
}

// True when the record changes the stat panel or the bag button, or adds or removes a node that holds one.
function touchesWatched(record: MutationRecord): boolean {
  const target = record.target as Element;
  if (target.nodeType === 1 && target.closest(WATCHED)) {
    if (target.closest(IGNORED)) return false;
    const nodes = [...Array.from(record.addedNodes), ...Array.from(record.removedNodes)];
    return nodes.length === 0 || !nodes.every(isIgnored);
  }
  for (const list of [record.addedNodes, record.removedNodes]) {
    for (const node of Array.from(list)) {
      if (node.nodeType === 1 && ((node as Element).matches(WATCHED) || (node as Element).querySelector(WATCHED))) return true;
    }
  }
  return false;
}

// Applies each feature to the frame once, and counts the pass and its time. The body observer then drops the
// records of the writes of this pass, so they start no second pass. Gives the missing parts.
function applyAll(w: CoreWindow, state: CoreState): string[] {
  const start = performance.now();
  passes++;
  try { return applyFeatures(w, state); }
  finally {
    passMs += performance.now() - start;
    if (w.__statsuiObserver) w.__statsuiObserver.takeRecords();
  }
}

function applyFeatures(w: CoreWindow, state: CoreState): string[] {
  const doc = w.document;
  ensureStyle(doc);
  const now = clock.now();
  const missing: string[] = [];
  missing.push(...applyTextStyle(doc, state));
  missing.push(...applyGainMarks(doc, state, view.data));
  missing.push(...applyThreshold(doc, state, view.data));
  missing.push(...applyComingLoss(doc, state, view.data, now));
  missing.push(...applyStripes(doc, state, view.data, now));
  missing.push(...applyWeight(doc, state, view.data));
  missing.push(...applyBuffTip(doc, view.data));
  missing.push(...applyStatTip(doc, state, view.data, now));
  // Two features can need the same part, and check() names each part once.
  lastMissing = Array.from(new Set(missing));
  return lastMissing;
}

// One step: the shown times move with the page clock. Style writes only, so no observer wakes. It returns at once
// while there is no frame or no data.
function step(): void {
  const w = findFrame();
  if (!w || !view.data || !w.__statsuiObserver) return;
  try {
    const now = clock.now();
    const state = w.eval('state');
    stepStripes(w.document, state, view.data, now);
    stepComingLoss(w.document, state, view.data, now);
    stepBuffTip(now);
    stepStatTip(view.data, now);
  } catch (e) { /* a page update broke a part; the next setData() call reports it */ }
}

// Applies every feature to one CoreUI1 window and installs its body observer once. Gives the
// "installed"/"missing:"/"error:" text that setData() hands back to C#.
function apply(w: CoreWindow): string {
  let state: CoreState;
  try { state = w.eval('state'); } catch (e) { return 'error: state not reachable (' + e + ')'; }

  let missing: string[];
  try { missing = applyAll(w, state); } catch (e) { return 'error: ' + e; }

  if (!w.__statsuiObserver) {
    // A burst of changes (Vue draws a row in several steps) gets one pass at the next frame, before the frame is
    // drawn. The flag is of this frame: a rebuilt frame gets its own observer.
    let pendingFrame: number | null = null;
    const observer = new MutationObserver((records) => {
      observerRuns++;
      if (pendingFrame !== null || !records.some(touchesWatched)) return;
      pendingFrame = w.requestAnimationFrame(() => {
        pendingFrame = null;
        try { applyAll(w, w.eval('state')); }
        catch (e) { /* a page update broke a part; the next setData() call reports it */ }
      });
    });
    observer.observe(w.document.body, { childList: true, subtree: true });
    w.__statsuiObserver = observer;
    const tipSource = { data: () => view.data, now: () => clock.now(), state: () => w.eval('state') };
    installBuffTip(w, tipSource);
    installStatTip(w, tipSource);
  }
  if (stepTimer === null) stepTimer = setInterval(step, STEP_MS);

  return missing.length ? ('installed; missing: ' + missing.join(', ')) : 'installed';
}

// One apply pass with the current data. No retry: when the CoreUI1 frame is not there, C# sends again.
export function run(): string {
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  return apply(w);
}

// "ok <runs> <passes> <micros>" while the CoreUI1 frame has the body observer, with the counts and the time of the
// passes in microseconds since the last check, and "; missing: <parts>" after it while a part of the last apply pass
// is missing. Any other answer makes C# send the data again.
export function check(): string {
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  if (!w.__statsuiObserver) return 'no observer';
  const result = 'ok ' + observerRuns + ' ' + passes + ' ' + Math.round(passMs * 1000) + (lastMissing.length ? '; missing: ' + lastMissing.join(', ') : '');
  observerRuns = 0;
  passes = 0;
  passMs = 0;
  return result;
}
