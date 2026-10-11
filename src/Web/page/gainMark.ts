// The gain mark after the name on each stat bar: the gain multiplier of the stat when it is not x1. C# gives the
// text and the tone, so the page only draws them. The mark is the last child of the .attr-name box of the row; Vue
// patches only its own two children there, so the mark stays when the game draws the row again.
import type { CoreState, PageData } from './types';

export function applyGainMarks(doc: Document, state: CoreState, data: PageData | null): string[] {
  const attrs = state.attrList || [];
  const rows = doc.querySelectorAll('.status-row');
  for (let i = 0; i < rows.length && i < attrs.length; i++) {
    const box = rows[i].querySelector('.attr-name');
    if (!box) continue;
    const want = data && data.stats[i] ? data.stats[i].mark : null;
    let mark = box.querySelector<HTMLElement>(':scope > .stui-mark');
    if (!want) {
      if (mark) mark.remove();
      continue;
    }
    if (!mark) {
      mark = doc.createElement('span');
      box.appendChild(mark);
    }
    const cls = 'stui-mark stui-' + want.tone;
    if (mark.className !== cls) mark.className = cls;
    if (mark.textContent !== want.text) mark.textContent = want.text;
  }
  return [];
}
