// The tooltip of the mod on a stat bar: the name and the value, the gains and the loss that are not x1, the food types
// on eating, the coming losses, on the Satiety bar the note of the threshold line, and the text of
// the game last. Totals only. It is one node in the hovered bar, at the place of the game's tooltip (left of the bar), and it
// grows upward from the bottom of the bar. It is built on pointerover and removed on pointerout; the step of the page
// moves its times and sums with text data writes only.
import { formatLeft, type TipSource } from './buffTip';
import { comingLossParts, type LossPart } from './comingLoss';
import { fill, partsMissing } from './core';
import { matchBadges } from './stripes';
import type { CoreState, CoreWindow, PageData } from './types';

// A multiplier this near to x1 shows no row, as the gain mark of C#.
const NO_MARK_WITHIN = 0.005;

// A plain signed number with at most one decimal: -36, -31.5.
function plain(x: number): string {
  const r = Math.round(x * 10) / 10;
  return (r > 0 ? '+' : '') + r;
}

function percent(ratio: number): string {
  return plain(ratio * 100) + '%';
}

function el(doc: Document, tag: string, cls: string, text?: string): HTMLElement {
  const node = doc.createElement(tag);
  node.className = cls;
  if (text !== undefined) node.textContent = text;
  return node;
}

// One row of the grid: a label and a value in the one column of the values.
function addRow(doc: Document, grid: HTMLElement, label: string, value: string, cls = ''): { label: HTMLElement; num: HTMLElement } {
  const row = el(doc, 'div', 'stui-stat-row' + (cls ? ' ' + cls : ''));
  const labelNode = el(doc, 'span', 'stui-stat-label', label);
  const num = el(doc, 'span', 'stui-stat-num', value);
  row.appendChild(labelNode);
  row.appendChild(num);
  grid.appendChild(row);
  return { label: labelNode, num };
}

// A line between two sections of the grid; none before the first.
function addSeparator(doc: Document, grid: HTMLElement): void {
  if (grid.children.length > 0) grid.appendChild(el(doc, 'div', 'stui-stat-sep'));
}

// The timing of a part of the coming loss: "-3 each hour for 12:00", "in 14:00" (a loss at the end shows its value
// only in the value column).
function whenText(part: LossPart, texts: Record<string, string>): string {
  const amount = plain(-part.amount);
  const left = formatLeft(part.left);
  if (part.kind === 'end') return fill(texts.lossAtEnd, { left });
  if (part.every === 3600) return fill(texts.lossEachHour, { amount, left });
  return fill(texts.lossEvery, { amount, hours: Math.round((part.every / 3600) * 100) / 100, left });
}

// The open tooltip, for the step: its node, its stat row, and the key and the text nodes of each part of its coming
// loss.
let open: { tip: HTMLElement; row: number; keys: string[]; when: Text[]; parts: Text[] } | null = null;

// A part of the coming loss by its buff and its kind, the same across a push of new data. Two buffs of one config id
// in a row differ by their end.
function keyOf(part: LossPart): string {
  return part.buff.id + ':' + part.buff.end + ':' + part.kind;
}

function hide(): void {
  if (open) open.tip.remove();
  open = null;
}

// The parts of the coming loss of a row at the game time now.
function lossOf(data: PageData, row: number, now: number | null): LossPart[] {
  const stat = data.stats[row];
  return stat ? comingLossParts(row, data.buffs, stat.lossRest, now, { min: stat.lossMin, max: stat.lossMax }) : [];
}

