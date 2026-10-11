// Runs the page script bundle against the game's own CoreUI1.html, so a game update that renames or removes a
// page part that the script needs shows here and not only in the game.
import assert from 'node:assert/strict';
import { test } from 'vitest';
import { attrRows, check, emptyData, loadCoreWindow, plainStats, postAttr, rootWindow, runPageJs, setData, wait } from './harness';

test('a second full send keeps one interface with setData and check', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);

  runPageJs(root, '0');
  const first = root.__statsui;
  runPageJs(root, '0');

  assert.ok(first, 'the bundle set no window.__statsui');
  assert.equal(root.__statsui, first, 'a second send replaced the interface');
  assert.equal(typeof first.setData, 'function');
  assert.equal(typeof first.check, 'function');
});

test('setData installs, and check() answers ok with the observer runs, the passes, and their time', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  assert.equal(setData(root, emptyData()), 'installed');
  assert.match(check(root), /^ok \d+ 1 \d+$/, 'one setData is one apply pass, with its time in microseconds');
  assert.match(check(root), /^ok \d+ 0 0$/, 'the counts start again after each check');
});

// The passes of a check() answer.
function passesOf(answer: string): number {
  return Number(answer.split(' ')[2]);
}

test('a change of the page outside the stat panel and the bag button runs no apply pass', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());
  await wait(core, 50);
  check(root);

  core.document.body.appendChild(core.document.createElement('div'));
  await wait(core, 50);

  const answer = check(root);
  assert.ok(Number(answer.split(' ')[1]) > 0, 'the observer saw no change: ' + answer);
  assert.equal(passesOf(answer), 0, answer);
});

test('a burst of changes in the stat panel runs one apply pass', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());
  await wait(core, 50);
  check(root);

  const panel = core.document.querySelector('.status-panel');
  for (let i = 0; i < 3; i++) {
    panel.appendChild(core.document.createElement('div'));
    await Promise.resolve();
  }
  await wait(core, 50);

  assert.equal(passesOf(check(root)), 1);
});

test('a tooltip of the mod or a change inside a game tooltip runs no apply pass', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  setData(root, emptyData());
  await wait(core, 50);
  check(root);

  const doc = core.document;
  const tip = doc.createElement('div');
  tip.className = 'stui-stat-tip';
  doc.querySelector('.progress-bar-wrapper').appendChild(tip);
  tip.appendChild(doc.createElement('span'));
  doc.querySelector('.status-tooltip').appendChild(doc.createElement('div'));
  await wait(core, 50);
  tip.remove();
  await wait(core, 50);

  const answer = check(root);
  assert.ok(Number(answer.split(' ')[1]) > 0, 'the observer saw no change: ' + answer);
  assert.equal(passesOf(answer), 0, answer);
});

test('check() is not ok before the first setData', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  assert.equal(check(root), 'no observer');
});

test('with no CoreUI1 frame, setData and check() name the frame', async (t) => {
  const root = rootWindow(t, null);
  runPageJs(root, '0');

  assert.equal(setData(root, emptyData()), 'no CoreUI1 frame');
  assert.equal(check(root), 'no CoreUI1 frame');
});

test('the frame gets one style node and one observer', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);
  runPageJs(root, '0');

  setData(root, emptyData());
  const observer = core.__statsuiObserver;
  setData(root, emptyData());

  assert.ok(observer, 'no observer on the frame window');
  assert.equal(core.__statsuiObserver, observer, 'a second setData made a second observer');
  assert.equal(core.document.querySelectorAll('#statsui-style').length, 1);
});

test('a rebuilt CoreUI1 frame makes check() fail, and the next push installs again', async (t) => {
  const core = await loadCoreWindow(t);
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  assert.equal(setData(root, emptyData()), 'installed');

  const rebuilt = await loadCoreWindow(t);
  await postAttr(rebuilt, attrRows());
  root.setFrame(rebuilt);

  assert.doesNotMatch(check(root), /^ok/, 'check() is ok on a frame with no observer');
  assert.equal(setData(root, emptyData()), 'installed');
  assert.match(check(root), /^ok /);
  assert.ok(rebuilt.document.body.classList.contains('stui-text'), 'the new frame has no mod text style');
});

test('a setData with the same data writes nothing to the page', async (t) => {
  const core = await loadCoreWindow(t);
  await postAttr(core, attrRows());
  const root = rootWindow(t, core);
  runPageJs(root, '0');
  const data = emptyData();
  data.stats = plainStats();
  data.stats[1].mark = { text: 'x1.65', tone: 'good' };
  data.stats[0].threshold = { start: 80, end: 60 };
  data.weight = { text: '7.7', fill: 0.58, line: 0.8, tone: 'good' };
  setData(root, data);

  let records = 0;
  const watch = new core.MutationObserver((list: unknown[]) => { records += list.length; });
  watch.observe(core.document.documentElement, { childList: true, subtree: true, attributes: true, characterData: true });
  setData(root, data);
  await wait(core, 30);
  watch.disconnect();

  assert.equal(records, 0);
});
