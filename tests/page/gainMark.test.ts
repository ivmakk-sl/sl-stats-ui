// The gain mark after the name on each stat bar.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait, type Win } from './harness';

function nameBox(core: Win, row: number): Win {
  return core.document.querySelectorAll('.status-row')[row].querySelector('.attr-name');
}

function withMarks() {
  const data = emptyData();
  data.stats = plainStats();
  data.stats[1].mark = { text: 'x1.65', tone: 'good' };
  data.stats[3].mark = { text: 'x0.9', tone: 'bad' };
  return data;
}

test('a stat with a gain mark shows it after its name with its tone, and a stat with none shows nothing', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  assert.equal(setData(root, withMarks()), 'installed');

  const morale = nameBox(core, 1);
  const mark = morale.querySelector('.stui-mark');
  assert.ok(mark, 'no gain mark on Morale');
  assert.equal(mark.textContent, 'x1.65');
  assert.ok(mark.classList.contains('stui-good'));
  assert.equal(morale.lastElementChild, mark, 'the mark is after the name');
  assert.equal(morale.children[1].textContent, 'Morale', 'the name of the game stays');
  assert.ok(nameBox(core, 3).querySelector('.stui-mark.stui-bad'));
  assert.equal(nameBox(core, 0).querySelector('.stui-mark'), null);
  assert.equal(nameBox(core, 2).querySelector('.stui-mark'), null);
});

test('the mark stays after the game draws the row again with a new value', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, withMarks());

  const rows = attrRows();
  rows[1].value = 0.3;
  rows[1].barValue = '30/100';
  await postAttr(core, rows);
  await wait(core, 30);

  const morale = nameBox(core, 1);
  assert.equal(morale.querySelectorAll('.stui-mark').length, 1);
  assert.equal(morale.querySelector('.stui-mark').textContent, 'x1.65');
  assert.equal(core.document.querySelectorAll('.status-row')[1].querySelector('.progress-text').lastElementChild.textContent, '30/100');
});

test('a mark that goes away is removed, and a changed one is written in place', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, withMarks());
  const mark = nameBox(core, 1).querySelector('.stui-mark');

  const data = withMarks();
  data.stats[1].mark = { text: 'x3', tone: 'cap' };
  data.stats[3].mark = null;
  setData(root, data);

  assert.equal(nameBox(core, 1).querySelector('.stui-mark'), mark);
  assert.equal(mark.textContent, 'x3');
  assert.ok(mark.classList.contains('stui-cap'));
  assert.equal(nameBox(core, 3).querySelector('.stui-mark'), null);
});
