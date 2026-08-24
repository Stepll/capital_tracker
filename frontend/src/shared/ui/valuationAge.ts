export type ValuationStatus = "Fresh" | "NeedsManualUpdate" | "AutoPricingStalled";

export interface ValuationAge {
  /** Null when the holding has never been valued at all. */
  lastValuedOn: string | null;
  days: number | null;
  status: ValuationStatus;
}

const dateFormatter = new Intl.DateTimeFormat("uk-UA", { day: "2-digit", month: "long", year: "numeric" });

function plural(days: number) {
  const lastTwo = days % 100;
  const last = days % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return `${days} днів`;
  if (last === 1) return `${days} день`;
  if (last >= 2 && last <= 4) return `${days} дні`;
  return `${days} днів`;
}

/**
 * The two stale cases read differently on purpose. One asks the owner for a number; the
 * other says the daily price job stopped doing its work, which no amount of typing fixes.
 */
export function staleMessage(age: ValuationAge): string | null {
  if (age.status === "Fresh") return null;

  if (age.lastValuedOn === null || age.days === null) {
    return "оцінки ще не було";
  }

  const since = dateFormatter.format(new Date(age.lastValuedOn));

  return age.status === "AutoPricingStalled"
    ? `ціна мала оновлюватись щодня, але не оновлювалась ${plural(age.days)}`
    : `оцінка від ${since} — ${plural(age.days)} тому`;
}

/**
 * The same fact at card width, where the full sentence wraps to two lines and pushes the
 * holding's value into wrapping with it. The long form is for the dashboard and the
 * holding's own page, which have a line to spare.
 */
export function staleBadge(age: ValuationAge): string | null {
  if (age.status === "Fresh") return null;
  if (age.days === null) return "без оцінки";

  return age.status === "AutoPricingStalled"
    ? `ціна не оновлюється · ${age.days} дн`
    : `оцінка застаріла · ${age.days} дн`;
}

/** Paired with the message everywhere, so colour is never the only thing saying this. */
export function staleGlyph(age: ValuationAge): string {
  return age.status === "AutoPricingStalled" ? "⚠" : "⏳";
}
