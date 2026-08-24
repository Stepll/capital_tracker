import { useState } from "react";
import { useParams } from "react-router-dom";
import { useAccountDetail } from "./useAccountDetail";
import { ACCOUNT_TYPE_LABELS } from "./types";
import { HoldingRow } from "../holdings/HoldingRow";
import { AddHoldingModal } from "../holdings/AddHoldingModal";
import { useDeleteHolding } from "../holdings/useHoldings";
import { TransactionList } from "../transactions/TransactionList";
import { useAccountTransactions } from "../transactions/useTransactions";
import { DonutChart } from "../../shared/ui/DonutChart";
import { ValueOverTimeChart } from "../../shared/ui/ValueOverTimeChart";
import { toSlices } from "../../shared/ui/chartColors";
import chartStyles from "../../shared/ui/Charts.module.css";
import styles from "./AccountDetailPage.module.css";

export function AccountDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: account, isLoading } = useAccountDetail(id);
  const { data: transactions = [] } = useAccountTransactions(id);
  const deleteHolding = useDeleteHolding(id!);
  const [isModalOpen, setModalOpen] = useState(false);

  if (isLoading) return <div className={styles.page}>Завантаження…</div>;
  if (!account) return <div className={styles.page}>Рахунок не знайдено.</div>;

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.name}>{account.name}</h1>
        <p className={styles.subtitle}>{ACCOUNT_TYPE_LABELS[account.type]}</p>
        {/* Server-computed: holdings can be denominated differently from the account
            (a USD stock in a UAH brokerage account), so summing them here would add
            USD to UAH and label the result with the account's currency. */}
        <p className={styles.total}>
          {account.totalValue.toLocaleString("uk-UA")} {account.currency}
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

      {/* Colour here follows the slice's rank, not the holding — unlike the dashboard's
          donut, where a type owns its colour. Within one account that is the honest
          encoding: a holding has no colour identity anywhere else in the app to match. */}
      <div className={chartStyles.card}>
        <h2 className={chartStyles.cardTitle}>Розподіл рахунку</h2>
        <DonutChart
          slices={toSlices(account.allocationByHolding)}
          currency={account.currency}
          size="large"
          emptyMessage="Додайте активи з вартістю, щоб побачити розподіл рахунку."
        />
      </div>

      <div className={chartStyles.card}>
        <h2 className={chartStyles.cardTitle}>Динаміка рахунку</h2>
        <ValueOverTimeChart
          data={account.valueHistory}
          currency={account.currency}
          emptyMessage="Онови вартість активів пізніше, щоб побачити динаміку рахунку."
        />
      </div>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <h2 className={styles.sectionTitle}>Транзакції</h2>
        </div>
        {/* Read-only here on purpose: a transaction belongs to one asset, and editing it
            where the asset is only a label invites putting units on the wrong holding.
            The name is a link to the page that can. */}
        <div className={styles.card}>
          <TransactionList
            transactions={transactions}
            showHolding
            emptyMessage="Транзакцій у цьому рахунку ще немає."
          />
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
