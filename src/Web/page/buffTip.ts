// The tooltip of the mod on a buff badge ("1 Plain"): a neutral dark box with the title in the buff or debuff tone,
// the time left with a time bar, the suppression line of a suppressed debuff, the effect lines, the text of the game,
// the layer count above one, and the cures and the suppressors of a debuff, which a new push builds again while the
// tooltip is open. While the extended tooltips are on, the body has the class stui-tips, under which
// the CSS hides the game's tooltip. The tooltip is one node in the hovered badge, at the place of the game's tooltip;
// it is built on pointerover and removed on pointerout. The step of the page moves its time with a text data write
// and a style write, so no observer of a childList wakes.
import { word } from '../../Shared/i18n/web/words';
import { setStyle } from './core';
import { matchBadges, type BadgeMatch } from './stripes';
import type { CoreState, CoreWindow, CureItem, CureLists, PageData } from './types';

// What the tooltip reads at the time of an event: the data of the last push, the page clock, and the page state.
export interface TipSource {
  data(): PageData | null;
  now(): number | null;
  state(): CoreState;
}

// The open tooltip and its badge entry, for the step, with the key of the data of its cure blocks.
let open: { tip: HTMLElement; match: BadgeMatch; curesKey?: string } | null = null;

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
  if (e.suppressed) tip.appendChild(el(doc, 'div', 'stui-tip-suppressed', e.suppressed));
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
      layers.appendChild(el(doc, 'span', 'stui-tip-label', word(data.texts, 'layers')));
      layers.appendChild(el(doc, 'span', 'stui-tip-value', e.maxLayers > 1 ? e.layers + '/' + e.maxLayers : String(e.layers)));
      lines.appendChild(layers);
    }
    tip.appendChild(lines);
  }
  if (e.desc) tip.appendChild(el(doc, 'div', 'stui-tip-desc', e.desc));
  const cures = buildCures(doc, e.good ? undefined : cureLists(data, e.id), data);
  if (cures) tip.appendChild(cures);
  return tip;
}

// The item lists of a debuff while both switches are on, or undefined.
function cureLists(data: PageData, id: number): CureLists | undefined {
  if (!data.switches.curesInTooltips || !data.cures) return undefined;
  return data.cures[String(id)];
}

// The "Cure with:" and "Suppress with:" blocks, in the order that C# gives, or null with no item line. A label shows
// only with an item line after it.
function buildCures(doc: Document, lists: CureLists | undefined, data: PageData): HTMLElement | null {
  if (!lists || lists.cures.length + lists.suppressors.length === 0) return null;
  const box = el(doc, 'div', 'stui-tip-cures');
  const block = (label: string, items: CureItem[]) => {
    if (items.length === 0) return;
    box.appendChild(el(doc, 'div', 'stui-tip-cure-label', word(data.texts, label)));
    for (const item of items) {
      const row = el(doc, 'div', 'stui-tip-cure' + (item.total > 0 ? '' : ' stui-tip-cure-none'));
      const icon = el(doc, 'span', 'stui-tip-cure-icon');
      if (item.icon) {
        const img = doc.createElement('img');
        img.setAttribute('src', item.icon);
        icon.appendChild(img);
      }
      row.appendChild(icon);
      row.appendChild(el(doc, 'span', 'stui-tip-cure-name', item.name));
      if (item.total > 0) row.appendChild(el(doc, 'span', 'stui-tip-cure-count', 'x' + item.total));
      box.appendChild(row);
    }
  };
  block('curedBy', lists.cures);
  block('suppressedByList', lists.suppressors);
  return box;
}

// The data that the cure blocks of a badge show: its item lists and the two labels.
function curesKey(data: PageData, match: BadgeMatch): string {
  const e = match.entry;
  const lists = e.good ? undefined : cureLists(data, e.id);
  return JSON.stringify(lists ?? null) + '|' + word(data.texts, 'curedBy') + '|' + word(data.texts, 'suppressedByList');
}

// A new push while a tooltip is open: the blocks of that tooltip are built again when their data changed, so a new
// item total shows with no close and reopen.
function refreshCures(data: PageData | null): void {
  if (!open || !open.tip.isConnected || !data) return;
  const key = curesKey(data, open.match);
  if (key === open.curesKey) return;
  open.curesKey = key;
  const e = open.match.entry;
  const doc = open.tip.ownerDocument;
  const next = buildCures(doc, e.good ? undefined : cureLists(data, e.id), data);
  const old = open.tip.querySelector(':scope > .stui-tip-cures');
  if (old && next) old.replaceWith(next);
  else if (old) old.remove();
  else if (next) open.tip.appendChild(next);
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
  open = { tip, match, curesKey: curesKey(data, match) };
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
  else refreshCures(data);
  return [];
}

// The step of the page: the time of the open tooltip.
export function stepBuffTip(now: number | null): void {
  if (open && open.tip.isConnected) update(open.tip, open.match, now);
}
