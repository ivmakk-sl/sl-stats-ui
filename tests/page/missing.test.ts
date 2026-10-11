// The safe failure: a game part that a game update removes turns off only the feature that needs it, and check()
// names the part.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { findBagButton } from '../../src/Web/page/weight';
import { attrRows, buffData, check, emptyData, loadCoreWindow, plainStats, postAttr, postButtons, rootWindow, runPageJs, setData, wait, withBadges, type Win } from './harness';

const NOW = 100000;
const VIM = { name: 'Vim and Vigor', isGood: true, buffConfigId: 2 };

function data() {
  const d = emptyData();
  d.stats = plainStats();
  d.stats[1].mark = { text: 'x1.65', tone: 'good' };
  d.buffs = [buffData(2, 1, NOW + 7200, 10800)];
  d.weight = { text: '7.7', fill: 0.58, line: 0.8, tone: 'good' };
  return d;
}

async function hud(t: any): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, withBadges(attrRows(), { 1: [VIM] }));
  await postButtons(core);
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  return { core, root };
}

// Removes each node of a selector, then pushes again, as C# does on the next change.
async function removeAndPush(core: Win, root: Win, selector: string): Promise<string> {
  core.document.querySelectorAll(selector).forEach((el: Win) => el.remove());
  await wait(core, 30);
  setData(root, data(), NOW);
  return check(root, NOW);
}

function marks(core: Win): number {
  return core.document.querySelectorAll('.stui-mark').length;
}

test('with each part there, check() names no missing part', async (t) => {
  const { root } = await hud(t);

  setData(root, data(), NOW);

  assert.match(check(root, NOW), /^ok \d+ \d+ \d+$/);
});

test('without the bag button, check() names it, and the other features work', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);
  findBagButton(core.document)!.remove();
  await wait(core, 30);

  setData(root, data(), NOW);

  assert.match(check(root, NOW), /; missing: .*bag button/);
  assert.equal(core.document.querySelector('.stui-weight'), null);
  assert.equal(marks(core), 1);
  assert.equal(core.document.querySelectorAll('.stui-stripe').length, 1);
});

test('without the buff containers, check() names them, and the gain mark and the weight readout work', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const answer = await removeAndPush(core, root, '.buff-container');

  assert.match(answer, /; missing: .*\.buff-container/);
  assert.equal(core.document.querySelectorAll('.stui-stripe').length, 0);
  assert.equal(marks(core), 1);
  assert.ok(core.document.querySelector('.stui-weight'));
});

test('without the game tooltip of the bars, check() names it while the tooltips are on, and the other features work', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const answer = await removeAndPush(core, root, '.status-tooltip');

  assert.match(answer, /; missing: .*\.status-tooltip/);
  assert.equal(marks(core), 1);
  assert.ok(core.document.querySelector('.stui-weight'));
  core.document.querySelector('.progress-bar-wrapper').dispatchEvent(new core.MouseEvent('pointerover', { bubbles: true }));
  assert.equal(core.document.querySelector('.stui-stat-tip'), null, 'the stat tooltip stays off');
});

test('without the stat rows, check() names them, and the weight readout works', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const answer = await removeAndPush(core, root, '.status-row');

  assert.match(answer, /; missing: .*\.status-row/);
  assert.ok(core.document.querySelector('.stui-weight'));
});

test('check() names each missing part once', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const answer = await removeAndPush(core, root, '.status-row');

  const parts = answer.split('; missing: ')[1].split(', ');
  assert.equal(new Set(parts).size, parts.length, 'a part shows twice: ' + answer);
});
