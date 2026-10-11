// Runs in the root page, reaching the CoreUI1 iframe through iframe.contentWindow, as in Trapline.
// Defines window.__statsui once. C# then calls window.__statsui.setData(data, now) when a stat, a buff
// or debuff, the carried weight, or the display language changes, and window.__statsui.check(now) about once
// each second while nothing changes. now is the game time in game seconds, the anchor of the page clock. Each call
// applies once and does not retry: when the CoreUI1 frame is not there, C# sends again later. A second full send of
// the script keeps the first interface and its state.
import { check as checkFrame, clock, run, view } from './install';
import type { PageData } from './types';

function anchor(now?: number): void {
  if (typeof now === 'number' && isFinite(now) && now > 0) clock.anchor(now);
}

// Stores the data and applies it in one pass.
function setData(data: PageData, now?: number): string {
  anchor(now);
  view.data = data || null;
  return run();
}

// The first anchor after a push with no game time runs one apply pass: the step only moves the hatched parts that a
// pass added, and a pass with no clock adds none.
function check(now?: number): string {
  const hadClock = clock.now() !== null;
  anchor(now);
  if (!hadClock && clock.now() !== null && view.data) run();
  return checkFrame();
}

window.__statsui = window.__statsui || { setData, check };
