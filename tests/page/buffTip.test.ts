// The tooltip of the mod on a buff badge, in place of the game's tooltip.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, badgesOf, buffData, check, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait, withBadges, type Win } from './harness';

const NOW = 100000;

const VIM = { name: 'Vim and Vigor', isGood: true, buffConfigId: 2 };
const COLD = { name: 'Slight Chill', isGood: false, buffConfigId: 700 };
const ENC = { name: 'Encumbered', isGood: false, buffConfigId: 1008 };

function data() {
  const d = emptyData();
  d.stats = plainStats();
  d.texts = { layers: 'Layers' };
  const cold = buffData(700, 2, NOW + 1000, 7200, false);
  cold.layers = 3;
  // The page clock moves some game seconds in a test: 30 s more keeps two game hours left, two thirds of the full time.
  d.buffs = [buffData(2, 1, NOW + 7230, 10845), cold, buffData(1008, 3, -1, -1, false)];
  return d;
}

async function hud(t: any, d = data()): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, withBadges(attrRows(), { 1: [VIM], 2: [COLD], 3: [ENC] }));
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, d, NOW);
  return { core, root };
}

function over(core: Win, el: Win): void {
  el.dispatchEvent(new core.MouseEvent('pointerover', { bubbles: true }));
}

function out(core: Win, el: Win): void {
  el.dispatchEvent(new core.MouseEvent('pointerout', { bubbles: true, relatedTarget: core.document.body }));
}

function tipOf(badge: Win): Win {
  return badge.querySelector(':scope > .stui-buff-tip');
}

function css(core: Win): string {
  return core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
}

test('with the tooltips on, the body has stui-tips and the game tooltip of a badge is hidden', async (t) => {
  const { core } = await hud(t);

  assert.ok(core.document.body.classList.contains('stui-tips'));
  assert.match(css(core), /\.stui-tips \.buff-tooltip[^{]*\{[^}]*display:\s*none/);
});

test('a badge shows the title in the buff tone, the time left with a time bar, and the text of the game', async (t) => {
  const { core } = await hud(t);
  const vim = badgesOf(core, 1)[0];

  over(core, vim);

  const tip = tipOf(vim);
  assert.ok(tip, 'no tooltip on Vim and Vigor');
  const title = tip.querySelector('.stui-tip-title');
  assert.equal(title.textContent, 'Vim and Vigor');
  assert.ok(title.classList.contains('stui-good'));
  // Two game hours left, in the game's format of the time left.
  assert.equal(tip.querySelector('.stui-tip-time').textContent, '02:00');
  assert.ok(Math.abs(parseFloat(tip.querySelector('.stui-tip-fill').style.getPropertyValue('--stui-left')) - 66.67) < 0.01);
  assert.equal(tip.querySelector('.stui-tip-desc').textContent, 'The text of buff 2.');
  assert.equal(tip.querySelector('.stui-tip-layers'), null, 'one layer shows no layer count');
});

test('the buff tooltip is left of the badge and grows upward, so a low badge keeps it inside the screen', async (t) => {
  const { core } = await hud(t);

  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  const rule = css.match(/\.stui-buff-tip\s*\{[^}]*\}/)[0];
  assert.match(rule, /right:\s*calc\(100% \+ 9px\)/);
  assert.match(rule, /bottom:\s*-4px/);
  assert.doesNotMatch(rule, /translateY/);
});

test('a debuff has its title in the debuff tone, and a layer count above one shows', async (t) => {
  const { core } = await hud(t);
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  const tip = tipOf(cold);
  assert.ok(tip.querySelector('.stui-tip-title').classList.contains('stui-bad'));
  const layers = tip.querySelector('.stui-tip-layers');
  assert.ok(layers, 'no layer count');
  assert.equal(layers.textContent, 'Layers3');
  assert.ok(layers.parentElement.classList.contains('stui-tip-lines'), 'the layer count is a row of the line grid');
  assert.equal(tip.lastElementChild.className, 'stui-tip-desc', 'the text of the game is last');
});

