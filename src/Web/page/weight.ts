// The weight readout on the bag button of the HUD: the carried weight as a number and a weight bar near the bottom
// edge, in two segments split at the Encumbered line. The nodes of the mod sit in one wrapper inside the button, which
// takes no pointer, so the click and the entry tip of the game stay. The wrapper carries the tone, because Vue sets the
// class list of the button itself again on each change of its own class binding.
import { setStyle } from './core';
import type { CoreState, PageData } from './types';

// The start of the path of the briefcase icon: the bag button has no class of its own.
const BAG_PATH = 'M20 6h-4V4';

export function findBagButton(doc: Document): HTMLElement | null {
  const buttons = doc.querySelectorAll<HTMLElement>('.vertical-tools .icon-sq:not(.ability-btn)');
  for (let i = 0; i < buttons.length; i++) {
    const path = buttons[i].querySelector('svg.icon-svg path');
    if ((path?.getAttribute('d') || '').startsWith(BAG_PATH)) return buttons[i];
  }
  return null;
}

function percent(part: number): string {
  return Math.round(part * 10000) / 100 + '%';
}

// The part of one segment of the weight bar, from `from` to `to` (parts of the weight limit), that the fill covers.
function segmentFill(fill: number, from: number, to: number): number {
  return to > from ? Math.min(1, Math.max(0, (fill - from) / (to - from))) : 0;
}

function child(parent: Element, cls: string): HTMLElement {
  const doc = parent.ownerDocument;
  const el = doc.createElement('span');
  el.className = cls;
  parent.appendChild(el);
  return el;
}

// Gives the missing parts. A hidden bag button (the game shows it only from some point of a save) is not missing.
export function applyWeight(doc: Document, state: CoreState, data: PageData | null): string[] {
  const weight = data && data.switches.weightReadout ? data.weight : null;
  if (!weight) {
    doc.querySelectorAll('.stui-weight').forEach((el) => el.remove());
    return [];
  }
  const button = findBagButton(doc);
  if (!button) return state.bagActive ? ['bag button'] : [];

  let readout = button.querySelector<HTMLElement>(':scope > .stui-weight');
  if (!readout) {
    readout = doc.createElement('span');
    child(readout, 'stui-weight-num');
    const bar = child(readout, 'stui-weight-bar');
    child(child(bar, 'stui-weight-seg'), 'stui-weight-fill');
    child(child(bar, 'stui-weight-seg'), 'stui-weight-fill');
  }
  const cls = 'stui-weight stui-' + weight.tone;
  if (readout.className !== cls) readout.className = cls;
  const num = readout.querySelector('.stui-weight-num')!;
  if (num.textContent !== weight.text) num.textContent = weight.text;
  setStyle(readout.querySelector<HTMLElement>('.stui-weight-bar')!, '--stui-line', percent(weight.line));
  const fills = readout.querySelectorAll<HTMLElement>('.stui-weight-seg > .stui-weight-fill');
  setStyle(fills[0], '--stui-fill', percent(segmentFill(weight.fill, 0, weight.line)));
  setStyle(fills[1], '--stui-fill', percent(segmentFill(weight.fill, weight.line, 1)));
  if (readout.parentElement !== button) button.appendChild(readout);
  return [];
}
