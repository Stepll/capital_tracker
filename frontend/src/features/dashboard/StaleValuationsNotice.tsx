import { Link } from "react-router-dom";
import { staleGlyph, staleMessage } from "../../shared/ui/valuationAge";
import type { StaleValuation } from "./useDashboardSummary";
import styles from "./StaleValuationsNotice.module.css";

const MAX_ROWS = 5;

function plural(count: number) {
  const lastTwo = count % 100;
  const last = count % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return `${count} активів`;
  if (last === 1) return `${count} актив`;
  if (last >= 2 && last <= 4) return `${count} активи`;
  return `${count} активів`;
}

interface Props {
  stale: StaleValuation[];
  totalNetWorth: number;
}

/**
 * Sits under the headline total because that is what it is about: the number above is only
 * as true as the valuations behind it, and this says how much of it has gone out of date.
 * Deliberately quiet — a fact, not an alarm — and every row carries a glyph and a sentence
 * so the colour is never the only thing saying something is wrong.
 */
export function StaleValuationsNotice({ stale, totalNetWorth }: Props) {
  if (stale.length === 0) return null;

  const staleValue = stale.reduce((sum, item) => sum + item.valueInDisplayCurrency, 0);
  const share = totalNetWorth > 0 ? Math.round((staleValue / totalNetWorth) * 100) : 0;
  const shown = stale.slice(0, MAX_ROWS);

  return (
    <section className={styles.notice}>
      <p className={styles.headline}>
        {plural(stale.length)} із застарілою оцінкою
        {share > 0 && <span className={styles.share}> · {share}% капіталу</span>}
      </p>

      <ul className={styles.list}>
        {shown.map((item) => (
          <li key={item.holdingId}>
            <Link to={`/holdings/${item.holdingId}`} className={styles.row}>
              <span
                className={
                  item.valuationAge.status === "AutoPricingStalled" ? styles.glyphAlert : styles.glyph
                }
                aria-hidden="true"
              >
                {staleGlyph(item.valuationAge)}
              </span>
              <span className={styles.name}>{item.name}</span>
              <span className={styles.account}>{item.accountName}</span>
              <span className={styles.message}>{staleMessage(item.valuationAge)}</span>
            </Link>
          </li>
        ))}
      </ul>

      {stale.length > shown.length && (
        <p className={styles.more}>і ще {stale.length - shown.length} — відкрий рахунки, щоб побачити всі.</p>
      )}
    </section>
  );
}