test('a mod text that C# did not send shows its name, as the C# code does', async (t) => {
  const d = data();
  d.texts = {};
  const { core } = await hud(t, d);
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  assert.equal(tipOf(cold).querySelector('.stui-tip-layers .stui-tip-label').textContent, 'layers');
});

test('a buff with no end shows no time line', async (t) => {
  const { core } = await hud(t);
  const enc = badgesOf(core, 3)[0];

  over(core, enc);

  const tip = tipOf(enc);
  assert.ok(tip);
  assert.equal(tip.querySelector('.stui-tip-time'), null);
  assert.equal(tip.querySelector('.stui-tip-bar'), null);
});

test('the time left counts down with the page clock, with no node added or removed', async (t) => {
  const { core, root } = await hud(t);
  const vim = badgesOf(core, 1)[0];
  over(core, vim);
  const time = tipOf(vim).querySelector('.stui-tip-time');

  let nodes = 0;
  const watch = new core.MutationObserver((records: Win[]) => {
    for (const r of records) nodes += r.addedNodes.length + r.removedNodes.length;
  });
  watch.observe(core.document.body, { childList: true, subtree: true });
  check(root, NOW + 3600);
  await wait(root, 400);
  watch.disconnect();

  assert.notEqual(time.textContent, '02:00');
  assert.equal(nodes, 0);
});

test('the tooltip goes when the pointer leaves the badge', async (t) => {
  const { core } = await hud(t);
  const vim = badgesOf(core, 1)[0];
  over(core, vim);

  out(core, vim);

  assert.equal(core.document.querySelector('.stui-buff-tip'), null);
});

test('with the tooltips off, the game tooltip shows and the mod shows none', async (t) => {
  const d = data();
  d.switches.extendedTooltips = false;
  const { core } = await hud(t, d);
  const vim = badgesOf(core, 1)[0];

  over(core, vim);

  assert.ok(!core.document.body.classList.contains('stui-tips'));
  assert.equal(tipOf(vim), null);
});

test('the effect lines show between the time line and the text of the game, and a buff with none shows none', async (t) => {
  const d = data();
  d.buffs[0].lines = [{ label: 'Morale gain', value: '+50%', bad: false }];
  const { core } = await hud(t, d);
  const vim = badgesOf(core, 1)[0];
  const cold = badgesOf(core, 2)[0];

  over(core, vim);
  const tip = tipOf(vim);
  const lines = tip.querySelectorAll('.stui-tip-line');
  assert.equal(lines.length, 1);
  assert.equal(lines[0].querySelector('.stui-tip-label').textContent, 'Morale gain');
  assert.equal(lines[0].querySelector('.stui-tip-value').textContent, '+50%');
  const order = [...tip.children].map((c: Win) => c.className.split(' ')[0]);
  assert.ok(order.indexOf('stui-tip-bar') < order.indexOf('stui-tip-lines'), 'the lines come after the time bar: ' + order);
  assert.ok(order.indexOf('stui-tip-lines') < order.indexOf('stui-tip-desc'), 'the lines come before the text of the game: ' + order);

  over(core, cold);
  assert.equal(tipOf(cold).querySelectorAll('.stui-tip-line').length, 0);
});

test('the layer count shows its cap when the buff has one', async (t) => {
  const d = data();
  d.buffs[1].maxLayers = 5;
  const { core } = await hud(t, d);
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  assert.equal(tipOf(cold).querySelector('.stui-tip-layers').textContent, 'Layers3/5');
});

test('the value of a penalty shows in the debuff tone, and another value keeps its color', async (t) => {
  const d = data();
  d.buffs[0].lines = [{ label: 'Stamina loss', value: '-50%', bad: false }, { label: 'Life at the end', value: '-20', bad: true }];
  const { core } = await hud(t, d);
  const vim = badgesOf(core, 1)[0];

  over(core, vim);

  const values = [...tipOf(vim).querySelectorAll('.stui-tip-line .stui-tip-value')];
  assert.deepEqual(values.map((v: Win) => v.classList.contains('stui-bad')), [false, true]);
  assert.match(css(core), /\.stui-tip-value\.stui-bad\s*\{[^}]*color:\s*var\(--stui-mark-bad\)/);
});

