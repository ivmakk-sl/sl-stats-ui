// The page clock: the game time in game seconds, the last anchor of C#.
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { makeClock } from '../../src/Web/page/core';

beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { vi.useRealTimers(); });

test('the clock starts at the anchor of a check call', () => {
  const clock = makeClock();
  clock.anchor(5000);

  expect(clock.now()).toBe(5000);
});

test('the clock does not guess the game time between two anchors, as the game can slow its time on its own', () => {
  const clock = makeClock();
  clock.anchor(5000);

  vi.advanceTimersByTime(3000);

  expect(clock.now()).toBe(5000);
});

test('a new anchor replaces the old one, ahead or behind (a save load)', () => {
  const clock = makeClock();
  clock.anchor(5000);

  clock.anchor(5060);
  expect(clock.now()).toBe(5060);
  clock.anchor(1000);
  expect(clock.now()).toBe(1000);
});

test('the clock has no time before its first anchor', () => {
  expect(makeClock().now()).toBeNull();
});
