import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAccountDetail } from "./useAccountDetail";
import { ACCOUNT_TYPE_LABELS } from "./types";
import { HoldingRow } from "../holdings/HoldingRow";
import { AddHoldingModal } from "../holdings/AddHoldingModal";
import { useDeleteHolding } from "../holdings/useHoldings";
import styles from "./AccountDetailPage.module.css";

export function AccountDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: account, isLoading } = useAccountDetail(id);
  const deleteHolding = useDeleteHolding(id!);
  const [isModalOpen, setModalOpen] = useState(false);

  if (isLoading) return <div className={styles.page}>Завантаження…</div>;
  if (!account) return <div className={styles.page}>Рахунок не знайдено.</div>;

  const total = account.holdings.reduce((sum, h) => sum + h.currentValue, 0);

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to="/" className={styles.back}>
          ← Дашборд
        </Link>
        <h1 className={styles.name}>{account.name}</h1>
        <p className={styles.subtitle}>{ACCOUNT_TYPE_LABELS[account.type]}</p>
        <p className={styles.total}>
          {total.toLocaleString("uk-UA")} {account.currency}
        </p>
      </header>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>Активи</h2>
          <button className={styles.addButton} onClick={() => setModalOpen(true)}>
            + Додати актив
          </button>
        </div>

        {account.holdings.length === 0 && (
          <div className={styles.empty}>
            <p>У цьому рахунку ще немає активів.</p>
            <button className={styles.addButton} onClick={() => setModalOpen(true)}>
              Додати перший актив
            </button>
          </div>
        )}

        <div className={styles.list}>
          {account.holdings.map((holding) => (
            <HoldingRow
              key={holding.id}
              holding={holding}
              onDelete={(hId) => deleteHolding.mutate(hId)}
            />
          ))}
        </div>
      </section>

      {isModalOpen && (
        <AddHoldingModal
          accountId={account.id}
          currency={account.currency}
          onClose={() => setModalOpen(false)}
        />
      )}
    </div>
  );
}
