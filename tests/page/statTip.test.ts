// The tooltip of the mod on a stat bar, in place of the game's tooltip: left of the bar.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import type { LossData } from '../../src/Web/page/types';
import { attrRows, check, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait, withBadges, type Win } from './harness';

const HOUR = 3600;
const NOW = 100000;
const SATIETY = 0, MORALE = 1, LIFE = 3;

const STIM = { name: 'Stimulants', isGood: true, buffConfigId: 24040 };
const ADRE = { name: 'Adrenaline', isGood: true, buffConfigId: 24030 };

const TEXTS = {
  gain: 'Gain', naturalGain: 'Natural gain', loss: 'Loss', gainOnEating: 'Gain on eating', comingLoss: 'Coming loss',
  lossEachHour: '{amount} each hour for {left}', lossEvery: '{amount} every {hours} h for {left}', lossAtEnd: 'in {left}',
  wellFedLine: 'Well-Fed from {start}, ends below {end}',
};

function none(): LossData {
  return { each: 0, every: 0, atEnd: 0, ratio: 0 };
}

function data() {
  const d = emptyData();
  d.texts = { ...TEXTS };
  d.stats = plainStats();
  d.stats[MORALE].mark = { text: 'x1.65', tone: 'good' };
  d.stats[MORALE].gain = 0.65;
  d.stats[MORALE].loss = -0.5;
  d.stats[MORALE].lossRest = -0.5;
  d.stats[SATIETY].food = [{ name: 'Meat', ratio: 0.5 }];
  d.stats[SATIETY].threshold = { start: 80, end: 60 };
  const stim = [none(), none(), none(), none()];
  stim[LIFE] = { each: 3, every: HOUR, atEnd: 0, ratio: 0 };
  const adre = [none(), none(), none(), none()];
  adre[LIFE] = { each: 0, every: 0, atEnd: 20, ratio: 0 };
  // Some minutes more than the whole hours, as the page clock moves some game seconds in a test.
  d.buffs = [
    { id: 24040, row: 2, good: true, layers: 1, maxLayers: 0, end: NOW + 12 * HOUR + 600, full: 86400, desc: '', losses: stim, lines: [] },
    { id: 24030, row: 2, good: true, layers: 1, maxLayers: 0, end: NOW + 14 * HOUR + 600, full: 86400, desc: '', losses: adre, lines: [] },
  ];
  return d;
}

async function hud(t: any, d = data()): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, withBadges(attrRows(), { 2: [STIM, ADRE] }));
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, d, NOW);
  return { core, root };
}

function barOf(core: Win, row: number): Win {
  return core.document.querySelectorAll('.status-row')[row].querySelector('.progress-bar-wrapper');
}

function over(core: Win, el: Win): void {
  el.dispatchEvent(new core.MouseEvent('pointerover', { bubbles: true }));
}

function tip(core: Win): Win {
  return core.document.querySelector('.stui-stat-tip');
}

// Each row of the grid of the tooltip as [label, value]. A section head has an empty value.
function rows(core: Win): string[][] {
  return [...tip(core).querySelectorAll('.stui-stat-grid > .stui-stat-row')].map((r: Win) => [
    r.querySelector('.stui-stat-label').textContent, r.querySelector('.stui-stat-num').textContent,
  ]);
}

function css(core: Win): string {
  return core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
}

test('a stat bar shows one tooltip in its bar, at the place of the game tooltip, growing upward', async (t) => {
  const { core } = await hud(t);

  over(core, barOf(core, MORALE));

  const tips = core.document.querySelectorAll('.stui-stat-tip');
  assert.equal(tips.length, 1);
  assert.equal(tips[0].parentElement, barOf(core, MORALE));
  const rule = css(core).match(/\.stui-stat-tip\s*\{[^}]*\}/)![0];
  assert.match(rule, /position:\s*absolute/);
  assert.match(rule, /right:\s*108%/, 'left of the bar, as the game tooltip');
  assert.match(rule, /bottom:\s*0/, 'the tooltip grows upward from the bar');
});

