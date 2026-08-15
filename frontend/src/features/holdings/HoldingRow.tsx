import type { Holding } from "./types";
import styles from "./HoldingRow.module.css";

interface Props {
  holding: Holding;
  onDelete: (id: string) => void;
}

export function HoldingRow({ holding, onDelete }: Props) {
  return (
    <div className={styles.row}>
      <div className={styles.info}>
        <span className={styles.name}>{holding.name}</span>
        {holding.symbol && <span className={styles.symbol}>{holding.symbol}</span>}
      </div>
      <div className={styles.right}>
        <span className={styles.value}>
          {holding.currentValue.toLocaleString("uk-UA")} {holding.currency}
        </span>
        <button
          className={styles.delete}
          onClick={() => onDelete(holding.id)}
          aria-label="Видалити актив"
        >
          ✕
        </button>
      </div>
    </div>
  );
}
