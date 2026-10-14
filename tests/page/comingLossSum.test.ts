// The sum of the coming loss of one stat row from the buff entries, the rest of the loss ratio, and the page clock.
import { expect, test } from 'vitest';
import { comingLossSum } from '../../src/Web/page/comingLoss';
import type { BuffData, LossData } from '../../src/Web/page/types';

const HOUR = 3600;
const NOW = 100000;
const SATIETY = 0, STAMINA = 2, LIFE = 3;

function none(): LossData {
  return { each: 0, every: 0, atEnd: 0, ratio: 0 };
}

// A buff entry with the given loss on each named row.
function buff(id: number, end: number, rows: Record<number, Partial<LossData>>): BuffData {
  const losses = [none(), none(), none(), none()];
  for (const k of Object.keys(rows)) Object.assign(losses[Number(k)], rows[Number(k)]);
  return { id, row: 2, good: true, layers: 1, maxLayers: 0, end, full: 86400, desc: '', suppressed: null, losses, lines: [] };
}

// Stimulants: Life -3 each hour.
const stimulants = (hoursLeft: number) => buff(24040, NOW + hoursLeft * HOUR, { [LIFE]: { each: 3, every: HOUR } });

test('Stimulants with 12 hours left and no loss ratio give 36 Life', () => {
  expect(comingLossSum(LIFE, [stimulants(12)], 0, 193, NOW)).toBe(36);
});

test('Adrenaline gives its loss at the end: 100 Stamina and 20 Life, without its own loss ratio', () => {
  const adrenaline = buff(24030, NOW + 14 * HOUR, { [STAMINA]: { atEnd: 100, ratio: -0.5 }, [LIFE]: { atEnd: 20 } });

  expect(comingLossSum(STAMINA, [adrenaline], 0, 357, NOW)).toBe(100);
  expect(comingLossSum(LIFE, [adrenaline], 0, 193, NOW)).toBe(20);
});

test('a loss ratio of x0.7 that ends after 5 of the 12 intervals applies to those 5 only', () => {
  const firstAid = buff(3022, NOW + 5.5 * HOUR, { [LIFE]: { ratio: -0.3 } });

  // 5 x 3 x 0.7 + 7 x 3
  expect(comingLossSum(LIFE, [stimulants(12), firstAid], 0, 193, NOW)).toBeCloseTo(31.5, 6);
});

test('the rest of the loss ratio applies to each interval and to the loss at the end', () => {
  const adrenaline = buff(24030, NOW + 2 * HOUR, { [LIFE]: { atEnd: 20 } });

  expect(comingLossSum(LIFE, [stimulants(2), adrenaline], -0.5, 193, NOW)).toBeCloseTo(13, 6);
});

// The game's limits of a loss ratio (AttributeStandard 30001-30005): x0.5 to x3.
const LIMITS = { min: -0.5, max: 2 };

test('the loss at the end applies the game limit to the sum of the parts that still run', () => {
  // Fixture p: talents give a raw rest of -0.5; two timed buffs (-0.3, -0.4) end before Adrenaline (-0.5 of its own).
  const focus = buff(3005, NOW + 14 * HOUR, { [STAMINA]: { ratio: -0.3 } });
  const steady = buff(310110, NOW + 23 * HOUR, { [STAMINA]: { ratio: -0.4 } });
  const adrenaline = buff(24030, NOW + 24 * HOUR, { [STAMINA]: { atEnd: 100, ratio: -0.5 } });

  expect(comingLossSum(STAMINA, [focus, steady, adrenaline], -0.5, 300, NOW, LIMITS)).toBeCloseTo(50, 6);
});

test('each interval applies the game limit: a raw loss ratio of -1.2 counts x0.5', () => {
  const firstAid = buff(3022, NOW + 24 * HOUR, { [LIFE]: { ratio: -0.3 } });

  // 12 x 3 x 0.5
  expect(comingLossSum(LIFE, [stimulants(12), firstAid], -0.9, 193, NOW, LIMITS)).toBeCloseTo(18, 6);
});

test('the upper limit: a raw loss ratio of +2.5 counts x3', () => {
  expect(comingLossSum(LIFE, [stimulants(2)], 2.5, 193, NOW, LIMITS)).toBeCloseTo(18, 6);
});

test('a buff with no end counts no interval and no loss at the end', () => {
  const freezing = buff(1114, -1, { [SATIETY]: { each: 4, every: HOUR, atEnd: 5 } });

  expect(comingLossSum(SATIETY, [freezing], 0, 135, NOW)).toBe(0);
});

test('only the whole intervals left count', () => {
  expect(comingLossSum(LIFE, [stimulants(2.9)], 0, 193, NOW)).toBe(6);
});

test('the sum stops at the current value', () => {
  expect(comingLossSum(LIFE, [stimulants(12)], 0, 20, NOW)).toBe(20);
});

test('a buff whose losses the mod cannot read counts nothing, and no clock gives no loss', () => {
  const tree = { ...stimulants(12), losses: null };

  expect(comingLossSum(LIFE, [tree], 0, 193, NOW)).toBe(0);
  expect(comingLossSum(LIFE, [stimulants(12)], 0, 193, null)).toBe(0);
});