test('both tooltips have an arrow to their badge or bar with the border of the tooltip', async (t) => {
  const { core } = await hud(t);

  for (const sel of ['stui-stat-tip', 'stui-buff-tip']) {
    const rule = css(core).match(new RegExp('\\.' + sel + '::before\\s*\\{[^}]*\\}'));
    assert.ok(rule, 'no arrow on .' + sel);
    assert.match(rule[0], /rotate\(45deg\)/);
    assert.match(rule[0], /border-top:\s*1px solid/);
    assert.match(rule[0], /border-right:\s*1px solid/);
  }
});

test('the tooltip shows the name, the value, each multiplier that is not x1 as a plain percent, and the text of the game last', async (t) => {
  const { core } = await hud(t);

  over(core, barOf(core, MORALE));

  assert.equal(tip(core).querySelector('.stui-stat-name').textContent, 'Morale');
  assert.equal(tip(core).querySelector('.stui-stat-value').textContent, '60/100');
  assert.deepEqual(rows(core), [['Gain', '+65%'], ['Loss', '-50%']]);
  assert.equal(tip(core).querySelector('.stui-stat-num.stui-good, .stui-stat-num.stui-bad'), null, 'the percents have no tone');
  assert.equal(tip(core).lastElementChild.className, 'stui-stat-desc');
  assert.equal(tip(core).lastElementChild.textContent, 'The text of Morale.');
});

test('at the cap the bar mark shows x3, and the tooltip names the cap in the cap tone', async (t) => {
  const d = data();
  d.stats[MORALE].mark = { text: 'x3', tone: 'cap' };
  d.stats[MORALE].gain = 2;
  const { core } = await hud(t, d);

  over(core, barOf(core, MORALE));

  assert.equal(core.document.querySelectorAll('.status-row')[MORALE].querySelector('.stui-mark').textContent, 'x3');
  assert.deepEqual(rows(core)[0], ['Gain', '+200% MAX']);
  assert.ok(tip(core).querySelector('.stui-stat-num').classList.contains('stui-cap'));
});

