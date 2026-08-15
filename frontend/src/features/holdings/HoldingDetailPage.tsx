import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useHoldingDetail, useAddValuation, useAssignSector } from "./useHoldingDetail";
import { useSectors } from "../sectors/useSectors";
import { useGenerateInsight } from "../insights/useInsights";
import { ValueOverTimeChart } from "../../shared/ui/ValueOverTimeChart";
import chartStyles from "../../shared/ui/Charts.module.css";
import styles from "./HoldingDetailPage.module.css";

export function HoldingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: holding, isLoading } = useHoldingDetail(id);
  const { data: sectors } = useSectors();
  const addValuation = useAddValuation(id!);
  const assignSector = useAssignSector(id!);
  const generateInsight = useGenerateInsight();
  const [newValue, setNewValue] = useState("");
  const [insightMessage, setInsightMessage] = useState<string | null>(null);

  if (isLoading) return <div className={styles.page}>Завантаження…</div>;
  if (!holding) return <div className={styles.page}>Актив не знайдено.</div>;

  const handleAddValuation = async () => {
    if (!newValue) return;
    await addValuation.mutateAsync(Number(newValue));
    setNewValue("");
  };

  const handleGenerateInsight = async () => {
    if (!holding.sectorId) return;
    setInsightMessage(null);
    const insight = await generateInsight.mutateAsync(holding.sectorId);
    setInsightMessage(insight.summary);
  };

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to={`/accounts/${holding.accountId}`} className={styles.back}>
          ← {holding.accountName}
        </Link>
        <h1 className={styles.name}>{holding.name}</h1>
        {holding.symbol && <p className={styles.symbol}>{holding.symbol}</p>}
        <p className={styles.value}>
          {holding.currentValue.toLocaleString("uk-UA")} {holding.currency}
        </p>
      </header>

      <section className={chartStyles.card}>
        <h2 className={chartStyles.cardTitle}>Динаміка вартості</h2>
        <ValueOverTimeChart
          data={holding.valuationHistory}
          currency={holding.currency}
          emptyMessage="Онови вартість активу пізніше, щоб побачити динаміку."
        />
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Оновити вартість</h2>
        <div className={styles.updateRow}>
          <input
            type="number"
            min="0"
            step="0.01"
            placeholder={`Нова вартість, ${holding.currency}`}
            value={newValue}
            onChange={(e) => setNewValue(e.target.value)}
          />
          <button
            className={styles.primaryButton}
            onClick={handleAddValuation}
            disabled={!newValue || addValuation.isPending}
          >
            {addValuation.isPending ? "Зберігаємо…" : "Зберегти"}
          </button>
        </div>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Сектор</h2>
        <select
          className={styles.select}
          value={holding.sectorId ?? ""}
          onChange={(e) => assignSector.mutate(e.target.value || null)}
          disabled={assignSector.isPending}
        >
          <option value="">Не вказано</option>
          {sectors?.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>AI-аналіз</h2>
        <div className={styles.aiRow}>
          <button
            className={styles.secondaryButton}
            onClick={handleGenerateInsight}
            disabled={!holding.sectorId || generateInsight.isPending}
            title={holding.sectorId ? undefined : "Спершу вкажи сектор"}
          >
            {generateInsight.isPending ? "Аналізуємо…" : "Запустити аналіз сектору"}
          </button>
          {!holding.sectorId && <p className={styles.hint}>Спершу вкажи сектор вище.</p>}
        </div>
        {insightMessage && <p className={styles.insightMessage}>{insightMessage}</p>}
      </section>
    </div>
  );
}
