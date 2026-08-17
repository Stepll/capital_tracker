import { useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { HoldingAnalysisModal } from "../insights/HoldingAnalysisModal";
import { streamHoldingInsight } from "../insights/streamHoldingInsight";
import type { InsightErrorCode, InsightPhase } from "../insights/insightTypes";
import { useHoldingInsights, type AiInsight } from "../insights/useInsights";
import type { HoldingDetail } from "./useHoldingDetail";
import styles from "./HoldingDetailPage.module.css";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

interface Props {
  holding: HoldingDetail;
}

export function HoldingInsightsPanel({ holding }: Props) {
  const queryClient = useQueryClient();
  const { data: insights, isLoading } = useHoldingInsights(holding.id);

  const [isOpen, setOpen] = useState(false);
  const [running, setRunning] = useState(false);
  const [phase, setPhase] = useState<InsightPhase | null>(null);
  const [detail, setDetail] = useState<string | null>(null);
  const [error, setError] = useState<InsightErrorCode | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [shown, setShown] = useState<AiInsight | null>(null);

  const abortRef = useRef<AbortController | null>(null);

  const [latest, ...history] = insights ?? [];

  const cooldownUntil = holding.nextAnalysisAvailableAt;
  const blocked = holding.excludeFromAiAnalysis
    ? "Для цього активу AI-аналіз вимкнено."
    : cooldownUntil
      ? `Наступний аналіз буде доступний ${dateTimeFormatter.format(new Date(cooldownUntil))}.`
      : null;

  // Started from the click rather than a useEffect: under StrictMode an effect fires
  // twice in development, and each run is a paid analysis.
  function start() {
    const controller = new AbortController();
    abortRef.current = controller;

    setOpen(true);
    setRunning(true);
    setShown(null);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);

    void streamHoldingInsight(
      holding.id,
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
        queryClient.invalidateQueries({ queryKey: ["holdings", holding.id, "insights"] });
        // The holding itself carries the cooldown, so it has to be refetched too or the
        // button stays enabled until the next navigation.
        queryClient.invalidateQueries({ queryKey: ["holdings", holding.id] });
      },
      controller.signal,
    );
  }

  function close() {
    // Aborting mid-run cancels the model call server-side; nothing is saved and the
    // cooldown is untouched, which is why the button says "Скасувати аналіз".
    abortRef.current?.abort();
    abortRef.current = null;
    setOpen(false);
    setRunning(false);
  }

  return (
    <section className={styles.insightsPanel}>
      <h2 className={styles.sectionTitle}>AI-аналітика активу</h2>

      <button className={styles.secondaryButton} onClick={start} disabled={running || blocked !== null}>
        {running ? "Аналізуємо…" : "Запустити аналіз"}
      </button>

      {blocked && <p className={styles.hint}>{blocked}</p>}

      {isLoading && <p className={styles.hint}>Завантаження…</p>}

      {!isLoading && !latest && <p className={styles.hint}>Ще не проводили аналіз цього активу.</p>}

      {latest && (
        <button type="button" className={styles.latestInsight} onClick={() => openStored(latest)}>
          <p className={styles.latestInsightLabel}>Останній аналіз</p>
          <p className={styles.insightDate}>{dateTimeFormatter.format(new Date(latest.generatedAt))}</p>
          <p className={styles.insightSummary}>{latest.summary}</p>
        </button>
      )}

      {history.length > 0 && (
        <div className={styles.insightHistory}>
          <p className={styles.subTitle}>Попередні аналізи</p>
          {history.map((insight) => (
            <button
              key={insight.id}
              type="button"
              className={styles.insightHistoryItem}
              onClick={() => openStored(insight)}
            >
              <p className={styles.insightDate}>{dateTimeFormatter.format(new Date(insight.generatedAt))}</p>
              <p className={styles.insightSummary}>{insight.summary}</p>
            </button>
          ))}
        </div>
      )}

      {isOpen && (
        <HoldingAnalysisModal
          analysis={shown}
          phase={phase}
          detail={detail}
          error={error}
          retryAt={retryAt}
          onClose={close}
        />
      )}
    </section>
  );

  /** Reopens a stored analysis in the same modal, with no stream attached. */
  function openStored(insight: AiInsight) {
    setShown(insight);
    setPhase(null);
    setDetail(null);
    setError(null);
    setRetryAt(null);
    setRunning(false);
    setOpen(true);
  }
}
