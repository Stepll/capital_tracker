import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { HoldingAnalysisModal } from "./HoldingAnalysisModal";
import {
  streamMarketInsight,
  streamPortfolioInsight,
  type InsightStreamEvent,
} from "./streamHoldingInsight";
import type { InsightErrorCode, InsightPhase } from "./insightTypes";
import { useInsights, type AiInsight, type InsightScope } from "./useInsights";
import styles from "./InsightsPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

/** What each scope is called in the archive, and what the modal is titled. */
const SCOPE_LABELS: Record<InsightScope, string> = {
  Holding: "Актив",
  Portfolio: "Портфель",
  MarketUkraine: "Ринок України",
  MarketGlobal: "Світовий ринок",
};

const MODAL_TITLES: Record<InsightScope, string> = {
  Holding: "AI-аналіз активу",
  Portfolio: "AI-аналіз портфеля",
  MarketUkraine: "AI-огляд ринку України",
  MarketGlobal: "AI-огляд світового ринку",
};

/** Which button is mid-run — only that one says so. */
type RunKind = "Portfolio" | "MarketUkraine" | "MarketGlobal";

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
  const [phase, setPhase] = useState<InsightPhase | null>(null);
  const [detail, setDetail] = useState<string | null>(null);
  const [error, setError] = useState<InsightErrorCode | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [shown, setShown] = useState<AiInsight | null>(null);
  const [title, setTitle] = useState(MODAL_TITLES.Portfolio);
  const [running, setRunningKind] = useState<RunKind | null>(null);

  const abortRef = useRef<AbortController | null>(null);

  // Started from the click rather than a useEffect: under StrictMode an effect fires
  // twice in development, and each run is a paid analysis.
  function start(kind: RunKind) {
    const controller = new AbortController();
    abortRef.current = controller;

    setTitle(MODAL_TITLES[kind]);
    setOpen(true);
    setRunningKind(kind);
    setShown(null);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);

    const onEvent = (event: InsightStreamEvent) => {
      if (event.type === "Phase") {
        setPhase(event.phase);
        setDetail(event.detail);
        return;
      }

      setRunningKind(null);

      if (event.type === "Failed") {
        setError(event.errorCode);
        setRetryAt(event.retryAt);
        return;
      }

      setShown(event.insight);
      queryClient.invalidateQueries({ queryKey: ["insights"] });
    };

    void (kind === "Portfolio"
      ? streamPortfolioInsight(onEvent, controller.signal)
      : streamMarketInsight(
          kind === "MarketUkraine" ? "ukraine" : "global",
          onEvent,
          controller.signal,
        ));
  }

  function close() {
    // Aborting mid-run cancels the model call server-side; nothing is saved and the
    // cooldown is untouched.
    abortRef.current?.abort();
    abortRef.current = null;
    setOpen(false);
    setRunningKind(null);
  }

  /** Reopens a stored analysis in the same modal, with no stream attached. */
  function openStored(insight: AiInsight) {
    setTitle(MODAL_TITLES[insight.scope]);
    setShown(insight);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);
    setRunningKind(null);
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
          Аналіз портфеля цілком, огляд ринків і архів усіх запусків. Аналізи видалених активів
          лишаються тут — за кожен уже заплачено.
        </p>
      </header>

      <section className={styles.actions}>
        <button
          className={styles.primaryAction}
          onClick={() => start("Portfolio")}
          disabled={running !== null}
        >
          {running === "Portfolio" ? "Аналізуємо…" : "Аналіз портфеля"}
        </button>
        <button
          className={styles.secondaryAction}
          onClick={() => start("MarketUkraine")}
          disabled={running !== null}
        >
          {running === "MarketUkraine" ? "Досліджуємо…" : "Ринок України"}
        </button>
        <button
          className={styles.secondaryAction}
          onClick={() => start("MarketGlobal")}
          disabled={running !== null}
        >
          {running === "MarketGlobal" ? "Досліджуємо…" : "Світовий ринок"}
        </button>
      </section>

      <section className={styles.feed}>
        {isLoading && <p className={styles.hint}>Завантаження…</p>}

        {!isLoading && insights?.length === 0 && (
          <p className={styles.hint}>
            Ще немає жодного аналізу — запусти щось із кнопок вище або відкрий будь-який актив.
          </p>
        )}

        <div className={styles.list}>
          {insights?.map((insight) => (
            <article key={insight.id} className={styles.card}>
              <div className={styles.cardHeader}>
                <span className={styles.cardSubject}>
                  {insight.scope === "Holding"
                    ? (insight.holdingName ?? SCOPE_LABELS.Holding)
                    : SCOPE_LABELS[insight.scope]}
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
