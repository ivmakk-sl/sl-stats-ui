// The text style of the stat bars and the buff badges (a class on the body, and the rule in page.css), and the
// heart of the game for Life. The HUD page swaps the game's heart for a shield in its handler of
// WebUI_CoreUI1_AttrMsg; the script writes the game's own attrIcon back into the icon of the Life row.
import { partsMissing } from './core';
import type { CoreState } from './types';

// The row of Life in state.attrList (Satiety, Morale, Stamina, Life).
const LIFE_ROW = 3;

// Gives the missing parts, or none while the game has no stat row yet.
export function applyTextStyle(doc: Document, state: CoreState): string[] {
  if (!doc.body.classList.contains('stui-text')) doc.body.classList.add('stui-text');
  const attrs = state.attrList || [];
  if (attrs.length <= LIFE_ROW) return [];
  const missing = partsMissing(doc, 'textStyle');
  if (missing.length) return missing;
  const row = doc.querySelectorAll('.status-row')[LIFE_ROW];
  const icon = row && row.querySelector('.attr-name > span:first-child');
  const want = attrs[LIFE_ROW].attrIcon;
  if (!icon || !want) return [];
  // The text node of Vue keeps its place, so Vue still owns it.
  const node = icon.firstChild;
  if (node && node.nodeType === 3) { if (node.nodeValue !== want) node.nodeValue = want; }
  else if (icon.textContent !== want) icon.textContent = want;
  return [];
}
