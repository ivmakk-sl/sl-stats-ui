// The time stripe at the bottom of each buff badge with an end time: the time left over the full time, amber near
// the end. The apply pass adds and removes the stripe nodes; the step of the page only writes their length, so the
// observers of the page and of the other mods do not wake.
import { partsMissing, setStyle } from './core';
import type { BuffData, CoreState, PageData } from './types';

// A badge, its buff entry, and the name of the badge in the page state.
export interface BadgeMatch { el: HTMLElement; entry: BuffData; name: string }

// Under this part of the full time the stripe is amber.
const WARN_BELOW = 0.15;

// The badges of one row in page order, without a badge that is leaving (the transition-group `bf` keeps it in the
// page for its leave animation).
function badgesOf(row: Element): HTMLElement[] {
  const container = row.querySelector('.buff-container');
  if (!container) return [];
  return Array.from(container.children).filter((el): el is HTMLElement =>
    el.classList.contains('buff') && !el.classList.contains('bf-leave-active'));
}

// Each badge with its buff entry. The n-th badge of a row is state.attrList[row].buffs[n], whose uniqueKey
// `<buffConfigId>#<k>` names the k-th occurrence of that id in the row; C# sends the entries of a row in the order
// of the game's badges, so it is the k-th entry of that id in that row.
export function matchBadges(doc: Document, state: CoreState, buffs: BuffData[]): BadgeMatch[] {
  const matches: BadgeMatch[] = [];
  const attrs = state.attrList || [];
  const rows = doc.querySelectorAll('.status-row');
  for (let i = 0; i < rows.length && i < attrs.length; i++) {
    const list = attrs[i].buffs || [];
    const els = badgesOf(rows[i]);
    for (let n = 0; n < els.length && n < list.length; n++) {
      const [id, k] = String(list[n].uniqueKey).split('#').map(Number);
      let seen = 0;
      const entry = buffs.find((b) => b.row === i && b.id === id && ++seen === k);
      if (entry) matches.push({ el: els[n], entry, name: list[n].name });
    }
  }
  return matches;
}

function setLength(stripe: HTMLElement, entry: BuffData, now: number | null): void {
  let part = 1;
  if (now !== null && entry.full > 0) part = Math.min(1, Math.max(0, (entry.end - now) / entry.full));
  setStyle(stripe, '--stui-left', Math.round(part * 10000) / 100 + '%');
  const warn = now !== null && entry.full > 0 && part < WARN_BELOW;
  if (stripe.classList.contains('stui-warn') !== warn) stripe.classList.toggle('stui-warn', warn);
}

function stripeOf(el: HTMLElement): HTMLElement | null {
  return el.querySelector<HTMLElement>(':scope > .stui-stripe');
}

// Adds a stripe to each badge with an end time, and removes each other stripe. Gives the missing parts.
export function applyStripes(doc: Document, state: CoreState, data: PageData | null, now: number | null): string[] {
  const on = !!(data && data.switches.timeStripes);
  const keep = new Set<HTMLElement>();
  let missing: string[] = [];
  if (on && (state.attrList || []).length > 0) {
    missing = partsMissing(doc, 'stripes');
    for (const { el, entry } of matchBadges(doc, state, data!.buffs)) {
      if (entry.end < 0) continue;
      let stripe = stripeOf(el);
      if (!stripe) {
        stripe = doc.createElement('i');
        stripe.className = 'stui-stripe';
        stripe.setAttribute('data-no-sync', '');
      }
      setLength(stripe, entry, now);
      if (stripe.parentElement !== el) el.appendChild(stripe);
      keep.add(stripe);
    }
  }
  doc.querySelectorAll<HTMLElement>('.stui-stripe').forEach((s) => { if (!keep.has(s)) s.remove(); });
  return missing;
}

// The step of the page: the length of each stripe that the apply pass added, with style writes only.
export function stepStripes(doc: Document, state: CoreState, data: PageData, now: number | null): void {
  if (!data.switches.timeStripes) return;
  for (const { el, entry } of matchBadges(doc, state, data.buffs)) {
    const stripe = stripeOf(el);
    if (stripe) setLength(stripe, entry, now);
  }
}
