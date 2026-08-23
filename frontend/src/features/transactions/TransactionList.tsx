import { Link } from "react-router-dom";
import { TRANSACTION_DIRECTION, TRANSACTION_TYPE_LABELS, movesUnits, type Transaction } from "./types";
import styles from "./TransactionList.module.css";

const dateFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "2-digit",
  year: "2-digit",
});

// Crypto positions run to eight decimals, and toLocaleString stops at three by default —
// a truncated quantity in a list that is the source of truth for quantities would be a lie.
const formatUnits = (units: number) => units.toLocaleString("uk-UA", { maximumFractionDigits: 10 });

const formatMoney = (amount: number) =>
  amount.toLocaleString("uk-UA", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

interface Props {
  transactions: Transaction[];
  /** The account page lists several holdings at once and needs to say which is which. */
  showHolding?: boolean;
  onEdit?: (transaction: Transaction) => void;
  onDelete?: (transaction: Transaction) => void;
  emptyMessage: string;
}

export function TransactionList({ transactions, showHolding, onEdit, onDelete, emptyMessage }: Props) {
  if (transactions.length === 0) {
    return <p className={styles.empty}>{emptyMessage}</p>;
  }

  return (
    <ul className={styles.list}>
      {transactions.map((transaction) => {
        const direction = TRANSACTION_DIRECTION[transaction.type];

        return (
          <li key={transaction.id} className={styles.row}>
            <span className={styles.date}>{dateFormatter.format(new Date(transaction.date))}</span>

            <div className={styles.main}>
              <div className={styles.headline}>
                <span className={styles.type}>{TRANSACTION_TYPE_LABELS[transaction.type]}</span>
                {showHolding && (
                  <Link to={`/holdings/${transaction.holdingId}`} className={styles.holding}>
                    {transaction.holdingName}
                  </Link>
                )}
                {/* Colour means one thing here — units in or out — and the sign in front of
                    the number says the same, so the row still reads without it. */}
                {movesUnits(transaction.type) && (
                  <span className={direction > 0 ? styles.unitsIn : styles.unitsOut}>
                    {direction > 0 ? "+" : "−"}
                    {formatUnits(transaction.quantity)} од.
                  </span>
                )}
              </div>
              {transaction.notes && <p className={styles.notes}>{transaction.notes}</p>}
            </div>

            <div className={styles.amountColumn}>
              <span className={styles.amount}>
                {formatMoney(transaction.amount)} {transaction.currency}
              </span>
              {movesUnits(transaction.type) && (
                <span className={styles.unitPrice}>{formatMoney(transaction.unitPrice)} / од.</span>
              )}
            </div>

            {(onEdit || onDelete) && (
              <div className={styles.actions}>
                {onEdit && (
                  <button className={styles.action} onClick={() => onEdit(transaction)} aria-label="Редагувати">
                    ✎
                  </button>
                )}
                {onDelete && (
                  <button
                    className={styles.actionDanger}
                    onClick={() => onDelete(transaction)}
                    aria-label="Видалити транзакцію"
                  >
                    ✕
                  </button>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
