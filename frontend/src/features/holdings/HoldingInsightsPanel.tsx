import { useHoldingInsights, useGenerateHoldingInsight } from "../insights/useInsights";
import styles from "./HoldingDetailPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

interface Props {
  holdingId: string;
}

export function HoldingInsightsPanel({ holdingId }: Props) {
  const { data: insights, isLoading } = useHoldingInsights(holdingId);
  const generate = useGenerateHoldingInsight(holdingId);

  const [latest, ...history] = insights ?? [];

  return (
    <section className={styles.insightsPanel}>
      <h2 className={styles.sectionTitle}>AI-аналітика активу</h2>

      <button
        className={styles.secondaryButton}
        onClick={() => generate.mutate()}
        disabled={generate.isPending}
      >
        {generate.isPending ? "Аналізуємо…" : "Запустити аналіз"}
      </button>

      {isLoading && <p className={styles.hint}>Завантаження…</p>}

      {!isLoading && !latest && (
        <p className={styles.hint}>Ще не проводили аналіз цього активу.</p>
      )}

      {latest && (
        <div className={styles.latestInsight}>
          <p className={styles.latestInsightLabel}>Останній аналіз</p>
          <p className={styles.insightDate}>{dateTimeFormatter.format(new Date(latest.generatedAt))}</p>
          <p className={styles.insightSummary}>{latest.summary}</p>
        </div>
      )}

      {history.length > 0 && (
        <div className={styles.insightHistory}>
          <p className={styles.subTitle}>Попередні аналізи</p>
          {history.map((insight) => (
            <div key={insight.id} className={styles.insightHistoryItem}>
              <p className={styles.insightDate}>{dateTimeFormatter.format(new Date(insight.generatedAt))}</p>
              <p className={styles.insightSummary}>{insight.summary}</p>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
