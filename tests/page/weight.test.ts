// The weight readout on the bag button of the HUD: the number and the weight bar in two segments split at the
// Encumbered line.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import type { PageData } from '../../src/Web/page/types';
import { attrRows, check, emptyData, loadCoreWindow, postAttr, postButtons, rootWindow, runPageJs, setData, type Win } from './harness';

const BAG_PATH = 'M20 6h-4V4';

function bagButton(core: Win): Win {
  return [...core.document.querySelectorAll('.vertical-tools .icon-sq')]
    .find((b: Win) => (b.querySelector('svg.icon-svg path')?.getAttribute('d') || '').startsWith(BAG_PATH));
}

function withWeight(): PageData {
  const data = emptyData();
  data.weight = { text: '8.6', fill: 0.72, line: 0.8, tone: 'near' };
  return data;
}

async function hud(t: any): Promise<{ core: Win; root: Win }> {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  await postButtons(core);
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  return { core, root };
}

test('the bag button shows the carried weight and the weight bar with its fill, its line, and its tone', async (t) => {
  const { core, root } = await hud(t);

  assert.equal(setData(root, withWeight()), 'installed');

  const button = bagButton(core);
  assert.ok(button, 'the game page has no bag button');
  const readout = button.querySelector('.stui-weight');
  assert.ok(readout, 'no weight readout in the bag button');
  assert.ok(readout.classList.contains('stui-near'));
  assert.equal(readout.querySelector('.stui-weight-num').textContent, '8.6');
  assert.deepEqual(fillsOf(readout), ['90%', '0%'], 'the fill below the line is 0.72 / 0.8 of the first segment');
  assert.equal(readout.querySelector('.stui-weight-bar').style.getPropertyValue('--stui-line'), '80%');
  assert.equal(readout.querySelector('.stui-weight-line'), null, 'the white line is gone: the gap of the segments marks it');
  assert.ok(button.querySelector('svg.icon-svg'), 'the icon of the game stays');
  assert.equal(core.document.querySelectorAll('.stui-weight').length, 1, 'only the bag button gets the readout');
});

// The fill of each segment of the weight bar: below the Encumbered line, and past it.
function fillsOf(readout: Win): string[] {
  return [...readout.querySelectorAll('.stui-weight-seg > .stui-weight-fill')].map((f: Win) => f.style.getPropertyValue('--stui-fill'));
}

test('a weight past the Encumbered line fills the first segment and a part of the second', async (t) => {
  const { core, root } = await hud(t);
  const data = withWeight();
  data.weight = { text: '11.2', fill: 0.9, line: 0.8, tone: 'over' };

  setData(root, data);

  assert.deepEqual(fillsOf(bagButton(core).querySelector('.stui-weight')), ['100%', '50%']);
});

test('a weight at the Encumbered line fills only the first segment, and a line at the weight limit leaves the second empty', async (t) => {
  const { core, root } = await hud(t);
  const data = withWeight();
  data.weight = { text: '9.6', fill: 0.8, line: 0.8, tone: 'near' };
  setData(root, data);
  assert.deepEqual(fillsOf(bagButton(core).querySelector('.stui-weight')), ['100%', '0%']);

  data.weight = { text: '13.0', fill: 1, line: 1, tone: 'near' };
  setData(root, data);
  assert.deepEqual(fillsOf(bagButton(core).querySelector('.stui-weight')), ['100%', '0%']);

  data.weight = { text: '0.0', fill: 0, line: 0, tone: 'good' };
  setData(root, data);
  assert.deepEqual(fillsOf(bagButton(core).querySelector('.stui-weight')), ['0%', '0%']);
});

