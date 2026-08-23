import { useState, type FormEvent } from "react";
import { CURRENCIES } from "../../shared/currencies";
import styles from "../../shared/ui/Modal.module.css";
import {
  saveErrorMessage,
  useAddTransaction,
  useUpdateTransaction,
  type SaveTransactionInput,
} from "./useTransactions";
import { TRANSACTION_TYPES, TRANSACTION_TYPE_LABELS, movesUnits, type Transaction, type TransactionType } from "./types";

const todayIso = () => new Date().toISOString().slice(0, 10);

interface Props {
  holdingId: string;
  /** What the holding is already denominated in — the default for a new row. */
  currency: string;
  /** Present when editing; absent when adding. */
  transaction?: Transaction;
  onClose: () => void;
}

export function TransactionFormModal({ holdingId, currency, transaction, onClose }: Props) {
  const [type, setType] = useState<TransactionType>(transaction?.type ?? "Buy");
  const [date, setDate] = useState(transaction?.date ?? todayIso());
  const [quantity, setQuantity] = useState(transaction ? String(transaction.quantity) : "");
  // One field serves two roles: a unit price where units exist, the whole sum where they
  // don't. A cash flow is stored as one unit at that price, so the two stay the same shape.
  const [price, setPrice] = useState(
    transaction ? String(movesUnits(transaction.type) ? transaction.unitPrice : transaction.amount) : "",
  );
  const [rowCurrency, setRowCurrency] = useState(transaction?.currency ?? currency);
  const [notes, setNotes] = useState(transaction?.notes ?? "");

  const addTransaction = useAddTransaction(holdingId);
  const updateTransaction = useUpdateTransaction(transaction?.id ?? "");
  const mutation = transaction ? updateTransaction : addTransaction;

  const withUnits = movesUnits(type);
  const total = withUnits ? Number(quantity || 0) * Number(price || 0) : Number(price || 0);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    const input: SaveTransactionInput = {
      type,
      date,
      quantity: withUnits ? Number(quantity) : 1,
      unitPrice: Number(price),
      currency: rowCurrency,
      notes: notes.trim() || null,
    };

    try {
      await mutation.mutateAsync(input);
      onClose();
    } catch {
      // Rendered from mutation.error below — closing here would hide the reason.
    }
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <form className={styles.modal} onClick={(e) => e.stopPropagation()} onSubmit={handleSubmit}>
        <h2 className={styles.title}>{transaction ? "Редагувати транзакцію" : "Нова транзакція"}</h2>

        <label className={styles.field}>
          <span>Тип</span>
          <select value={type} onChange={(e) => setType(e.target.value as TransactionType)}>
            {TRANSACTION_TYPES.map((option) => (
              <option key={option} value={option}>
                {TRANSACTION_TYPE_LABELS[option]}
              </option>
            ))}
          </select>
        </label>

        <label className={styles.field}>
          <span>Дата</span>
          <input type="date" value={date} max={todayIso()} onChange={(e) => setDate(e.target.value)} required />
        </label>

        <div className={styles.fieldRow}>
          {withUnits && (
            <label className={styles.field}>
              <span>Кількість</span>
              <input
                type="number"
                min="0"
                step="any"
                placeholder="напр. 10"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                required
                autoFocus
              />
            </label>
          )}

          <label className={styles.field}>
            <span>{withUnits ? "Ціна за одиницю" : "Сума"}</span>
            <input
              type="number"
              min="0"
              step="0.01"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              required
              autoFocus={!withUnits}
            />
          </label>

          <label className={styles.field}>
            <span>Валюта</span>
            <select value={rowCurrency} onChange={(e) => setRowCurrency(e.target.value)}>
              {CURRENCIES.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>
        </div>

        {withUnits && total > 0 && (
          <p className={styles.hint}>
            Разом: {total.toLocaleString("uk-UA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}{" "}
            {rowCurrency}
          </p>
        )}

        <label className={styles.field}>
          <span>Нотатка (опційно)</span>
          <input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="напр. реінвестування" />
        </label>

        {mutation.isError && <p className={styles.error}>{saveErrorMessage(mutation.error)}</p>}

        <div className={styles.actions}>
          <button type="button" className={styles.cancel} onClick={onClose}>
            Скасувати
          </button>
          <button type="submit" className={styles.submit} disabled={mutation.isPending}>
            {mutation.isPending ? "Зберігаємо…" : "Зберегти"}
          </button>
        </div>
      </form>
    </div>
  );
}
