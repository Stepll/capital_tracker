import { useState } from "react";
import { useAccounts, useDeleteAccount } from "../accounts/useAccounts";
import { AccountCard } from "../accounts/AccountCard";
import { AddAccountModal } from "../accounts/AddAccountModal";
import { useDashboardSummary } from "./useDashboardSummary";
import { AllocationChart } from "./AllocationChart";
import { StaleValuationsNotice } from "./StaleValuationsNotice";
import { ReturnBreakdown } from "../../shared/ui/ReturnBreakdown";
import { ValueOverTimeChart } from "../../shared/ui/ValueOverTimeChart";
import styles from "./DashboardPage.module.css";
import chartStyles from "../../shared/ui/Charts.module.css";

const CURRENCY_SYMBOLS: Record<string, string> = { UAH: "₴", USD: "$", EUR: "€" };

export function DashboardPage() {
  const { data: accounts, isLoading } = useAccounts();
  const { data: summary } = useDashboardSummary();
  const deleteAccount = useDeleteAccount();
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
          {/* The headline says what is held; this says what of it was earned. */}
          {summary && summary.return.totalPercent !== null && (
            <p className={summary.return.total >= 0 ? styles.returnUp : styles.returnDown}>
              {summary.return.total >= 0 ? "+" : "−"}
              {Math.abs(summary.return.total).toLocaleString("uk-UA", { maximumFractionDigits: 0 })}{" "}
              {currencySymbol} за весь час · {summary.return.totalPercent > 0 ? "+" : ""}
              {summary.return.totalPercent.toLocaleString("uk-UA", { maximumFractionDigits: 1 })}%
            </p>
          )}
        </div>
      </header>

      {summary && (
        <StaleValuationsNotice stale={summary.staleValuations} totalNetWorth={summary.totalNetWorth} />
      )}

      {summary && summary.return.invested > 0 && (
        <div className={chartStyles.card}>
          <h2 className={chartStyles.cardTitle}>Результат портфеля</h2>
          <ReturnBreakdown
            result={summary.return}
            currency={summary.currency}
            hint="Кожна покупка переведена в цю валюту курсом на дату угоди, тож у результат входить і рух курсу."
          />
        </div>
      )}

      {summary && summary.allocationByType.length > 0 && (
        <div className={chartStyles.card}>
          <h2 className={chartStyles.cardTitle}>Розподіл капіталу</h2>
          <AllocationChart data={summary.allocationByType} currency={summary.currency} size="large" />
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

      {summary && summary.netWorthHistory.length > 0 && (
        <div className={chartStyles.card}>
          <h2 className={chartStyles.cardTitle}>Динаміка капіталу</h2>
          <ValueOverTimeChart data={summary.netWorthHistory} currency={summary.currency} />
        </div>
      )}

      {isModalOpen && <AddAccountModal onClose={() => setModalOpen(false)} />}
    </div>
  );
}
