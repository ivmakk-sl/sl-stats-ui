// The time stripe at the bottom of each buff badge with an end time.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { matchBadges } from '../../src/Web/page/stripes';
import { attrRows, badgesOf, buffData, check, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait, withBadges, type Win } from './harness';

const NOW = 100000;

const VIM = { name: 'Vim and Vigor', isGood: true, buffConfigId: 2 };
const CALM = { name: 'Calm in Crisis', isGood: true, buffConfigId: 3053 };
const COLD = { name: 'Slight Chill', isGood: false, buffConfigId: 700 };
const ENC = { name: 'Encumbered', isGood: false, buffConfigId: 1008 };

function data() {
  const d = emptyData();
  d.stats = plainStats();
  d.buffs = [
    buffData(2, 1, NOW + 7200, 10800),
    buffData(3053, 1, NOW + 100000, 10800),
    buffData(700, 2, NOW + 1000, 7200, false),
    buffData(700, 2, NOW + 3600, 7200, false),
    buffData(1008, 3, -1, -1, false),
  ];
  return d;
}

async function hud(t: any): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, withBadges(attrRows(), { 1: [VIM, CALM], 2: [COLD, COLD], 3: [ENC] }));
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  return { core, root };
}

function stripe(badge: Win): Win { return badge.querySelector(':scope > .stui-stripe'); }

function length(badge: Win): number {
  return parseFloat(stripe(badge).style.getPropertyValue('--stui-left'));
}

test('each badge matches its entry by the row and the occurrence of its key, also with one id twice in a row', async (t) => {
  const { core } = await hud(t);
  const d = data();

  const matches = matchBadges(core.document, core.eval('state'), d.buffs);

  const of = (el: Win) => matches.find((m) => m.el === el)?.entry;
  assert.equal(of(badgesOf(core, 1)[0]), d.buffs[0]);
  assert.equal(of(badgesOf(core, 1)[1]), d.buffs[1]);
  assert.equal(of(badgesOf(core, 2)[0]), d.buffs[2], 'the first Slight Chill is the first entry of 700');
  assert.equal(of(badgesOf(core, 2)[1]), d.buffs[3], 'the second Slight Chill is the second entry of 700');
  assert.equal(of(badgesOf(core, 3)[0]), d.buffs[4]);
});

test('a leaving badge is skipped, so the badges after it keep their entries', async (t) => {
  const { core } = await hud(t);
  const row = core.document.querySelectorAll('.status-row')[2].querySelector('.buff-container');
  const leaving = core.document.createElement('div');
  leaving.className = 'buff buff-neg bf-leave-active';
  row.insertBefore(leaving, row.firstChild);
  const d = data();

  const matches = matchBadges(core.document, core.eval('state'), d.buffs);

  assert.equal(matches.find((m) => m.el === leaving), undefined);
  assert.equal(matches.find((m) => m.el === badgesOf(core, 2)[0])?.entry, d.buffs[2]);
});

test('a badge with an end time has a stripe of the time left over the full time', async (t) => {
  const { core, root } = await hud(t);

  assert.equal(setData(root, data(), NOW), 'installed');

  const vim = badgesOf(core, 1)[0];
  assert.ok(stripe(vim), 'no stripe on Vim and Vigor');
  assert.ok(Math.abs(length(vim) - 66.67) < 0.01, 'two of three hours left is two thirds: ' + length(vim));
  assert.ok(stripe(vim).hasAttribute('data-no-sync'), 'the stripe has data-no-sync');
  assert.equal(length(badgesOf(core, 1)[1]), 100, 'a time left above the full time is a full stripe');
});

test('under 15% of the full time the stripe is amber', async (t) => {
  const { core, root } = await hud(t);

  setData(root, data(), NOW);

  const first = badgesOf(core, 2)[0];
  assert.ok(stripe(first).classList.contains('stui-warn'), '1000 of 7200 s is under 15%');
  assert.ok(!stripe(badgesOf(core, 2)[1]).classList.contains('stui-warn'), '3600 of 7200 s is not');
});

test('a badge with no end has no stripe', async (t) => {
  const { core, root } = await hud(t);

  setData(root, data(), NOW);

  assert.equal(stripe(badgesOf(core, 3)[0]), null);
});

test('with the time stripes switched off, no badge has a stripe', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const off = data();
  off.switches.timeStripes = false;
  setData(root, off, NOW);

  assert.equal(core.document.querySelectorAll('.stui-stripe').length, 0);
});

test('the step shrinks the stripes with style writes only, and adds or removes no node', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);
  const vim = badgesOf(core, 1)[0];
  const before = length(vim);

  let nodes = 0;
  const watch = new core.MutationObserver((records: Win[]) => {
    for (const r of records) nodes += r.addedNodes.length + r.removedNodes.length;
  });
  watch.observe(core.document.body, { childList: true, subtree: true });
  // A check of C# one game hour later.
  check(root, NOW + 3600);
  await wait(root, 400);
  watch.disconnect();

  assert.ok(length(vim) < before, 'the stripe did not shrink: ' + before + ' -> ' + length(vim));
  assert.equal(nodes, 0);
});

test('the stripe does not change the size of the badge', async (t) => {
  const { core, root } = await hud(t);
  setData(root, data(), NOW);

  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  const rule = css.match(/\.stui-stripe\s*\{[^}]*\}/);
  assert.ok(rule, 'no rule for the stripe');
  assert.match(rule[0], /position:\s*absolute/, 'the stripe is out of the flow of the badge');
  assert.doesNotMatch(css, /\.buff\s*\{[^}]*(padding|height|border)/, 'the mod sets no size of the badge');
});

test('a new badge that the game draws after the push gets its stripe', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, data(), NOW);

  await postAttr(core, withBadges(attrRows(), { 1: [VIM] }));
  await wait(core, 30);

  assert.ok(stripe(badgesOf(core, 1)[0]));
});
