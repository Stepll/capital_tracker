import { useState, type FormEvent } from "react";
import { useCreateHolding } from "./useHoldings";
import styles from "../../shared/ui/Modal.module.css";

interface Props {
  accountId: string;
  currency: string;
  onClose: () => void;
}

export function AddHoldingModal({ accountId, currency, onClose }: Props) {
  const [name, setName] = useState("");
  const [symbol, setSymbol] = useState("");
  const [quantity, setQuantity] = useState("");
  const [initialValue, setInitialValue] = useState("");
  const createHolding = useCreateHolding();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    await createHolding.mutateAsync({
      accountId,
      name,
      symbol: symbol || undefined,
      quantity: quantity ? Number(quantity) : undefined,
      initialValue: Number(initialValue),
    });
    onClose();
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <form className={styles.modal} onClick={(e) => e.stopPropagation()} onSubmit={handleSubmit}>
        <h2 className={styles.title}>Новий актив</h2>

        <label className={styles.field}>
          <span>Назва</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="напр. Apple Inc., Квартира на Позняках"
            required
            autoFocus
          />
        </label>

        <label className={styles.field}>
          <span>Тікер (опційно)</span>
          <input
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="напр. AAPL"
          />
        </label>

        <label className={styles.field}>
          <span>Кількість одиниць (опційно)</span>
          <input
            type="number"
            min="0"
            step="any"
            placeholder="напр. 10 (акцій), 0.5 (BTC)"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
          />
        </label>

        <label className={styles.field}>
          <span>Поточна вартість, {currency}</span>
          <input
            type="number"
            min="0"
            step="0.01"
            value={initialValue}
            onChange={(e) => setInitialValue(e.target.value)}
            required
          />
        </label>

        <div className={styles.actions}>
          <button type="button" className={styles.cancel} onClick={onClose}>
            Скасувати
          </button>
          <button type="submit" className={styles.submit} disabled={createHolding.isPending}>
            {createHolding.isPending ? "Створюємо…" : "Створити"}
          </button>
        </div>
      </form>
    </div>
  );
}
