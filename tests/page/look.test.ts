// The opacity of the hatched part of a coming loss, the weight of the tooltip values, and the gain mark: the options
// that the user picked on the design board (A3, B2, and C3).
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, emptyData, loadCoreWindow, postAttr, rootWindow, runPageJs, setData, type Win } from './harness';

// The CSS of the mod in the frame, without its comments.
async function modCss(t: Parameters<typeof loadCoreWindow>[0]): Promise<string> {
  const core: Win = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());
  return core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
}

// The body of the one rule with exactly this selector.
function ruleOf(css: string, selector: string): string {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const m = css.match(new RegExp('(?:^|\\})\\s*' + escaped + '\\s*\\{([^}]*)\\}'));
  assert.ok(m, 'no rule ' + selector);
  return m[1];
}

test('the hatched part has white lines at 28% and a dark fill at 25%', async (t) => {
  const rule = ruleOf(await modCss(t), '.stui-loss');

  assert.match(rule, /rgb\(from var\(--stui-white\) r g b \/ 28%\) 0 1px/);
  assert.match(rule, /rgb\(from var\(--stui-black\) r g b \/ 25%\)/);
});

test('the gain mark is 9.5px regular, on the baseline of the name, and green also at the cap', async (t) => {
  const css = await modCss(t);

  assert.match(ruleOf(css, '.stui-mark'), /font-size:\s*9\.5px/);
  assert.match(ruleOf(css, '.stui-mark'), /font-weight:\s*400/);
  assert.match(ruleOf(css, '.stui-text .attr-name'), /align-items:\s*baseline/);
  assert.match(ruleOf(css, '.stui-mark.stui-good,\n.stui-mark.stui-cap'), /color:\s*var\(--stui-mark-good\)/);
});

test('the line values of both tooltips are semibold, and the title, the head value, and the time stay bold', async (t) => {
  const css = await modCss(t);

  assert.match(ruleOf(css, '.stui-tip-value'), /font-weight:\s*600/);
  assert.match(ruleOf(css, '.stui-stat-num'), /font-weight:\s*600/);
  for (const anchor of ['.stui-tip-title', '.stui-tip-time', '.stui-stat-name', '.stui-stat-value'])
    assert.match(ruleOf(css, anchor), /font-weight:\s*700/, anchor);
});
