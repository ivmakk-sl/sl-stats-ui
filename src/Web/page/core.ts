// The helpers that each feature uses: the game parts, the frame lookup, the style node, and the one write of an
// inline style.
import tokensCss from '../tokens.css?inline';
import pageCss from '../page.css?inline';
import type { CoreWindow } from './types';

// The game selectors that each feature needs. A missing one is reported as a missing part, so a game update shows
// in the log and in the page tests.
export const FEATURES = {
  textStyle: ['.status-row', '.progress-text', '.attr-name'],
  weight: ['.vertical-tools', '.icon-sq'],
  stripes: ['.buff-container'],
  threshold: ['.bar-mask'],
  comingLoss: ['.bar-mask'],
  statTip: ['.progress-bar-wrapper', '.status-tooltip'],
};

export function partsMissing(doc: Document, feature: keyof typeof FEATURES): string[] {
  return (FEATURES[feature] as string[]).filter((part) => !doc.querySelector(part));
}

// The value and the maximum of a bar text of the game ("76/135"), each 0 when the text has none.
export function barNumbers(barValue: string): { value: number; max: number } {
  const [v, m] = String(barValue || '').split('/').map(Number);
  return { value: isFinite(v) && v > 0 ? v : 0, max: isFinite(m) && m > 0 ? m : 0 };
}

// Writes one inline style property only when its value differs: the body observer sees every write, even of an
// identical value, and would apply again forever.
export function setStyle(el: HTMLElement, prop: string, value: string): void {
  if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
}

// The one style node of the mod in the frame: the tokens, then the rules.
export function ensureStyle(doc: Document): void {
  if (doc.getElementById('statsui-style')) return;
  const style = doc.createElement('style');
  style.id = 'statsui-style';
  style.textContent = tokensCss + '\n' + pageCss;
  doc.head.appendChild(style);
}

// The CoreUI1 frame once it has loaded, looked up again on every call: a reload gives it a new contentWindow, so
// nothing here keeps it across calls.
export function findFrame(): CoreWindow | null {
  const frames = document.querySelectorAll('iframe');
  for (let i = 0; i < frames.length; i++) {
    const w = frames[i].contentWindow as CoreWindow | null;
    if (!w) continue;
    try { if (!/CoreUI1\.html/i.test(String(w.location))) continue; } catch (e) { continue; }
    if (w.document.readyState !== 'complete') continue;
    return w;
  }
  return null;
}

// The game time in game seconds: the last anchor of C#, which reads the clock of the game with each push and each
// check (about once each real second while the world runs). The page does not guess the time between two anchors:
// the game slows its time on its own (the idle slowdown), and a buff shows its time in game minutes, which is one
// real second at speed 1.
export function makeClock() {
  let game: number | null = null;
  return {
    anchor(gameSeconds: number): void {
      game = gameSeconds;
    },
    now(): number | null {
      return game;
    },
  };
}