test('the bar sits inset with round ends, the number is regular, and the icon of the bag button is smaller and higher', async (t) => {
  const { core, root } = await hud(t);
  setData(root, withWeight());

  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  const rule = (selector: string) => css.match(new RegExp('(?:^|\\})\\s*' + selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s*\\{([^}]*)\\}'))?.[1] || '';
  assert.match(rule('.stui-weight-bar'), /left:\s*7px/);
  assert.match(rule('.stui-weight-bar'), /bottom:\s*5px/);
  assert.match(rule('.stui-weight-bar'), /height:\s*2px/);
  assert.match(rule('.stui-weight-seg'), /border-radius:\s*1px/);
  assert.match(rule('.stui-weight-seg'), /rgb\(from var\(--stui-white\) r g b \/ 12%\)/);
  assert.match(rule('.stui-weight-num'), /font-size:\s*8\.5px/);
  assert.match(rule('.stui-weight-num'), /font-weight:\s*400/);
  assert.match(rule('.stui-weight-num'), /bottom:\s*10px/);
  const icon = rule('.icon-sq:has(> .stui-weight) > .icon-svg');
  assert.match(icon, /width:\s*16px/);
  assert.match(icon, /transform:\s*translateY\(-6px\)/);
});

test('a new weight updates the readout in place', async (t) => {
  const { core, root } = await hud(t);
  setData(root, withWeight());
  const readout = bagButton(core).querySelector('.stui-weight');

  const data = withWeight();
  data.weight = { text: '10.4', fill: 0.8667, line: 0.8, tone: 'over' };
  setData(root, data);

  assert.equal(bagButton(core).querySelector('.stui-weight'), readout, 'the readout was built again');
  assert.equal(readout.querySelector('.stui-weight-num').textContent, '10.4');
  assert.deepEqual(fillsOf(readout), ['100%', '33.35%']);
  assert.ok(readout.classList.contains('stui-over'));
  assert.ok(!readout.classList.contains('stui-near'));
});

test('the readout takes no pointer, so the click and the entry tip of the game stay', async (t) => {
  const { core, root } = await hud(t);
  setData(root, withWeight());

  const css = core.document.getElementById('statsui-style').textContent.replace(/\/\*[\s\S]*?\*\//g, '');
  const rule = css.match(/\.stui-weight\s*\{[^}]*\}/);
  assert.ok(rule, 'no rule for the readout');
  assert.match(rule[0], /pointer-events:\s*none/);
  assert.ok(bagButton(core).querySelector('.stui-weight-num').closest('.stui-weight'), 'the number is inside the readout');
  assert.ok(bagButton(core).querySelector('.stui-weight-bar').closest('.stui-weight'), 'the bar is inside the readout');
});

test('with the weight readout switched off, the bag button has no node of the mod', async (t) => {
  const { core, root } = await hud(t);
  setData(root, withWeight());

  const data = withWeight();
  data.switches.weightReadout = false;
  setData(root, data);

  assert.equal(bagButton(core).querySelector('[class*="stui-"]'), null);
});

test('with no weight read yet, the bag button has no node of the mod', async (t) => {
  const { core, root } = await hud(t);

  setData(root, emptyData());

  assert.equal(bagButton(core).querySelector('[class*="stui-"]'), null);
});

test('a bag button that the game draws after the push gets the readout', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, withWeight());

  await postButtons(core);

  assert.ok(bagButton(core).querySelector('.stui-weight'));
});

test('a bag button that the mod cannot find is reported, and the other parts still install', async (t) => {
  const { core, root } = await hud(t);
  bagButton(core).querySelector('svg.icon-svg path').setAttribute('d', 'M0 0');

  assert.equal(setData(root, withWeight()), 'installed; missing: bag button');

  assert.match(check(root), /^ok \d+ \d+ \d+; missing: bag button$/);
  assert.equal(core.document.querySelectorAll('.stui-weight').length, 0);
  const life = core.document.querySelectorAll('.status-row')[3].querySelector('.attr-name > span:first-child');
  assert.equal(life.textContent, '❤', 'the text style still installs');
});

test('a hidden bag button (no bag yet) is not a missing part', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  assert.equal(setData(root, withWeight()), 'installed');
  assert.match(check(root), /^ok \d+ \d+ \d+$/);
});
