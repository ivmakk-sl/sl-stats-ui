// The tooltip of the mod on a buff badge ("1 Plain"): a neutral dark box with the title in the buff or debuff tone,
// the time left with a time bar, the effect lines, the text of the game, and the layer count above one. While the extended tooltips
// are on, the body has the class stui-tips, under which the CSS hides the game's tooltip. The tooltip is one node in
// the hovered badge, at the place of the game's tooltip; it is built on pointerover and removed on pointerout. The
// step of the page moves its time with a text data write and a style write, so no observer of a childList wakes.
import { setStyle } from './core';
import { matchBadges, type BadgeMatch } from './stripes';
import type { CoreState, CoreWindow, PageData } from './types';

// What the tooltip reads at the time of an event: the data of the last push, the page clock, and the page state.
export interface TipSource {
  data(): PageData | null;
  now(): number | null;
  state(): CoreState;
}

// The open tooltip and its badge entry, for the step.
let open: { tip: HTMLElement; match: BadgeMatch } | null = null;

// The time left in the game's format of a buff time (formatEventTime of CoreUI1): game hours and minutes.
export function formatLeft(seconds: number): string {
  const s = Math.max(0, seconds);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return String(h).padStart(2, '0') + ':' + String(m).padStart(2, '0');
}

function leftPart(match: BadgeMatch, now: number | null): number {
  const e = match.entry;
  if (now === null || e.full <= 0) return 1;
  return Math.min(1, Math.max(0, (e.end - now) / e.full));
}

function el(doc: Document, tag: string, cls: string, text?: string): HTMLElement {
  const node = doc.createElement(tag);
  node.className = cls;
  if (text !== undefined) node.textContent = text;
  return node;
}

function build(doc: Document, match: BadgeMatch, data: PageData, now: number | null): HTMLElement {
  const e = match.entry;
  const tip = el(doc, 'div', 'stui-buff-tip');
  tip.setAttribute('data-no-sync', '');
  const head = el(doc, 'div', 'stui-tip-head');
  head.appendChild(el(doc, 'span', 'stui-tip-title ' + (e.good ? 'stui-good' : 'stui-bad'), match.name));
  tip.appendChild(head);
  if (e.end >= 0) {
    head.appendChild(el(doc, 'span', 'stui-tip-time', formatLeft(now === null ? 0 : e.end - now)));
    const bar = el(doc, 'div', 'stui-tip-bar');
    bar.appendChild(el(doc, 'div', 'stui-tip-fill'));
    tip.appendChild(bar);
  }
  if ((e.lines && e.lines.length > 0) || e.layers > 1) {
    const lines = el(doc, 'div', 'stui-tip-lines');
    for (const line of e.lines || []) {
      const row = el(doc, 'div', 'stui-tip-line');
      row.appendChild(el(doc, 'span', 'stui-tip-label', line.label));
      row.appendChild(el(doc, 'span', 'stui-tip-value' + (line.bad ? ' stui-bad' : ''), line.value));
      lines.appendChild(row);
    }
    if (e.layers > 1) {
      const layers = el(doc, 'div', 'stui-tip-layers');
      layers.appendChild(el(doc, 'span', 'stui-tip-label', data.texts.layers || ''));
      layers.appendChild(el(doc, 'span', 'stui-tip-value', e.maxLayers > 1 ? e.layers + '/' + e.maxLayers : String(e.layers)));
      lines.appendChild(layers);
    }
    tip.appendChild(lines);
  }
  if (e.desc) tip.appendChild(el(doc, 'div', 'stui-tip-desc', e.desc));
  return tip;
}

// The time of the open tooltip: text data and a style only.
function update(tip: HTMLElement, match: BadgeMatch, now: number | null): void {
  const time = tip.querySelector('.stui-tip-time');
  if (time && time.firstChild && now !== null) {
    const text = formatLeft(match.entry.end - now);
    const node = time.firstChild as Text;
    if (node.data !== text) node.data = text;
  }
  const fill = tip.querySelector<HTMLElement>('.stui-tip-fill');
  if (fill) setStyle(fill, '--stui-left', Math.round(leftPart(match, now) * 10000) / 100 + '%');
}

function hide(): void {
  if (open) open.tip.remove();
  open = null;
}

function show(doc: Document, badge: HTMLElement, src: TipSource): void {
  const data = src.data();
  if (!data || !data.switches.extendedTooltips) return;
  const match = matchBadges(doc, src.state(), data.buffs).find((m) => m.el === badge);
  if (!match) return;
  hide();
  const now = src.now();
  const tip = build(doc, match, data, now);
  update(tip, match, now);
  badge.appendChild(tip);
  open = { tip, match };
}

// Listens to the pointer on the badges of the frame once. The game's own handlers of the badge stay.
export function installBuffTip(w: CoreWindow, src: TipSource): void {
  const doc = w.document;
  doc.addEventListener('pointerover', (ev) => {
    const badge = (ev.target as Element | null)?.closest?.('.status-row .buff') as HTMLElement | null;
    if (badge && (!open || open.tip.parentElement !== badge)) show(doc, badge, src);
  });
  doc.addEventListener('pointerout', (ev) => {
    if (!open) return;
    const badge = open.tip.parentElement;
    const to = (ev as MouseEvent).relatedTarget as Node | null;
    if (!badge || !to || !badge.contains(to)) hide();
  });
}

// The class of the body that hides the game's tooltips, and no open tooltip while the switch is off.
export function applyBuffTip(doc: Document, data: PageData | null): string[] {
  const on = !!(data && data.switches.extendedTooltips);
  if (doc.body.classList.contains('stui-tips') !== on) doc.body.classList.toggle('stui-tips', on);
  if (!on) hide();
  return [];
}

// The step of the page: the time of the open tooltip.
export function stepBuffTip(now: number | null): void {
  if (open && open.tip.isConnected) update(open.tip, open.match, now);
}
