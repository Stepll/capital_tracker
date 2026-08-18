import { useState } from "react";
import { Link } from "react-router-dom";
import { HoldingAnalysisModal } from "./HoldingAnalysisModal";
import { useInsights, type AiInsight } from "./useInsights";
import styles from "./InsightsPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

function factCount(count: number) {
  const lastTwo = count % 100;
  const last = count % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return `${count} фактів`;
  if (last === 1) return `${count} факт`;
  if (last >= 2 && last <= 4) return `${count} факти`;
  return `${count} фактів`;
}

export function InsightsPage() {
  const { data: insights, isLoading } = useInsights();
  // Reopened in the same modal the live run uses, with no stream attached.
  const [shown, setShown] = useState<AiInsight | null>(null);

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to="/" className={styles.back}>
          ← Дашборд
        </Link>
        <h1 className={styles.title}>AI-аналітика</h1>
        <p className={styles.subtitle}>
          Архів усіх аналізів, які колись запускались. Аналізи видалених активів лишаються тут —
          за кожен уже заплачено.
        </p>
      </header>

      <section className={styles.feed}>
        {isLoading && <p className={styles.hint}>Завантаження…</p>}

        {!isLoading && insights?.length === 0 && (
          <p className={styles.hint}>
            Ще немає жодного аналізу — запусти його на сторінці будь-якого активу.
          </p>
        )}

        <div className={styles.list}>
          {insights?.map((insight) => (
            <article key={insight.id} className={styles.card}>
              <div className={styles.cardHeader}>
                <span className={styles.cardSubject}>
                  {insight.scope === "Portfolio" ? "Портфель" : (insight.holdingName ?? "Актив")}
                  {insight.isHoldingDeleted && <span className={styles.deletedTag}>видалено</span>}
                </span>
                <span className={styles.cardDate}>
                  {dateTimeFormatter.format(new Date(insight.generatedAt))}
                </span>
              </div>

              <button type="button" className={styles.cardBody} onClick={() => setShown(insight)}>
                <p className={styles.cardSummary}>{insight.summary}</p>
                {insight.facts.length > 0 && (
                  <span className={styles.factCount}>{factCount(insight.facts.length)} →</span>
                )}
              </button>

              {insight.holdingId && (
                <Link to={`/holdings/${insight.holdingId}`} className={styles.cardLink}>
                  Сторінка активу
                </Link>
              )}
            </article>
          ))}
        </div>
      </section>

      {shown && (
        <HoldingAnalysisModal
          analysis={shown}
          phase={null}
          detail={null}
          error={null}
          retryAt={null}
          onClose={() => setShown(null)}
        />
      )}
    </div>
  );
}