test('a suppressed debuff shows the suppression line in the gain tone under the time bar and no effect lines; another badge none', async (t) => {
  const d = data();
  d.buffs[1].suppressed = 'Suppressed by Analgesia';
  d.buffs[0].lines = [{ label: 'Morale gain', value: '+50%', bad: false }];
  const { core } = await hud(t, d);
  const cold = badgesOf(core, 2)[0];
  const vim = badgesOf(core, 1)[0];

  over(core, cold);
  const tip = tipOf(cold);
  const line = tip.querySelector('.stui-tip-suppressed');
  assert.ok(line, 'no suppression line');
  assert.equal(line.textContent, 'Suppressed by Analgesia');
  assert.equal(tip.querySelectorAll('.stui-tip-line').length, 0);
  const order = [...tip.children].map((c: Win) => c.className.split(' ')[0]);
  assert.ok(order.indexOf('stui-tip-bar') < order.indexOf('stui-tip-suppressed'), 'the line comes after the time bar: ' + order);
  assert.ok(order.indexOf('stui-tip-suppressed') < order.indexOf('stui-tip-desc'), 'the line comes before the text of the game: ' + order);
  assert.match(css(core), /\.stui-tip-suppressed\s*\{[^}]*color:\s*var\(--stui-mark-good\)/);
  out(core, cold);

  over(core, vim);
  assert.equal(tipOf(vim).querySelector('.stui-tip-suppressed'), null);
});

const PAINKILLERS = { id: 2408, name: 'Painkillers', icon: '../../Res/Consumable/UI_Item_Icon_Cons_S_1600401101.png', total: 2 };
const KANGFUXIN = { id: 2413, name: 'Kangfuxin Liquid', icon: '../../Res/Consumable/kangfuxin.png', total: 0 };
const MORPHINE = { id: 2916, name: 'Morphine Injection', icon: '', total: 0 };

function withCures() {
  const d = data();
  d.texts = { layers: 'Layers', curedBy: 'Cure with:', suppressedByList: 'Suppress with:' };
  d.cures = { '700': { cures: [PAINKILLERS, KANGFUXIN], suppressors: [MORPHINE] } };
  return d;
}

// The rows of the two blocks in order: a label as 'label:<text>', an item line as '<name>|<count>|<dim>'.
function cureRows(tip: Win): string[] {
  return [...tip.querySelectorAll('.stui-tip-cures > *')].map((r: Win) =>
    r.classList.contains('stui-tip-cure-label')
      ? 'label:' + r.textContent
      : r.querySelector('.stui-tip-cure-name').textContent + '|'
        + (r.querySelector('.stui-tip-cure-count')?.textContent ?? '') + '|'
        + (r.classList.contains('stui-tip-cure-none') ? 'dim' : ''));
}

test('a debuff with cures shows "Cure with:" and "Suppress with:" with their item lines after the text of the game and the layer count', async (t) => {
  const { core } = await hud(t, withCures());
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  const tip = tipOf(cold);
  assert.deepEqual(cureRows(tip), [
    'label:Cure with:', 'Painkillers|x2|', 'Kangfuxin Liquid||dim', 'label:Suppress with:', 'Morphine Injection||dim',
  ]);
  const order = [...tip.children].map((c: Win) => c.className.split(' ')[0]);
  assert.equal(order[order.length - 1], 'stui-tip-cures', 'the blocks come last: ' + order);
  assert.ok(order.indexOf('stui-tip-lines') < order.indexOf('stui-tip-cures'), 'the blocks come after the layer count: ' + order);
  const line = tip.querySelector('.stui-tip-cure');
  assert.equal(line.querySelector('.stui-tip-cure-icon img').getAttribute('src'), PAINKILLERS.icon);
  assert.equal(line.lastElementChild.className, 'stui-tip-cure-count', 'the count is at the right');
});

