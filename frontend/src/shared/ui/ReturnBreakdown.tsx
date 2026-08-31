import styles from "./ReturnBreakdown.module.css";

export interface InvestmentReturn {
  /** Gross cost of everything ever bought — what the percentage is measured against. */
  invested: number;
  /** What the units still held cost, at their running average price. */
  costBasis: number;
  unrealised: number;
  realised: number;
  /** Dividends and rent, less expenses. */
  income: number;
  total: number;
  /** Null until something has been bought — nothing to divide by. */
  totalPercent: number | null;
}

const money = (value: number, currency: string) =>
  `${value.toLocaleString("uk-UA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;

/** Sign first, colour second — the number reads the same without the colour. */
function Signed({ value, currency }: { value: number; currency: string }) {
  if (value === 0) return <span className={styles.neutral}>{money(0, currency)}</span>;

  return (
    <span className={value > 0 ? styles.up : styles.down}>
      {value > 0 ? "+" : "−"}
      {money(Math.abs(value), currency)}
    </span>
  );
}

interface Props {
  result: InvestmentReturn;
  currency: string;
  /** Explains where the numbers come from; the portfolio and an asset word it differently. */
  hint: string;
}

/**
 * What was earned, as opposed to what is held — the same shape for one asset and for the
 * whole portfolio, because it is the same calculation asked in a different currency.
 * Rows that are zero are left out: a holding nobody has sold has no realised result, and a
 * column of zeros buries the two numbers that matter.
 */
export function ReturnBreakdown({ result, currency, hint }: Props) {
  return (
    <>
      <dl className={styles.grid}>
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

        <dt className={styles.totalLabel}>Разом</dt>
        <dd className={styles.totalValue}>
          <Signed value={result.total} currency={currency} />
          {result.totalPercent !== null && (
            <span className={styles.percent}>
              {result.totalPercent > 0 ? "+" : ""}
              {result.totalPercent.toLocaleString("uk-UA", { maximumFractionDigits: 1 })}%
            </span>
          )}
        </dd>
      </dl>

      <p className={styles.hint}>{hint}</p>
    </>
  );
}
