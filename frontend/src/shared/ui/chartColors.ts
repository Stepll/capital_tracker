/**
 * The app's one categorical series order — dark-mode steps, fixed order, never cycled and
 * never reassigned per render.
 *
 * Validated with the `dataviz` palette validator against this app's real chart surface
 * (--surface #17171b): lightness band, chroma floor, CVD separation, normal-vision floor
 * and contrast all pass, worst adjacent pair ΔE 8.4 (protan). A donut is an adjacent-pair
 * form — slices touch in the ring — which is the pairlist this order was checked on.
 *
 * ACCOUNT_TYPE_COLORS reserves the first six slots for account types, so a type keeps its
 * colour everywhere it appears; a holding simply takes the next free slot in its own chart.
 */
export const SERIES_COLORS = [
  "#3987e5", // blue
  "#d95926", // orange
  "#199e70", // aqua
  "#c98500", // yellow
  "#d55181", // magenta
  "#008300", // green
  "#9085e9", // violet
  "#e66767", // red
];

/**
 * Everything past the cut folds into one grey bucket instead of getting a generated hue —
 * an invented ninth colour is where a categorical palette stops being readable.
 * Deliberately achromatic, so it fails the validator's chroma floor by design: grey is
 * what "not one of the named ones" looks like. It still clears contrast against the
 * surface and separation from both its neighbours in the ring (CVD ΔE 9.5, normal 17.6).
 */
export const SERIES_OTHER_COLOR = "#6b6b73";

/**
 * Six named slices, then "Інші". Not a palette limit (there are eight slots) but a
 * legibility one: past six, the slice labels start colliding on the ring.
 */
const MAX_NAMED_SLICES = 6;

export interface Slice {
  name: string;
  value: number;
  color: string;
}

/** Biggest first, colours by position, the tail folded into one grey slice. */
export function toSlices(items: { name: string; value: number }[]): Slice[] {
  const sorted = [...items].sort((a, b) => b.value - a.value);
  const named = sorted
    .slice(0, MAX_NAMED_SLICES)
    .map((item, index) => ({ ...item, color: SERIES_COLORS[index] }));

  const rest = sorted.slice(MAX_NAMED_SLICES);
  if (rest.length === 0) {
    return named;
  }

  return [
    ...named,
    {
      name: "Інші",
      value: rest.reduce((sum, item) => sum + item.value, 0),
      color: SERIES_OTHER_COLOR,
    },
  ];
}
