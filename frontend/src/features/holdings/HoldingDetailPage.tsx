import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useHoldingDetail, useAddValuation } from "./useHoldingDetail";
import { HoldingAttributesSection } from "./HoldingAttributesSection";
import { HoldingInsightsPanel } from "./HoldingInsightsPanel";
import { ValueOverTimeChart } from "../../shared/ui/ValueOverTimeChart";
import chartStyles from "../../shared/ui/Charts.module.css";
import { CURRENCIES } from "../../shared/currencies";
import styles from "./HoldingDetailPage.module.css";

const todayIso = () => new Date().toISOString().slice(0, 10);

export function HoldingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: holding, isLoading } = useHoldingDetail(id);
  const addValuation = useAddValuation(id!);
  const [newValue, setNewValue] = useState("");
  const [valuationDate, setValuationDate] = useState(todayIso());
  // Null means "whatever the holding is already denominated in" — resolved at render,
  // since `holding` isn't available this early (the loading guards come after).
  const [valuationCurrency, setValuationCurrency] = useState<string | null>(null);

  if (isLoading) return <div className={styles.page}>Завантаження…</div>;
  if (!holding) return <div className={styles.page}>Актив не знайдено.</div>;

  const currency = valuationCurrency ?? holding.currency;

  const handleAddValuation = async () => {
    if (!newValue) return;
    await addValuation.mutateAsync({ value: Number(newValue), date: valuationDate, currency });
    setNewValue("");
    setValuationDate(todayIso());
    setValuationCurrency(null);
  };

  const firstValue = holding.valuationHistory[0]?.value;
  const change =
    firstValue && firstValue !== 0 ? ((holding.currentValue - firstValue) / firstValue) * 100 : null;

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to={`/accounts/${holding.accountId}`} className={styles.back}>
          ← {holding.accountName}
        </Link>
        <h1 className={styles.name}>{holding.name}</h1>
        {holding.symbol && (
          <p className={styles.symbol}>
            {holding.symbol}
            {holding.quantity ? ` · ${holding.quantity} од.` : ""}
          </p>
        )}
        <div className={styles.valueRow}>
          <p className={styles.value}>
            {holding.currentValue.toLocaleString("uk-UA")} {holding.currency}
          </p>
          {change !== null && (
            <span className={change >= 0 ? styles.changeUp : styles.changeDown}>
              {change >= 0 ? "+" : ""}
              {change.toFixed(1)}%
            </span>
          )}
        </div>
      </header>

      <div className={styles.contentGrid}>
        <div className={styles.leftColumn}>
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
                type="date"
                className={styles.dateInput}
                value={valuationDate}
                max={todayIso()}
                onChange={(e) => setValuationDate(e.target.value)}
                aria-label="Дата оцінки"
              />
              <input
                type="number"
                min="0"
                step="0.01"
                placeholder={`Нова вартість, ${currency}`}
                value={newValue}
                onChange={(e) => setNewValue(e.target.value)}
              />
              {/* An asset can be denominated differently from its account — a USD stock
                  in a UAH brokerage account — so the currency is explicit rather than
                  inherited, and defaults to the one the holding already uses. */}
              <select
                className={styles.currencySelect}
                value={currency}
                onChange={(e) => setValuationCurrency(e.target.value)}
                aria-label="Валюта оцінки"
              >
                {CURRENCIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
              <button
                className={styles.primaryButton}
                onClick={handleAddValuation}
                disabled={!newValue || addValuation.isPending}
              >
                {addValuation.isPending ? "Зберігаємо…" : "Зберегти"}
              </button>
            </div>
            <p className={styles.hint}>
              {holding.pricingMode === "Automatic"
                ? "Вартість оновлюється щодня автоматично за ринковою ціною. Введене вручну значення закріпить цей день і не буде перезаписане."
                : holding.pricingMode === "NeedsQuantity"
                  ? "Щоб вартість оновлювалася автоматично, вкажіть кількість одиниць у деталях активу нижче."
                  : "Значення на вже наявну дату замінить попереднє — зручно виправити помилку."}
            </p>
          </section>

          <HoldingAttributesSection key={holding.id} holding={holding} />
        </div>

        <div className={styles.rightColumn}>
          <HoldingInsightsPanel holding={holding} />
        </div>
      </div>
    </div>
  );
}
