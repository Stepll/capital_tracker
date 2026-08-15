import { useState } from "react";
import { useAccounts, useDeleteAccount } from "../accounts/useAccounts";
import { AccountCard } from "../accounts/AccountCard";
import { AddAccountModal } from "../accounts/AddAccountModal";
import { useAuth } from "../../shared/auth/AuthContext";
import styles from "./DashboardPage.module.css";

export function DashboardPage() {
  const { data: accounts, isLoading } = useAccounts();
  const deleteAccount = useDeleteAccount();
  const { logout } = useAuth();
  const [isModalOpen, setModalOpen] = useState(false);

  // Real net worth needs valuation snapshots (Phase 2) — for now every account
  // is freshly created with no holdings, so this is honestly zero rather than
  // pretending to sum something we don't track yet.
  const totalLabel = accounts && accounts.length > 0 ? "0" : "—";

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div>
          <p className={styles.eyebrow}>Загальний капітал</p>
          <h1 className={styles.total}>{totalLabel} ₴</h1>
        </div>
        <button className={styles.logout} onClick={logout}>
          Вийти
        </button>
      </header>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>Рахунки</h2>
          <button className={styles.addButton} onClick={() => setModalOpen(true)}>
            + Додати рахунок
          </button>
        </div>

        {isLoading && <p className={styles.hint}>Завантаження…</p>}

        {!isLoading && accounts?.length === 0 && (
          <div className={styles.empty}>
            <p>Ще немає жодного рахунку.</p>
            <button className={styles.addButton} onClick={() => setModalOpen(true)}>
              Додати перший рахунок
            </button>
          </div>
        )}

        <div className={styles.grid}>
          {accounts?.map((account) => (
            <AccountCard
              key={account.id}
              account={account}
              onDelete={(id) => deleteAccount.mutate(id)}
            />
          ))}
        </div>
      </section>

      {isModalOpen && <AddAccountModal onClose={() => setModalOpen(false)} />}
    </div>
  );
}