test('the text of the game is small and grey in both tooltips', async (t) => {
  const { core } = await hud(t);

  for (const sel of ['stui-stat-desc', 'stui-tip-desc']) {
    const rule = css(core).match(new RegExp('\\.' + sel + '\\s*\\{[^}]*\\}'))![0];
    assert.match(rule, /font-size:\s*10\.5px/);
    assert.match(rule, /color:\s*var\(--stui-tip-desc\)/);
  }
  assert.match(css(core), /--stui-tip-desc:\s*#a0a0a0/);
});

test('the Satiety bar shows the food types in their own section of the one grid, and the note of the threshold line', async (t) => {
  const { core } = await hud(t);

  over(core, barOf(core, SATIETY));

  assert.equal(tip(core).querySelectorAll('.stui-stat-grid').length, 1, 'one grid, so each value stands in one column');
  assert.deepEqual(rows(core), [['Gain on eating', ''], ['Meat', '+50%']]);
  const note = tip(core).querySelector('.stui-stat-note');
  assert.equal(note.textContent, 'Well-Fed from 80, ends below 60');
  assert.equal(note.parentElement, tip(core), 'the note is out of the grid, so it wraps in the box');
  assert.equal(tip(core).lastElementChild.className, 'stui-stat-desc');
});

test('the Life bar lists each coming loss with its timing and its value in the tone of a loss, under a head with no sum', async (t) => {
  const { core } = await hud(t);

  over(core, barOf(core, LIFE));

  const all = rows(core);
  assert.deepEqual(all[0], ['Coming loss', '']);
  assert.equal(all.length, 3);
  const parts = [...tip(core).querySelectorAll('.stui-stat-grid > .stui-stat-row')].slice(1).map((r: Win) => [
    r.querySelector('.stui-stat-source').textContent, r.querySelector('.stui-stat-when').textContent, r.querySelector('.stui-stat-num').textContent,
  ]);
  assert.equal(parts[0][0], 'Stimulants');
  assert.match(parts[0][1], /^-3 each hour for 12:\d\d$/);
  assert.equal(parts[0][2], '-36');
  assert.equal(parts[1][0], 'Adrenaline');
  assert.match(parts[1][1], /^in 1[34]:\d\d$/, 'the value of a loss at the end shows only in the value column');
  assert.equal(parts[1][2], '-20');
  const nums = [...tip(core).querySelectorAll('.stui-stat-grid > .stui-stat-row')].slice(1).map((r: Win) => r.querySelector('.stui-stat-num'));
  assert.ok(nums.every((n: Win) => n.classList.contains('stui-bad')), 'a value of the coming loss has no tone of a loss');
  assert.match(css(core), /\.stui-stat-num\.stui-bad\s*\{[^}]*color:\s*var\(--stui-mark-bad\)/);
});

test('when one coming loss ends, the step still moves the rows of the others', async (t) => {
  const { core, root } = await hud(t);
  over(core, barOf(core, LIFE));

  // Past the end of Stimulants, before the end of Adrenaline.
  check(root, NOW + 13 * HOUR + 600);
  await wait(root, 400);

  const when = [...tip(core).querySelectorAll('.stui-stat-when')].map((n: Win) => n.textContent);
  assert.equal(when[1], 'in 01:00');
});

test('the tooltip goes when the pointer leaves the bar', async (t) => {
  const { core } = await hud(t);
  const bar = barOf(core, MORALE);
  over(core, bar);

  bar.dispatchEvent(new core.MouseEvent('pointerout', { bubbles: true, relatedTarget: core.document.body }));

  assert.equal(tip(core), null);
});

test('the game tooltip of a bar is hidden while the tooltips are on, and with them off the mod shows none', async (t) => {
  const on = await hud(t);
  const css = on.core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  assert.match(css, /\.stui-tips \.status-tooltip[^{]*\{[^}]*display:\s*none/);

  const d = data();
  d.switches.extendedTooltips = false;
  const { core } = await hud(t, d);
  over(core, barOf(core, MORALE));

  assert.ok(!core.document.body.classList.contains('stui-tips'));
  assert.equal(tip(core), null);
});

test('in both tooltips each value stands at the right edge of a narrow box', async (t) => {
  const { core } = await hud(t);

  for (const [box, grid, width] of [['stui-buff-tip', 'stui-tip-lines', '180px'], ['stui-stat-tip', 'stui-stat-grid', '200px']]) {
    const boxRule = css(core).match(new RegExp('\\.' + box + '\\s*\\{[^}]*\\}'))![0];
    assert.match(boxRule, new RegExp('max-width:\\s*' + width));
    const gridRule = css(core).match(new RegExp('\\.' + grid + '\\s*\\{[^}]*\\}'))![0];
    assert.match(gridRule, /grid-template-columns:\s*1fr max-content/);
  }
});

test('with the game text of the stat tooltip switched off, the stat tooltip shows no game text', async (t) => {
  const d = data();
  d.switches.statTooltipGameText = false;
  const { core } = await hud(t, d);

  over(core, barOf(core, MORALE));

  assert.equal(tip(core).querySelector('.stui-stat-desc'), null);
  assert.deepEqual(rows(core), [['Gain', '+65%'], ['Loss', '-50%']]);
});

test('an open tooltip shows the new value of the bar and the new data of a push, with the pointer still on the bar', async (t) => {
  const { core, root } = await hud(t);
  over(core, barOf(core, MORALE));

  const changed = withBadges(attrRows(), { 2: [STIM, ADRE] });
  changed[MORALE].value = 0.5;
  changed[MORALE].barValue = '50/100';
  await postAttr(core, changed);
  await wait(core, 50);
  assert.equal(tip(core).querySelector('.stui-stat-value').textContent, '50/100');

  const d = data();
  d.stats[MORALE].naturalGain = 0.5;
  setData(root, d, NOW);
  assert.deepEqual(rows(core), [['Gain', '+65%'], ['Natural gain', '+50%'], ['Loss', '-50%']]);
  assert.equal(core.document.querySelectorAll('.stui-stat-tip').length, 1);
});

test('an apply pass with nothing new keeps the open tooltip node', async (t) => {
  const { core, root } = await hud(t);
  over(core, barOf(core, MORALE));
  const before = tip(core);

  setData(root, data(), NOW);

  assert.equal(tip(core), before);
});

test('the name of the stat shows in the gold of the game tooltip', async (t) => {
  const { core } = await hud(t);

  assert.match(css(core), /\.stui-stat-name\s*\{[^}]*color:\s*var\(--stui-stat-title\)/);
  assert.match(css(core), /--stui-stat-title:\s*#ffc107/i);
});
