import { Link } from "react-router-dom";
import { useSectors } from "../sectors/useSectors";
import { useGenerateInsight, useInsights } from "./useInsights";
import styles from "./InsightsPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

export function InsightsPage() {
  const { data: sectors } = useSectors();
  const { data: insights, isLoading } = useInsights();
  const generateInsight = useGenerateInsight();

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to="/" className={styles.back}>
          ← Дашборд
        </Link>
        <h1 className={styles.title}>AI-аналітика</h1>
        <p className={styles.subtitle}>
          Секторальний аналіз портфеля. Функція в розробці — кнопки нижче поки що заглушки.
        </p>
      </header>

      <section className={styles.sectorGrid}>
        {sectors?.map((sector) => (
          <button
            key={sector.id}
            className={styles.sectorButton}
            onClick={() => generateInsight.mutate(sector.id)}
            disabled={generateInsight.isPending}
          >
            {sector.name}
          </button>
        ))}
      </section>

      <section className={styles.feed}>
        <h2 className={styles.feedTitle}>Історія звітів</h2>

        {isLoading && <p className={styles.hint}>Завантаження…</p>}

        {!isLoading && insights?.length === 0 && (
          <p className={styles.hint}>Ще немає жодного звіту — натисни на сектор вище.</p>
        )}

        <div className={styles.list}>
          {insights?.map((insight) => (
            <article key={insight.id} className={styles.card}>
              <div className={styles.cardHeader}>
                <span className={styles.cardSector}>{insight.sectorName}</span>
                <span className={styles.cardDate}>
                  {dateTimeFormatter.format(new Date(insight.generatedAt))}
                </span>
              </div>
              <p className={styles.cardSummary}>{insight.summary}</p>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}