test('an item with no web icon keeps an empty icon place, so the lines stay aligned', async (t) => {
  const { core } = await hud(t, withCures());
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  const morphine = [...tipOf(cold).querySelectorAll('.stui-tip-cure')][2];
  const icon = morphine.querySelector('.stui-tip-cure-icon');
  assert.ok(icon, 'no icon place');
  assert.equal(icon.querySelector('img'), null);
});

test('a label shows only with an item line after it', async (t) => {
  const d = withCures();
  d.cures['700'] = { cures: [], suppressors: [MORPHINE] };
  const { core } = await hud(t, d);
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  assert.deepEqual(cureRows(tipOf(cold)), ['label:Suppress with:', 'Morphine Injection||dim']);
});

test('a buff, a debuff with no entry, and the switch of the cures off show no block', async (t) => {
  const d = withCures();
  d.cures['2'] = { cures: [PAINKILLERS], suppressors: [] };
  const { core } = await hud(t, d);
  const vim = badgesOf(core, 1)[0];
  const enc = badgesOf(core, 3)[0];

  over(core, vim);
  assert.equal(tipOf(vim).querySelector('.stui-tip-cures'), null, 'a buff shows a block');
  out(core, vim);
  over(core, enc);
  assert.equal(tipOf(enc).querySelector('.stui-tip-cures'), null, 'a debuff with no entry shows a block');
  out(core, enc);

  const off = withCures();
  off.switches.curesInTooltips = false;
  const second = await hud(t, off);
  const cold = badgesOf(second.core, 2)[0];
  over(second.core, cold);
  assert.ok(tipOf(cold), 'the extended tooltip still shows');
  assert.equal(tipOf(cold).querySelector('.stui-tip-cures'), null, 'the switch off shows a block');
});

test('a suppressed debuff shows the blocks at the same place, and the suppression line under the time bar', async (t) => {
  const d = withCures();
  d.buffs[1].suppressed = 'Suppressed by Analgesia';
  const { core } = await hud(t, d);
  const cold = badgesOf(core, 2)[0];

  over(core, cold);

  const tip = tipOf(cold);
  const order = [...tip.children].map((c: Win) => c.className.split(' ')[0]);
  assert.ok(order.indexOf('stui-tip-bar') < order.indexOf('stui-tip-suppressed'), 'the line comes after the time bar: ' + order);
  assert.equal(order[order.length - 1], 'stui-tip-cures', 'the blocks come last: ' + order);
  assert.equal(cureRows(tip)[0], 'label:Cure with:');
});

test('a new cures push while the tooltip is open changes the count in it', async (t) => {
  const { core, root } = await hud(t, withCures());
  const cold = badgesOf(core, 2)[0];
  over(core, cold);

  const next = withCures();
  next.cures['700'] = { cures: [{ ...KANGFUXIN, total: 1 }, { ...PAINKILLERS, total: 0 }], suppressors: [MORPHINE] };
  setData(root, next, NOW);

  const tip = tipOf(cold);
  assert.ok(tip, 'the tooltip closed');
  assert.deepEqual(cureRows(tip), [
    'label:Cure with:', 'Kangfuxin Liquid|x1|', 'Painkillers||dim', 'label:Suppress with:', 'Morphine Injection||dim',
  ]);
});

test('the label has the muted tone, a line with no item at home is dimmed, and the icon has a fixed place', async (t) => {
  const { core } = await hud(t, withCures());
  const text = css(core);

  assert.match(text, /\.stui-tip-cure-label\s*\{[^}]*color:\s*var\(--stui-/);
  assert.match(text, /\.stui-tip-cure-none\s*\{[^}]*opacity:/);
  assert.match(text, /\.stui-tip-cure-icon\s*\{[^}]*width:/);
});
