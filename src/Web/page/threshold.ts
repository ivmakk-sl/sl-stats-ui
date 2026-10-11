// The threshold line on the Satiety bar: a faint line at the Satiety value where Well-Fed starts. C# gives the value
// from the config of the game, and the maximum comes from the bar text of the game ("76/135"), so the line moves with
// the maximum. The line goes into the .bar-mask of the row before the text of the game, so the text draws over it.
import { barNumbers, partsMissing, setStyle } from './core';
import type { CoreState, PageData } from './types';

// The Satiety row of state.attrList.
const SATIETY = 0;

export function applyThreshold(doc: Document, state: CoreState, data: PageData | null): string[] {
  const attrs = state.attrList || [];
  const row = doc.querySelectorAll('.status-row')[SATIETY];
  const bar = row ? row.querySelector<HTMLElement>('.bar-mask') : null;
  const threshold = data && data.stats[SATIETY] ? data.stats[SATIETY].threshold : null;
  const max = attrs[SATIETY] ? barNumbers(attrs[SATIETY].barValue).max : 0;
  let line = bar ? bar.querySelector<HTMLElement>(':scope > .stui-threshold') : null;

  if (!bar || !threshold || max < threshold.start) {
    doc.querySelectorAll('.stui-threshold').forEach((el) => el.remove());
    return threshold && attrs.length > 0 ? partsMissing(doc, 'threshold') : [];
  }
  if (!line) {
    line = doc.createElement('i');
    line.className = 'stui-threshold';
  }
  setStyle(line, '--stui-at', Math.round((threshold.start / max) * 10000) / 100 + '%');
  const text = bar.querySelector(':scope > .progress-text');
  if (line.nextElementSibling !== text || line.parentElement !== bar) bar.insertBefore(line, text);
  return [];
}
