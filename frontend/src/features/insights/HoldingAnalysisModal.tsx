import modalStyles from "../../shared/ui/Modal.module.css";
import styles from "./HoldingAnalysisModal.module.css";
import {
  CONFIDENCE_LABELS,
  CONFIDENCE_LEVEL,
  ERROR_LABELS,
  FACT_CATEGORY_LABELS,
  PHASE_LABELS,
  POLARITY_GLYPH,
  POLARITY_LABELS,
  safeHttpUrl,
  type AnalysisFact,
  type InsightErrorCode,
  type InsightPhase,
} from "./insightTypes";
import type { AiInsight } from "./useInsights";

const dateTimeFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

const dateFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  year: "numeric",
});

/** Shown in order; the active one and everything before it are marked as reached. */
const PHASE_ORDER: InsightPhase[] = ["Preparing", "MarketData", "Searching", "Thinking", "Writing", "Saving"];

interface Props {
  /** Present once the analysis is done, or when reopening one from history. */
  analysis: AiInsight | null;
  phase: InsightPhase | null;
  detail: string | null;
  error: InsightErrorCode | null;
  retryAt: string | null;
  onClose: () => void;
}

export function HoldingAnalysisModal({ analysis, phase, detail, error, retryAt, onClose }: Props) {
  const isRunning = analysis === null && error === null;

  return (
    <div className={modalStyles.overlay} onClick={onClose}>
      <div
        className={`${modalStyles.modal} ${modalStyles.modalWide}`}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="AI-аналіз активу"
      >
        <div className={styles.header}>
          <h2 className={modalStyles.title}>AI-аналіз активу</h2>
          {analysis && (
            <span className={styles.date}>{dateTimeFormatter.format(new Date(analysis.generatedAt))}</span>
          )}
        </div>

        {isRunning && <Progress phase={phase} detail={detail} />}

        {error && <ErrorMessage code={error} retryAt={retryAt} />}

        {analysis && (
          <>
            <p className={styles.summary}>{analysis.summary}</p>

            {analysis.facts.length > 0 ? (
              <>
                <span className={styles.sectionLabel}>Факти</span>
                <div className={styles.facts}>
                  {analysis.facts.map((fact, index) => (
                    <FactCard key={`${fact.claim}-${index}`} fact={fact} />
                  ))}
                </div>
              </>
            ) : (
              <p className={styles.empty}>
                Конкретних фактів знайти не вдалося — по цьому активу мало публічної інформації.
              </p>
            )}

            <p className={styles.disclaimer}>
              Згенеровано AI на основі відкритих джерел. Може містити помилки — перевіряйте важливі
              факти самостійно. Це не інвестиційна рекомендація.
            </p>
          </>
        )}

        <button type="button" className={styles.close} onClick={onClose}>
          {isRunning ? "Скасувати аналіз" : "Закрити"}
        </button>
      </div>
    </div>
  );
}

function Progress({ phase, detail }: { phase: InsightPhase | null; detail: string | null }) {
  const currentIndex = phase ? PHASE_ORDER.indexOf(phase) : -1;

  return (
    <div className={styles.progress}>
      {PHASE_ORDER.map((step, index) => {
        const state =
          index === currentIndex ? styles.phaseActive : index < currentIndex ? styles.phaseDone : "";
        return (
          <div key={step}>
            <div className={`${styles.phase} ${state}`}>
              <span className={styles.phaseMarker} />
              {PHASE_LABELS[step]}
            </div>
            {index === currentIndex && detail && <div className={styles.detail}>{detail}</div>}
          </div>
        );
      })}
    </div>
  );
}

function ErrorMessage({ code, retryAt }: { code: InsightErrorCode; retryAt: string | null }) {
  const when = retryAt ? dateTimeFormatter.format(new Date(retryAt)) : null;

  return (
    <p className={styles.error}>
      {ERROR_LABELS[code]}
      {code === "Cooldown" && when && ` Наступний аналіз буде доступний ${when}.`}
    </p>
  );
}

function FactCard({ fact }: { fact: AnalysisFact }) {
  const polarityClass =
    fact.polarity === "Positive" ? styles.positive : fact.polarity === "Negative" ? styles.negative : styles.neutral;

  const href = safeHttpUrl(fact.sourceUrl);
  const filled = CONFIDENCE_LEVEL[fact.confidence];

  return (
    <article className={`${styles.fact} ${polarityClass}`}>
      <div className={styles.factHeader}>
        <span className={styles.glyph} title={POLARITY_LABELS[fact.polarity]} aria-label={POLARITY_LABELS[fact.polarity]}>
          {POLARITY_GLYPH[fact.polarity]}
        </span>
        <span className={styles.category}>{FACT_CATEGORY_LABELS[fact.category]}</span>
        {fact.isNew && <span className={styles.new}>Нове</span>}
        <span className={styles.confidence} title={CONFIDENCE_LABELS[fact.confidence]} aria-label={CONFIDENCE_LABELS[fact.confidence]}>
          {[1, 2, 3].map((level) => (
            <span key={level} className={`${styles.bar} ${level <= filled ? styles.barFilled : ""}`} />
          ))}
        </span>
      </div>

      <p className={styles.claim}>{fact.claim}</p>

      {(fact.sourceName || href || fact.sourceDate) && (
        <div className={styles.meta}>
          {href ? (
            <a href={href} target="_blank" rel="noopener noreferrer">
              {fact.sourceName ?? "Джерело"}
            </a>
          ) : (
            fact.sourceName && <span>{fact.sourceName}</span>
          )}
          {fact.sourceDate && <span>· {dateFormatter.format(new Date(fact.sourceDate))}</span>}
        </div>
      )}
    </article>
  );
}
