import { useState } from "react";
import { Link } from "react-router-dom";
import { useAccounts, useDeleteAccount } from "../accounts/useAccounts";
import { AccountCard } from "../accounts/AccountCard";
import { AddAccountModal } from "../accounts/AddAccountModal";
import { useDashboardSummary } from "./useDashboardSummary";
import { AllocationChart } from "./AllocationChart";
import { ValueOverTimeChart } from "../../shared/ui/ValueOverTimeChart";
import { useAuth } from "../../shared/auth/AuthContext";
import styles from "./DashboardPage.module.css";
import chartStyles from "../../shared/ui/Charts.module.css";

const CURRENCY_SYMBOLS: Record<string, string> = { UAH: "₴", USD: "$", EUR: "€" };

export function DashboardPage() {
  const { data: accounts, isLoading } = useAccounts();
  const { data: summary } = useDashboardSummary();
  const deleteAccount = useDeleteAccount();
  const { logout } = useAuth();
  const [isModalOpen, setModalOpen] = useState(false);

  const currencySymbol = summary ? CURRENCY_SYMBOLS[summary.currency] ?? summary.currency : "";
  const totalLabel = summary ? summary.totalNetWorth.toLocaleString("uk-UA") : "—";

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div>
          <p className={styles.eyebrow}>Загальний капітал</p>
          <h1 className={styles.total}>
            {totalLabel} {currencySymbol}
          </h1>
        </div>
        <div className={styles.headerActions}>
          <Link to="/insights" className={styles.settingsLink}>
            AI-аналітика
          </Link>
          <Link to="/settings" className={styles.settingsLink}>
            Налаштування
          </Link>
          <button className={styles.logout} onClick={logout}>
            Вийти
          </button>
        </div>
      </header>

      {summary && (summary.allocationByType.length > 0 || summary.netWorthHistory.length > 0) && (
        <div className={chartStyles.chartsGrid}>
          <div className={chartStyles.card}>
            <h2 className={chartStyles.cardTitle}>Розподіл капіталу</h2>
            <AllocationChart data={summary.allocationByType} currency={summary.currency} />
          </div>
          <div className={chartStyles.card}>
            <h2 className={chartStyles.cardTitle}>Динаміка капіталу</h2>
            <ValueOverTimeChart data={summary.netWorthHistory} currency={summary.currency} />
          </div>
        </div>
      )}

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
