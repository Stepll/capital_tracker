import type { HoldingDetail } from "./useHoldingDetail";
import styles from "./HoldingDetailPage.module.css";

const money = (value: number, currency: string) =>
  `${value.toLocaleString("uk-UA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;

/** Sign first, colour second — the number reads the same without the colour. */
function Signed({ value, currency }: { value: number; currency: string }) {
  if (value === 0) return <span className={styles.returnNeutral}>{money(0, currency)}</span>;

  return (
    <span className={value > 0 ? styles.returnUp : styles.returnDown}>
      {value > 0 ? "+" : "−"}
      {money(Math.abs(value), currency)}
    </span>
  );
}

interface Props {
  holding: HoldingDetail;
}

/**
 * What the asset earned, next to what it is worth. Rows that are zero are left out — a
 * holding nobody has sold has no realised result to report, and printing a row of zeros
 * makes the two numbers that matter harder to find.
 */
export function HoldingReturnSection({ holding }: Props) {
  const result = holding.return;
  const currency = holding.currency;

  // Nothing was ever bought — an asset carried purely as valuations has no cost to compare.
  if (result.invested === 0) return null;

  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>Результат</h2>

      <dl className={styles.returnGrid}>
        <dt>Вкладено</dt>
        <dd>{money(result.invested, currency)}</dd>

        {result.costBasis > 0 && (
          <>
            <dt>Собівартість того, що лишилось</dt>
            <dd>{money(result.costBasis, currency)}</dd>
          </>
        )}

        {result.unrealised !== 0 && (
          <>
            <dt>Нереалізований</dt>
            <dd>
              <Signed value={result.unrealised} currency={currency} />
            </dd>
          </>
        )}

        {result.realised !== 0 && (
          <>
            <dt>Реалізований</dt>
            <dd>
              <Signed value={result.realised} currency={currency} />
            </dd>
          </>
        )}

        {result.income !== 0 && (
          <>
            <dt>Дивіденди й оренда</dt>
            <dd>
              <Signed value={result.income} currency={currency} />
            </dd>
          </>
        )}

        <dt className={styles.returnTotalLabel}>Разом</dt>
        <dd className={styles.returnTotalValue}>
          <Signed value={result.total} currency={currency} />
          {result.totalPercent !== null && (
            <span className={styles.returnPercent}>
              {result.totalPercent > 0 ? "+" : ""}
              {result.totalPercent.toLocaleString("uk-UA", { maximumFractionDigits: 1 })}%
            </span>
          )}
        </dd>
      </dl>

      <p className={styles.hint}>
        Собівартість — за середньою ціною купівлі. Покупки в іншій валюті переведені курсом на
        дату угоди.
      </p>
    </section>
  );
}
