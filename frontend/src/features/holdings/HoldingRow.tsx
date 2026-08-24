import { Link } from "react-router-dom";
import type { Holding } from "./types";
import { staleBadge, staleGlyph } from "../../shared/ui/valuationAge";
import styles from "./HoldingRow.module.css";

interface Props {
  holding: Holding;
  onDelete: (id: string) => void;
}

export function HoldingRow({ holding, onDelete }: Props) {
  return (
    <Link to={`/holdings/${holding.id}`} className={styles.row}>
      <div className={styles.info}>
        <span className={styles.name}>{holding.name}</span>
        {holding.symbol && <span className={styles.symbol}>{holding.symbol}</span>}
        {/* The reminder follows you to the page you would go to in order to act on it —
            dropping it here would lose it exactly when it becomes actionable. */}
        {holding.valuationAge && staleBadge(holding.valuationAge) && (
          <span
            className={
              holding.valuationAge.status === "AutoPricingStalled" ? styles.staleAlert : styles.stale
            }
          >
            {staleGlyph(holding.valuationAge)} {staleBadge(holding.valuationAge)}
          </span>
        )}
      </div>
      <div className={styles.right}>
        <span className={styles.value}>
          {holding.currentValue.toLocaleString("uk-UA")} {holding.currency}
        </span>
        <button
          className={styles.delete}
          onClick={(e) => {
            e.preventDefault();
            onDelete(holding.id);
          }}
          aria-label="Видалити актив"
        >
          ✕
        </button>
      </div>
    </Link>
  );
}
