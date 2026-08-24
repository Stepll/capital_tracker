import { useLatestExchangeRates, useSettings, useUpdateDisplayCurrency } from "./useSettings";
import styles from "./SettingsPage.module.css";

export function SettingsPage() {
  const { data: settings, isLoading } = useSettings();
  const { data: rates } = useLatestExchangeRates();
  const updateCurrency = useUpdateDisplayCurrency();

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Налаштування</h1>
      </header>

      <div className={styles.sections}>
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Валюта відображення</h2>
        <p className={styles.hint}>
          У цій валюті буде показано загальний капітал і графіки на дашборді.
        </p>

        {!isLoading && settings && (
          <div className={styles.currencyOptions}>
            {settings.availableCurrencies.map((currency) => (
              <button
                key={currency}
                className={
                  currency === settings.displayCurrency
                    ? `${styles.currencyOption} ${styles.currencyOptionActive}`
                    : styles.currencyOption
                }
                disabled={updateCurrency.isPending}
                onClick={() => updateCurrency.mutate(currency)}
              >
                {currency}
              </button>
            ))}
          </div>
        )}

      </section>

        {rates && rates.length > 0 && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Поточні курси НБУ</h2>
            <p className={styles.hint}>Оновлюються щодня; за ними перераховуються всі суми.</p>
            <div className={styles.rates}>
              {rates.map((r) => (
                <div key={r.currency} className={styles.rateRow}>
                  <span>1 {r.currency}</span>
                  <span>{r.rateToUah.toFixed(2)} UAH</span>
                </div>
              ))}
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
