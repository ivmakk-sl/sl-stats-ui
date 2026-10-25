// The hatched part of each stat bar: the part of the fill that the current buffs and debuffs will take.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import type { BuffData, LossData } from '../../src/Web/page/types';
import { attrRows, check, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait, type Win } from './harness';

const HOUR = 3600;
const NOW = 100000;
const LIFE = 3;

// Stimulants in the Stamina row: Life -3 each hour, with the given hours left. The page clock moves some game
// seconds in a test, so a time left between two whole hours keeps the count of whole intervals.
function stimulants(hoursLeft: number): BuffData {
  const none = (): LossData => ({ each: 0, every: 0, atEnd: 0, ratio: 0 });
  const losses = [none(), none(), none(), none()];
  losses[LIFE] = { each: 3, every: HOUR, atEnd: 0, ratio: 0 };
  return { id: 24040, row: 2, good: true, layers: 1, maxLayers: 0, end: NOW + hoursLeft * HOUR, full: 86400, desc: '', suppressed: null, losses, lines: [] };
}

function data(hoursLeft = 12.5) {
  const d = emptyData();
  d.stats = plainStats();
  d.buffs = [stimulants(hoursLeft)];
  return d;
}

// Life 90/100 in the rows of the harness.
async function hud(t: any): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  return { core, root };
}

function partOf(core: Win, row: number): Win {
  return core.document.querySelectorAll('.status-row')[row].querySelector('.bar-mask > .stui-loss');
}

function pct(el: Win, prop: string): number {
  return parseFloat(el.style.getPropertyValue(prop));
}

test('the hatched part goes from the value after the loss to the value', async (t) => {
  const { core, root } = await hud(t);

  assert.equal(setData(root, data(), NOW), 'installed');

  const part = partOf(core, LIFE);
  assert.ok(part, 'no hatched part on Life');
  // 36 of Life 90/100: from 54% to 90%.
  assert.equal(pct(part, '--stui-from'), 54);
  assert.equal(pct(part, '--stui-width'), 36);
  assert.ok(part.hasAttribute('data-no-sync'), 'the part has data-no-sync');
  assert.ok(part.nextElementSibling.classList.contains('progress-text'), 'the text of the game draws over the part');
});

test('a push with no game time shows the hatched part at the first check with one', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data());
  assert.equal(partOf(core, LIFE), null);

  assert.match(check(root, NOW), /^ok/);

  assert.ok(partOf(core, LIFE), 'no hatched part after the first anchor');
});

test('a stat with no coming loss has no hatched part', async (t) => {
  const { core, root } = await hud(t);

  setData(root, data(), NOW);

  for (const row of [0, 1, 2]) assert.equal(partOf(core, row), null);
});

test('a hatched part that goes away is removed', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const d = data();
  d.buffs = [];
  setData(root, d, NOW);

  assert.equal(core.document.querySelector('.stui-loss'), null);
});

test('the step shrinks the part with the game time, with style writes only', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);
  const part = partOf(core, LIFE);
  const before = pct(part, '--stui-width');

  let nodes = 0;
  const watch = new core.MutationObserver((records: Win[]) => {
    for (const r of records) nodes += r.addedNodes.length + r.removedNodes.length;
  });
  watch.observe(core.document.body, { childList: true, subtree: true });
  // A check of C# two game hours later, so at least one interval passes.
  check(root, NOW + 7200);
  await wait(root, 400);
  watch.disconnect();

  assert.ok(pct(part, '--stui-width') < before, 'the part did not shrink: ' + before + ' -> ' + pct(part, '--stui-width'));
  assert.equal(nodes, 0);
});

test('the part moves with the value of the game, with no push', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const rows = attrRows();
  rows[LIFE].value = 0.5;
  rows[LIFE].barValue = '50/100';
  await postAttr(core, rows);
  await wait(core, 30);

  const part = partOf(core, LIFE);
  assert.equal(pct(part, '--stui-from'), 14);
  assert.equal(pct(part, '--stui-width'), 36);
});
