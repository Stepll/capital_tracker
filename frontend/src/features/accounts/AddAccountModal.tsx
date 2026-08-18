import { useState, type FormEvent } from "react";
import { useCreateAccount } from "./useAccounts";
import { ACCOUNT_TYPE_LABELS, type AccountType } from "./types";
import styles from "../../shared/ui/Modal.module.css";
import { CURRENCIES } from "../../shared/currencies";

interface Props {
  onClose: () => void;
}


export function AddAccountModal({ onClose }: Props) {
  const [name, setName] = useState("");
  const [type, setType] = useState<AccountType>("Bank");
  const [currency, setCurrency] = useState("UAH");
  const createAccount = useCreateAccount();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    await createAccount.mutateAsync({ name, type, currency });
    onClose();
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <form
        className={styles.modal}
        onClick={(e) => e.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <h2 className={styles.title}>Новий рахунок</h2>

        <label className={styles.field}>
          <span>Назва</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="напр. Приватбанк, Interactive Brokers"
            required
            autoFocus
          />
        </label>

        <label className={styles.field}>
          <span>Тип</span>
          <select value={type} onChange={(e) => setType(e.target.value as AccountType)}>
            {Object.entries(ACCOUNT_TYPE_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>

        <label className={styles.field}>
          <span>Валюта</span>
          <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
            {CURRENCIES.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>

        <div className={styles.actions}>
          <button type="button" className={styles.cancel} onClick={onClose}>
            Скасувати
          </button>
          <button type="submit" className={styles.submit} disabled={createAccount.isPending}>
            {createAccount.isPending ? "Створюємо…" : "Створити"}
          </button>
        </div>
      </form>
    </div>
  );
}
