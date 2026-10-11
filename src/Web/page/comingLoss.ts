// The coming loss of each stat bar: the part of the fill that the current buffs and debuffs will take before they
// end. C# gives the loss of each buff and the loss ratios; the page counts the intervals left with the page clock,
// so the sum moves with the game time and needs no push.
import { barNumbers, partsMissing, setStyle } from './core';
import type { BuffData, CoreState, PageData } from './types';

// The game's limits of the loss ratio of a stat, as parts of 1. With no limits, a multiplier only stays at 0 or more.
export interface LossLimits {
  min: number;
  max: number;
}

const NO_LIMITS: LossLimits = { min: -1, max: Infinity };

// The loss multiplier of a stat row at the game time t, as the game applies it at a loss: the raw rest of the loss
// ratio and the part of each buff that still runs at t, summed, then within the limits. A buff's own part is gone at
// its end, before its loss at the end.
function multiplierAt(row: number, buffs: BuffData[], lossRest: number, t: number, limits: LossLimits): number {
  let ratio = lossRest;
  for (const b of buffs) {
    if (b.losses && b.end >= 0 && b.end > t) ratio += b.losses[row].ratio;
  }
  return Math.max(0, 1 + Math.min(limits.max, Math.max(limits.min, ratio)));
}

// One part of the coming loss of a stat row: the loss of each whole interval left of one buff (each), or its loss at
// the end (end), with its base amount, its interval, the count of the intervals left, the time left, and its sum
// with the loss multiplier.
export interface LossPart {
  buff: BuffData;
  kind: 'each' | 'end';
  amount: number;
  every: number;
  ticks: number;
  left: number;
  sum: number;
}

// The parts of the coming loss of one stat row at the game time now: for each buff with an end, the loss of each
// whole interval left (the ticks at now + k x interval) and its loss at the end, each times the loss multiplier at
// that time. A buff with no end counts nothing. No clock gives no part.
export function comingLossParts(row: number, buffs: BuffData[], lossRest: number, now: number | null, limits: LossLimits = NO_LIMITS): LossPart[] {
  const parts: LossPart[] = [];
  if (now === null) return parts;
  for (const b of buffs) {
    if (!b.losses || b.end < 0 || b.end <= now) continue;
    const loss = b.losses[row];
    const left = b.end - now;
    if (loss.each > 0 && loss.every > 0) {
      const ticks = Math.floor(left / loss.every);
      let sum = 0;
      for (let k = 1; k <= ticks; k++) sum += loss.each * multiplierAt(row, buffs, lossRest, now + k * loss.every, limits);
      if (ticks > 0) parts.push({ buff: b, kind: 'each', amount: loss.each, every: loss.every, ticks, left, sum });
    }
    if (loss.atEnd > 0) {
      const sum = loss.atEnd * multiplierAt(row, buffs, lossRest, b.end, limits);
      parts.push({ buff: b, kind: 'end', amount: loss.atEnd, every: 0, ticks: 0, left, sum });
    }
  }
  return parts;
}

// The sum of the coming loss of one stat row at the game time now, stopped at the current value.
export function comingLossSum(row: number, buffs: BuffData[], lossRest: number, value: number, now: number | null, limits: LossLimits = NO_LIMITS): number {
  const sum = comingLossParts(row, buffs, lossRest, now, limits).reduce((total, p) => total + p.sum, 0);
  return Math.min(sum, Math.max(0, value));
}

// A part of 1 as a percent with two decimals.
function percent(part: number): string {
  return Math.round(part * 10000) / 100 + '%';
}

// The place of the hatched part of one row: from the value after the loss to the value, as parts of the maximum.
// The value and the maximum come from the bar text of the game, so a natural change of a stat needs no push.
function placeOf(state: CoreState, data: PageData, row: number, now: number | null): { from: number; width: number } {
  const attr = (state.attrList || [])[row];
  const stat = data.stats[row];
  if (!attr || !stat) return { from: 0, width: 0 };
  const { value, max } = barNumbers(attr.barValue);
  if (max <= 0) return { from: 0, width: 0 };
  const loss = comingLossSum(row, data.buffs, stat.lossRest, value, now, { min: stat.lossMin, max: stat.lossMax });
  return { from: (value - loss) / max, width: loss / max };
}

function write(part: HTMLElement, place: { from: number; width: number }): void {
  setStyle(part, '--stui-from', percent(place.from));
  setStyle(part, '--stui-width', percent(place.width));
}

// Adds the hatched part to each bar with a coming loss and removes each other one. It goes into the .bar-mask of the
// row before the text of the game, so the text draws over it. Gives the missing parts.
export function applyComingLoss(doc: Document, state: CoreState, data: PageData | null, now: number | null): string[] {
  const rows = doc.querySelectorAll('.status-row');
  const attrs = state.attrList || [];
  let missing: string[] = [];
  for (let i = 0; i < rows.length && i < attrs.length; i++) {
    const bar = rows[i].querySelector<HTMLElement>('.bar-mask');
    let part = bar ? bar.querySelector<HTMLElement>(':scope > .stui-loss') : null;
    const place = data ? placeOf(state, data, i, now) : { from: 0, width: 0 };
    if (!bar || place.width <= 0) {
      if (part) part.remove();
      if (!bar && place.width > 0) missing = partsMissing(doc, 'comingLoss');
      continue;
    }
    if (!part) {
      part = doc.createElement('i');
      part.className = 'stui-loss';
      part.setAttribute('data-no-sync', '');
    }
    write(part, place);
    const text = bar.querySelector(':scope > .progress-text');
    if (part.parentElement !== bar || part.nextElementSibling !== text) bar.insertBefore(part, text);
  }
  return missing;
}

// The step of the page: the place of each hatched part that the apply pass added, with style writes only. A part
// whose loss ends keeps a width of 0 until the next apply pass removes it.
export function stepComingLoss(doc: Document, state: CoreState, data: PageData, now: number | null): void {
  const rows = doc.querySelectorAll('.status-row');
  for (let i = 0; i < rows.length; i++) {
    const part = rows[i].querySelector<HTMLElement>('.bar-mask > .stui-loss');
    if (part) write(part, placeOf(state, data, i, now));
  }
}
