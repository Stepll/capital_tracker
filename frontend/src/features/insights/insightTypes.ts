/** Mirrors the backend enums, which serialize by name in PascalCase. */
export type FactCategory =
  | "Risk"
  | "Opportunity"
  | "MarketNews"
  | "Legal"
  | "Financial"
  | "Reputation"
  | "Liquidity";

export type FactPolarity = "Positive" | "Negative" | "Neutral";

export type FactConfidence = "High" | "Medium" | "Low";

export interface AnalysisFact {
  claim: string;
  category: FactCategory;
  polarity: FactPolarity;
  confidence: FactConfidence;
  isNew: boolean;
  sourceName: string | null;
  sourceUrl: string | null;
  sourceDate: string | null;
}

export type InsightPhase =
  | "Preparing"
  | "MarketData"
  | "Thinking"
  | "Searching"
  | "Writing"
  | "Saving";

export type InsightErrorCode =
  | "NotFound"
  | "Excluded"
  | "Empty"
  | "Cooldown"
  | "Refusal"
  | "Upstream"
  | "Internal";

export const FACT_CATEGORY_LABELS: Record<FactCategory, string> = {
  Risk: "Ризик",
  Opportunity: "Можливість",
  MarketNews: "Новини ринку",
  Legal: "Юридичне",
  Financial: "Фінанси",
  Reputation: "Репутація",
  Liquidity: "Ліквідність",
};

export const CONFIDENCE_LABELS: Record<FactConfidence, string> = {
  High: "Висока впевненість",
  Medium: "Середня впевненість",
  Low: "Низька впевненість",
};

/** Filled bars out of three — confidence is deliberately achromatic, see the modal CSS. */
export const CONFIDENCE_LEVEL: Record<FactConfidence, number> = {
  High: 3,
  Medium: 2,
  Low: 1,
};

/**
 * Polarity is the one axis that gets colour, and red/green is the classic
 * deuteranopia collision (validated ΔE 6.4), so it never travels alone — this glyph
 * is the required secondary encoding, not decoration.
 */
export const POLARITY_GLYPH: Record<FactPolarity, string> = {
  Positive: "▲",
  Negative: "▼",
  Neutral: "•",
};

export const POLARITY_LABELS: Record<FactPolarity, string> = {
  Positive: "Позитивний фактор",
  Negative: "Негативний фактор",
  Neutral: "Нейтральний фактор",
};

export const PHASE_LABELS: Record<InsightPhase, string> = {
  Preparing: "Збираємо контекст активу",
  MarketData: "Тягнемо ринкові дані",
  Thinking: "Аналізуємо",
  Searching: "Шукаємо новини",
  Writing: "Формуємо висновки",
  Saving: "Зберігаємо",
};

export const ERROR_LABELS: Record<InsightErrorCode, string> = {
  NotFound: "Актив не знайдено.",
  Excluded: "Для цього активу AI-аналіз вимкнено в налаштуваннях.",
  Empty: "Нема чого аналізувати — додай активи або зніми виключення з AI-аналізу.",
  Cooldown: "Аналіз робили нещодавно.",
  Refusal: "Модель відмовилася аналізувати цей запит.",
  Upstream: "Не вдалося отримати аналіз. Спробуйте ще раз.",
  Internal: "Сталася помилка під час аналізу. Спробуйте ще раз.",
};

/**
 * sourceUrl comes from the model, so it is untrusted input: an unchecked
 * `javascript:` value in an href is a real XSS vector. Anything that isn't plain
 * http(s) is rendered as text instead of a link.
 */
export function safeHttpUrl(url: string | null): string | null {
  if (!url) return null;
  try {
    const parsed = new URL(url);
    return parsed.protocol === "http:" || parsed.protocol === "https:" ? url : null;
  } catch {
    return null;
  }
}
