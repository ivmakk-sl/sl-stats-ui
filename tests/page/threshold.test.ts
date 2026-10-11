// The threshold line on the Satiety bar: the Satiety value where Well-Fed starts.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, type Win } from './harness';

function barOf(core: Win, row: number): Win {
  return core.document.querySelectorAll('.status-row')[row].querySelector('.bar-mask');
}

function withThreshold() {
  const data = emptyData();
  data.stats = plainStats();
  data.stats[0].threshold = { start: 80, end: 60 };
  return data;
}

test('the Satiety bar shows one line at the start of Well-Fed over the maximum of the game', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  assert.equal(setData(root, withThreshold()), 'installed');

  const lines = barOf(core, 0).querySelectorAll('.stui-threshold');
  assert.equal(lines.length, 1);
  // 80 of the maximum 135 in barValue "76/135".
  assert.equal(lines[0].style.getPropertyValue('--stui-at'), '59.26%');
  assert.ok(lines[0].nextElementSibling.classList.contains('progress-text'), 'the text of the game draws over the line');
  for (const row of [1, 2, 3]) assert.equal(barOf(core, row).querySelector('.stui-threshold'), null);
});

test('the line moves when the maximum of Satiety changes', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, withThreshold());

  const rows = attrRows();
  rows[0].barValue = '76/100';
  await postAttr(core, rows);

  const lines = barOf(core, 0).querySelectorAll('.stui-threshold');
  assert.equal(lines.length, 1);
  assert.equal(lines[0].style.getPropertyValue('--stui-at'), '80%');
});

test('no line while the maximum of Satiety is below the start of Well-Fed', async (t) => {
  const core = await loadCoreWindow(t);
  const rows = attrRows();
  rows[0].barValue = '50/70';
  await postAttr(core, rows);
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  setData(root, withThreshold());

  assert.equal(barOf(core, 0).querySelector('.stui-threshold'), null);
});

test('no line without a threshold, and a line that goes away is removed', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, withThreshold());
  assert.ok(barOf(core, 0).querySelector('.stui-threshold'));

  const data = withThreshold();
  data.stats[0].threshold = null;
  setData(root, data);

  assert.equal(core.document.querySelector('.stui-threshold'), null);
});
