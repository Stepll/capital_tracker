import { useLatestExchangeRates, useSettings, useUpdateDisplayCurrency } from "./useSettings";
import { useDeleteImportProfile, useImportBatches, useImportProfiles, useUndoImport } from "../transfer/useImport";
import styles from "./SettingsPage.module.css";

const importFormatter = new Intl.DateTimeFormat("uk-UA", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

export function SettingsPage() {
  const { data: settings, isLoading } = useSettings();
  const { data: rates } = useLatestExchangeRates();
  const { data: imports } = useImportBatches();
  const { data: profiles } = useImportProfiles();
  const updateCurrency = useUpdateDisplayCurrency();
  const undoImport = useUndoImport();
  const deleteProfile = useDeleteImportProfile();

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
        {profiles && profiles.length > 0 && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Збережені зіставлення</h2>
            <p className={styles.hint}>
              Виписка з такою самою шапкою впізнається сама — колонки не доведеться розкладати
              вдруге.
            </p>
            <div className={styles.imports}>
              {profiles.map((profile) => (
                <div key={profile.id} className={styles.importRow}>
                  <span className={styles.importName}>{profile.name}</span>
                  <button
                    className={styles.undo}
                    onClick={() => deleteProfile.mutate(profile.id)}
                    disabled={deleteProfile.isPending}
                  >
                    Забути
                  </button>
                </div>
              ))}
            </div>
          </section>
        )}

        {imports && imports.length > 0 && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Імпорти</h2>
            {/* Undo has to stay reachable after the modal is closed, or it is only an undo
                for the thirty seconds you happen to still be looking at it. */}
            <p className={styles.hint}>
              Кожен імпорт можна скасувати — він прибере рівно ті рядки, які додав.
            </p>
            <div className={styles.imports}>
              {imports.map((batch) => (
                <div key={batch.id} className={styles.importRow}>
                  <div className={styles.importInfo}>
                    <span className={styles.importName}>{batch.fileName}</span>
                    <span className={styles.importMeta}>
                      {importFormatter.format(new Date(batch.createdAt))} · +{batch.transactionsCreated} транз. · +
                      {batch.valuationsWritten} оцін.
                    </span>
                  </div>
                  {batch.undoneAt === null ? (
                    <button
                      className={styles.undo}
                      onClick={() => undoImport.mutate(batch.id)}
                      disabled={undoImport.isPending}
                    >
                      Скасувати
                    </button>
                  ) : (
                    <span className={styles.importMeta}>скасовано</span>
                  )}
                </div>
              ))}
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
