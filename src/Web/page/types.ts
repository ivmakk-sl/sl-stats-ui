// The data that the page script reads: the fields of the CoreUI1 page state, and the data of setData.

// One buff or debuff badge of a stat row (parseBuffList of CoreUI1): the fields of buffsJson, and the key
// `<buffConfigId>#<n>` of its n-th occurrence in the row.
export interface CoreBuff {
  name: string;
  isGood: boolean;
  buffConfigId: number;
  uniqueKey: string;
}

// One stat row of the CoreUI1 state (state.attrList, filled from WebUI_CoreUI1_AttrMsg), in the order
// Satiety, Morale, Stamina, Life.
export interface CoreAttr {
  attrName: string;
  attrIcon: string;
  // The fill as a part of 1.
  value: number;
  // The value and the maximum as the game shows them, for example "76/135".
  barValue: string;
  tipName: string;
  tipValue: string;
  tipDesc: string;
  buffs: CoreBuff[];
}

// The fields of `const state = reactive({...})` in CoreUI1.html that the script reads.
export interface CoreState {
  attrList: CoreAttr[];
  isPause: boolean;
  // The bag button shows (v-if) only while this is true.
  bagActive: boolean;
}

// The CoreUI1 frame: a window whose top-level `state` the script reads with eval.
export type CoreWindow = Window & typeof globalThis;

// The config switches of the mod (PageJson.DataJson in C#).
export interface Switches {
  weightReadout: boolean;
  timeStripes: boolean;
  extendedTooltips: boolean;
  // The text of the game at the bottom of the stat tooltip.
  statTooltipGameText: boolean;
  // The cures and the suppressors in the tooltip of a debuff badge, shown only while extendedTooltips is on too.
  curesInTooltips: boolean;
}

// The gain mark of a stat bar (StatsLogic.GainMark in C#).
export interface GainMark {
  text: string;
  tone: 'good' | 'bad' | 'cap';
}

// The Satiety values where Well-Fed starts and where it ends (StatsLogic.WellFedThreshold in C#).
export interface Threshold {
  start: number;
  end: number;
}

// The data of one stat row (PageJson.StatsJson), in the order of state.attrList: the gain mark (null at x1), the
// ratios of the game as parts of 1 (0.5 is +50%), the threshold line (on the Satiety row only), and the food types.
export interface StatData {
  mark: GainMark | null;
  gain: number;
  loss: number;
  naturalGain: number;
  naturalLoss: number;
  // The raw loss ratio (before the game's limits) without the parts of the buffs with an end, which the coming loss
  // applies only before each end.
  lossRest: number;
  // The game's limits of the loss ratio (AttributeStandard: -0.5 and 2), applied to the sum at each loss.
  lossMin: number;
  lossMax: number;
  threshold: Threshold | null;
  // The gain ratio of each food type that has one, on eat only.
  food: { name: string; ratio: number }[];
}

// The loss of one stat from one buff (BuffsLogic.Loss in C#), times its layers: the loss of each interval, the
// interval in game seconds (0 for none), the loss at its end, and its part of the loss ratio of the stat.
export interface LossData {
  each: number;
  every: number;
  atEnd: number;
  ratio: number;
}

// One effect line of a buff (EffectLines.Text in C#), in the display language: "Morale gain" and "+50%".
export interface EffectLine {
  label: string;
  value: string;
  // A penalty: the value shows in the debuff tone.
  bad: boolean;
}

// One buff or debuff of a stat row (PageJson.BuffsJson), in the order of the game's badges: its config id, its
// stat row, buff or debuff, its layers, its end time and its full time in game seconds (-1 for no end or none), the
// text of the game, the suppression line of a suppressed debuff (null for another badge), the loss of each stat row
// (null when the mod cannot read its losses or the game suppresses the debuff), and the effect lines.
export interface BuffData {
  id: number;
  row: number;
  good: boolean;
  layers: number;
  // The cap of the layers (MaxLayerCount), 0 or 1 for none.
  maxLayers: number;
  end: number;
  full: number;
  desc: string;
  suppressed: string | null;
  losses: LossData[] | null;
  lines: EffectLine[];
}

// The carried weight (PageJson.WeightJson): the number of the bag button, the fill of the weight bar and the
// place of its Encumbered line as parts of the weight limit, and the tone.
export interface WeightData {
  text: string;
  fill: number;
  line: number;
  tone: 'good' | 'near' | 'over';
}

// One item line of the cures part (PageJson.CuresJson): the item id, its name in the display language, its web icon
// (a path of the web UI, empty for none), and its item total (0: dimmed, no count).
export interface CureItem {
  id: number;
  name: string;
  icon: string;
  total: number;
}

// The two item lists of one debuff, in the order of the tooltip (CuresLogic.LinesOf in C#).
export interface CureLists {
  cures: CureItem[];
  suppressors: CureItem[];
}

// The data of setData (PageJson.DataJson): the switches, the mod texts of the display language by name, and
// the parts of the stat panel and the bag button. weight null: not read yet. cures: the item lists of each debuff
// of the buff list with a cure or a suppressor, by its config id.
export interface PageData {
  switches: Switches;
  texts: Record<string, string>;
  stats: StatData[];
  buffs: BuffData[];
  weight: WeightData | null;
  cures: Record<string, CureLists>;
}

// now: the game time in game seconds, the anchor of the page clock.
export interface StatsUiApi {
  setData(data: PageData, now?: number): string;
  check(now?: number): string;
}

declare global {
  interface Window {
    // The interface of the page script in the root page.
    __statsui?: StatsUiApi;
    // The body observer of the page script in the CoreUI1 frame.
    __statsuiObserver?: MutationObserver;
  }
}
