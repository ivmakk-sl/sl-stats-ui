// The text style of the stat bars and the buff badges, and the heart of the game for Life.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, emptyData, loadCoreWindow, postAttr, rootWindow, runPageJs, setData, wait } from './harness';

function iconOf(core: any, row: number): string {
  const rows = core.document.querySelectorAll('.status-row');
  return rows[row].querySelector('.attr-name > span:first-child').textContent;
}

test('the Life row shows the heart of the game and not the shield of the HUD page', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  assert.equal(iconOf(core, 3), '🛡️', 'the game page itself shows the shield');
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  setData(root, emptyData());

  assert.equal(iconOf(core, 3), '❤');
  assert.equal(iconOf(core, 0), '🍖', 'the other rows keep their icons');
});

test('the heart stays after the game draws the Life row again', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());

  const rows = attrRows();
  rows[3].value = 0.5;
  rows[3].barValue = '50/100';
  await postAttr(core, rows);
  await wait(core, 30);

  assert.equal(iconOf(core, 3), '❤');
});

test('the heart comes on a stat panel that the game draws after the push', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());

  await postAttr(core, attrRows());
  await wait(core, 30);

  assert.equal(iconOf(core, 3), '❤');
});

test('the frame body has the class of the mod text style, and the rule selects the bar text and the badges', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  setData(root, emptyData());

  assert.ok(core.document.body.classList.contains('stui-text'));
  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  const rule = css.match(/([^{}]*)\{[^}]*font-family:\s*'Segoe UI', 'Microsoft YaHei', sans-serif[^}]*\}/);
  assert.ok(rule, 'no rule with the font of the mod');
  const selectors = rule[1].split(',').map((s: string) => s.trim());
  assert.ok(core.document.querySelector('.progress-text').matches(selectors[0]), 'the rule does not select the bar text');
  assert.ok(selectors.includes('.stui-text .buff'), 'the rule does not select the buff badges');
  assert.doesNotMatch(rule[0], /font-size/, 'the mod sets no font size, so HUD Scale still scales the text');
  assert.match(rule[0], /font-weight:\s*400/);
});

test('the bar text has a tight dark halo and the badges have no text shadow', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  setData(root, emptyData());

  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  // The game gives the bar text a wide soft shadow, which the mod replaces with a tight halo; the badges of the game
  // have none, and the mod adds none.
  assert.match(
    css,
    /\.stui-text \.progress-text\s*\{[^}]*text-shadow:\s*0 0 2px rgb\(from var\(--stui-black\) r g b \/ 90%\),\s*0 1px 1px rgb\(from var\(--stui-black\) r g b \/ 80%\)/,
  );
  // text-shadow inherits, so a shadow in any other rule could reach the badges.
  assert.equal(css.match(/text-shadow\s*:/g)?.length, 1, 'the mod sets a text shadow outside the bar text');
});

test('a second identical stat message writes nothing to the page', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());
  await wait(core, 30);

  let writes = 0;
  const watch = new core.MutationObserver((records: unknown[]) => { writes += records.length; });
  watch.observe(core.document.body, { childList: true, subtree: true, characterData: true, attributes: true });
  await postAttr(core, attrRows());
  await wait(core, 30);
  watch.disconnect();

  assert.equal(writes, 0);
});