function build(doc: Document, state: CoreState, data: PageData, row: number, now: number | null): HTMLElement | null {
  const attr = (state.attrList || [])[row];
  const stat = data.stats[row];
  if (!attr || !stat) return null;
  const texts = data.texts;
  const tip = el(doc, 'div', 'stui-stat-tip');
  tip.setAttribute('data-no-sync', '');
  const head = el(doc, 'div', 'stui-stat-top');
  head.appendChild(el(doc, 'span', 'stui-stat-name', attr.attrName));
  head.appendChild(el(doc, 'span', 'stui-stat-value', attr.barValue));
  tip.appendChild(head);

  // The gains and the loss as signed percents with no tone (the form of the effect lines); the cap keeps its tone.
  const grid = el(doc, 'div', 'stui-stat-grid');
  if (stat.mark) {
    const gain = addRow(doc, grid, texts.gain, percent(stat.gain) + (stat.mark.tone === 'cap' ? ' MAX' : ''));
    if (stat.mark.tone === 'cap') gain.num.classList.add('stui-cap');
  }
  if (Math.abs(stat.naturalGain) >= NO_MARK_WITHIN) addRow(doc, grid, texts.naturalGain, percent(stat.naturalGain));
  if (Math.abs(stat.loss) >= NO_MARK_WITHIN) addRow(doc, grid, texts.loss, percent(stat.loss));

  const food = stat.food || [];
  if (food.length > 0) {
    addSeparator(doc, grid);
    addRow(doc, grid, texts.gainOnEating, '', 'stui-stat-section');
    for (const f of food) addRow(doc, grid, f.name, percent(f.ratio));
  }

  const keys: string[] = [];
  const when: Text[] = [];
  const partNodes: Text[] = [];
  const parts = lossOf(data, row, now);
  if (parts.length > 0) {
    const names = new Map(matchBadges(doc, state, data.buffs).map((m) => [m.entry, m.name]));
    addSeparator(doc, grid);
    addRow(doc, grid, texts.comingLoss, '', 'stui-stat-section');
    for (const part of parts) {
      const line = addRow(doc, grid, '', plain(-part.sum));
      line.num.classList.add('stui-bad');
      line.label.appendChild(el(doc, 'span', 'stui-stat-source', names.get(part.buff) || ''));
      const whenNode = el(doc, 'span', 'stui-stat-when', whenText(part, texts));
      line.label.appendChild(whenNode);
      keys.push(keyOf(part));
      when.push(whenNode.firstChild as Text);
      partNodes.push(line.num.firstChild as Text);
    }
  }
  if (grid.children.length > 0) tip.appendChild(grid);

  if (stat.threshold)
    tip.appendChild(el(doc, 'div', 'stui-stat-note', fill(texts.wellFedLine, { start: stat.threshold.start, end: stat.threshold.end })));
  if (attr.tipDesc && data.switches.statTooltipGameText) tip.appendChild(el(doc, 'div', 'stui-stat-desc', attr.tipDesc));
  open = { tip, row, keys, when, parts: partNodes };
  return tip;
}

// True while a game part of the tooltip is missing: the tooltip then stays off.
let partMissing = false;

function show(w: CoreWindow, bar: Element, row: number, src: TipSource): void {
  const data = src.data();
  if (!data || !data.switches.extendedTooltips || partMissing) return;
  hide();
  const tip = build(w.document, src.state(), data, row, src.now());
  if (tip) bar.appendChild(tip);
}

// Listens to the pointer on the stat bars of the frame once. The game's own handlers of the bar stay.
export function installStatTip(w: CoreWindow, src: TipSource): void {
  const doc = w.document;
  doc.addEventListener('pointerover', (ev) => {
    const bar = (ev.target as Element | null)?.closest?.('.status-row .progress-bar-wrapper');
    if (!bar) return;
    const row = Array.from(doc.querySelectorAll('.status-row')).indexOf(bar.closest('.status-row')!);
    if (row >= 0 && (!open || open.row !== row || !open.tip.isConnected)) show(w, bar, row, src);
  });
  doc.addEventListener('pointerout', (ev) => {
    if (!open) return;
    const bar = (ev.target as Element | null)?.closest?.('.status-row .progress-bar-wrapper');
    const to = (ev as MouseEvent).relatedTarget as Node | null;
    if (bar && (!to || !bar.contains(to))) hide();
  });
}

// No open tooltip while the switch is off or a game part is missing. An open tooltip is built again from the state
// and the data of this pass (a natural change of the stat, a push), and its node is swapped only when its content
// differs. The class of the body that hides the game's tooltips is set by buffTip.ts. Gives the missing parts, or
// none while the game has no stat row yet.
export function applyStatTip(doc: Document, state: CoreState, data: PageData | null, now: number | null): string[] {
  const on = !!(data && data.switches.extendedTooltips);
  const missing = on && (state.attrList || []).length > 0 ? partsMissing(doc, 'statTip') : [];
  partMissing = missing.length > 0;
  if (!on || partMissing) hide();
  else if (open && open.tip.isConnected) refresh(doc, state, data!, now);
  return missing;
}

function refresh(doc: Document, state: CoreState, data: PageData, now: number | null): void {
  const old = open!;
  const fresh = build(doc, state, data, old.row, now);
  if (!fresh) { open = old; hide(); return; }
  if (fresh.innerHTML === old.tip.innerHTML) { open = old; return; }
  old.tip.replaceWith(fresh);
}

function setData(node: Text | null, text: string): void {
  if (node && node.data !== text) node.data = text;
}

// The step of the page: the times and the values of the coming loss of the open tooltip, with text data writes
// only. Each row moves with its own part; a part that ends keeps its row until the pointer comes again.
export function stepStatTip(data: PageData, now: number | null): void {
  if (!open || !open.tip.isConnected) return;
  for (const part of lossOf(data, open.row, now)) {
    const i = open.keys.indexOf(keyOf(part));
    if (i < 0) continue;
    setData(open.when[i], whenText(part, data.texts));
    setData(open.parts[i], plain(-part.sum));
  }
}
