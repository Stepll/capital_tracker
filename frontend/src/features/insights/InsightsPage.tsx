import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { HoldingAnalysisModal } from "./HoldingAnalysisModal";
import { streamPortfolioInsight } from "./streamHoldingInsight";
import type { InsightErrorCode, InsightPhase } from "./insightTypes";
import { useInsights, type AiInsight } from "./useInsights";
import styles from "./InsightsPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

const PORTFOLIO_TITLE = "AI-аналіз портфеля";

function factCount(count: number) {
  const lastTwo = count % 100;
  const last = count % 10;
  if (lastTwo >= 11 && lastTwo <= 14) return `${count} фактів`;
  if (last === 1) return `${count} факт`;
  if (last >= 2 && last <= 4) return `${count} факти`;
  return `${count} фактів`;
}

export function InsightsPage() {
  const queryClient = useQueryClient();
  const { data: insights, isLoading } = useInsights();

  const [isOpen, setOpen] = useState(false);
  const [running, setRunning] = useState(false);
  const [phase, setPhase] = useState<InsightPhase | null>(null);
  const [detail, setDetail] = useState<string | null>(null);
  const [error, setError] = useState<InsightErrorCode | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [shown, setShown] = useState<AiInsight | null>(null);
  const [title, setTitle] = useState(PORTFOLIO_TITLE);

  const abortRef = useRef<AbortController | null>(null);

  // Started from the click rather than a useEffect: under StrictMode an effect fires
  // twice in development, and each run is a paid analysis.
  function start() {
    const controller = new AbortController();
    abortRef.current = controller;

    setTitle(PORTFOLIO_TITLE);
    setOpen(true);
    setRunning(true);
    setShown(null);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);

    void streamPortfolioInsight(
      (event) => {
        if (event.type === "Phase") {
          setPhase(event.phase);
          setDetail(event.detail);
          return;
        }

        setRunning(false);

        if (event.type === "Failed") {
          setError(event.errorCode);
          setRetryAt(event.retryAt);
          return;
        }

        setShown(event.insight);
        queryClient.invalidateQueries({ queryKey: ["insights"] });
      },
      controller.signal,
    );
  }

  function close() {
    // Aborting mid-run cancels the model call server-side; nothing is saved and the
    // cooldown is untouched.
    abortRef.current?.abort();
    abortRef.current = null;
    setOpen(false);
    setRunning(false);
  }

  /** Reopens a stored analysis in the same modal, with no stream attached. */
  function openStored(insight: AiInsight) {
    setTitle(insight.scope === "Portfolio" ? PORTFOLIO_TITLE : "AI-аналіз активу");
    setShown(insight);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);
    setRunning(false);
    setOpen(true);
  }

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to="/" className={styles.back}>
          ← Дашборд
        </Link>
        <h1 className={styles.title}>AI-аналітика</h1>
        <p className={styles.subtitle}>
          Аналіз портфеля цілком і архів усіх запусків. Аналізи видалених активів лишаються тут —
          за кожен уже заплачено.
        </p>
      </header>

      <section className={styles.actions}>
        <button className={styles.primaryAction} onClick={start} disabled={running}>
          {running ? "Аналізуємо…" : "Аналіз портфеля"}
        </button>
        {/* Deliberately inert, not wired to anything: the previous stub on this page
            wrote real rows into the feed, and that is exactly what leaked. */}
        <button className={styles.secondaryAction} disabled title="Скоро">
          Ринок України
        </button>
        <button className={styles.secondaryAction} disabled title="Скоро">
          Світовий ринок
        </button>
      </section>

      <section className={styles.feed}>
        {isLoading && <p className={styles.hint}>Завантаження…</p>}

        {!isLoading && insights?.length === 0 && (
          <p className={styles.hint}>
            Ще немає жодного аналізу — запусти аналіз портфеля вище або відкрий будь-який актив.
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

              <button type="button" className={styles.cardBody} onClick={() => openStored(insight)}>
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

      {isOpen && (
        <HoldingAnalysisModal
          analysis={shown}
          title={title}
          phase={phase}
          detail={detail}
          error={error}
          retryAt={retryAt}
          onClose={close}
        />
      )}
    </div>
  );
}
