import { useState, type ChangeEvent } from "react";
import modal from "../../shared/ui/Modal.module.css";
import styles from "./ImportModal.module.css";
import { useCommitImport, usePreviewImport, useUndoImport } from "./useImport";
import {
  DEFAULT_IMPORT_OPTIONS,
  type ImportOptions,
  type ImportPreview,
  type ImportPreviewHolding,
  type ImportResult,
  type TransferScope,
} from "./types";

const SCOPE_TITLES: Record<TransferScope, string> = {
  Portfolio: "Імпорт портфеля",
  Account: "Імпорт у рахунок",
  Holding: "Імпорт в актив",
};

const units = (value: number | null) =>
  value === null ? "—" : `${value.toLocaleString("uk-UA", { maximumFractionDigits: 10 })} од.`;

const money = (value: number, currency: string) =>
  `${value.toLocaleString("uk-UA", { maximumFractionDigits: 2 })} ${currency}`;

interface Props {
  scope: TransferScope;
  targetId?: string;
  onClose: () => void;
}

export function ImportModal({ scope, targetId, onClose }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [options, setOptions] = useState<ImportOptions>(DEFAULT_IMPORT_OPTIONS);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);

  const previewImport = usePreviewImport(scope, targetId);
  const commitImport = useCommitImport(scope, targetId);
  const undoImport = useUndoImport();

  const refresh = async (nextFile: File, nextOptions: ImportOptions) => {
    setPreview(await previewImport.mutateAsync({ file: nextFile, options: nextOptions }));
  };

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const chosen = event.target.files?.[0] ?? null;
    setFile(chosen);
    setResult(null);
    setPreview(null);
    if (chosen) await refresh(chosen, options);
  };

  // Every option changes what would happen, so the preview is recomputed rather than
  // leaving the numbers on screen describing a plan that is no longer the one on offer.
  const toggle = async (key: keyof ImportOptions) => {
    const next = { ...options, [key]: !options[key] };
    setOptions(next);
    if (file) await refresh(file, next);
  };

  const handleCommit = async () => {
    if (!file) return;
    setResult(await commitImport.mutateAsync({ file, options }));
  };

  return (
    <div className={modal.overlay} onClick={onClose}>
      <form
        className={`${modal.modal} ${modal.modalWide}`}
        onClick={(e) => e.stopPropagation()}
        onSubmit={(e) => e.preventDefault()}
      >
        <h2 className={modal.title}>{SCOPE_TITLES[scope]}</h2>

        {result === null && (
          <>
            <label className={modal.field}>
              <span>CSV-файл</span>
              <input type="file" accept=".csv,text/csv" onChange={handleFile} />
              <em className={modal.hint}>
                Той самий формат, який віддає «Експорт» — вивантаж, поправ у таблиці й залий назад.
                Кодування UTF-8.
              </em>
            </label>

            {previewImport.isPending && <p className={modal.hint}>Читаємо файл…</p>}

            {preview && <Preview preview={preview} options={options} onToggle={toggle} />}
          </>
        )}

        {result !== null && <Result result={result} onUndo={() => undoImport.mutate(result.batchId)} undone={undoImport.isSuccess} />}

        <div className={modal.actions}>
          <button type="button" className={modal.cancel} onClick={onClose}>
            {result === null ? "Скасувати" : "Закрити"}
          </button>
          {result === null && (
            <button
              type="button"
              className={modal.submit}
              onClick={handleCommit}
              disabled={!preview?.canCommit || commitImport.isPending}
            >
              {commitImport.isPending ? "Імпортуємо…" : "Імпортувати"}
            </button>
          )}
        </div>
      </form>
    </div>
  );
}

function Preview({
  preview,
  options,
  onToggle,
}: {
  preview: ImportPreview;
  options: ImportOptions;
  onToggle: (key: keyof ImportOptions) => void;
}) {
  const needsOpening = preview.holdings.some((h) => h.wouldGoNegative);
  const hasOpeningToReplace = preview.holdings.some((h) => h.hasOpeningPosition);

  return (
    <>
      {preview.sameFileImportedBefore && (
        <p className={styles.warning}>
          ⚠ Цей самий файл уже імпортували{" "}
          {new Date(preview.sameFileImportedBefore.createdAt).toLocaleDateString("uk-UA")}. Рядки, які вже
          є, буде пропущено.
        </p>
      )}

      {preview.accountsToCreate.length > 0 && (
        <p className={modal.hint}>Буде створено рахунки: {preview.accountsToCreate.join(", ")}</p>
      )}

      {/* The diff, not the rows: people check outcomes, and "10 → 18 од." is the outcome. */}
      <div className={styles.table}>
        {preview.holdings.map((holding) => (
          <HoldingRow key={`${holding.accountName}/${holding.name}`} holding={holding} />
        ))}
        {preview.holdings.length === 0 && <p className={modal.hint}>У файлі немає рядків, які можна імпортувати.</p>}
      </div>

      <div className={styles.options}>
        <Option
          checked={options.skipDuplicateRows}
          onChange={() => onToggle("skipDuplicateRows")}
          label="Пропускати рядки, які вже є"
          hint="Знято — той самий рядок додасться ще раз і подвоїть позицію."
        />
        {hasOpeningToReplace && (
          <Option
            checked={options.replaceOpeningPositions}
            onChange={() => onToggle("replaceOpeningPositions")}
            label="Замінити початкову позицію"
            hint="Прибирає рядок «Початкова позиція», щоб справжня історія не порахувалась двічі."
          />
        )}
        {needsOpening && (
          <Option
            checked={options.addMissingOpeningPositions}
            onChange={() => onToggle("addMissingOpeningPositions")}
            label="Добудувати початкову позицію"
            hint="Виписка починається з продажу — додамо купівлю на бракуючі одиниці."
          />
        )}
      </div>

      {preview.problems.length > 0 && (
        <div className={styles.problems}>
          <p className={styles.problemsTitle}>Рядки, які не вдалося прочитати</p>
          {preview.problems.slice(0, 8).map((problem) => (
            <p key={`${problem.line}-${problem.message}`} className={styles.problem}>
              <span className={styles.line}>рядок {problem.line}</span> {problem.message}
            </p>
          ))}
          {preview.problems.length > 8 && (
            <p className={styles.problem}>…і ще {preview.problems.length - 8}</p>
          )}
        </div>
      )}
    </>
  );
}

function HoldingRow({ holding }: { holding: ImportPreviewHolding }) {
  const changesUnits = holding.quantityBefore !== holding.quantityAfter;

  return (
    <div className={styles.row}>
      <div className={styles.subject}>
        <span className={styles.name}>{holding.name}</span>
        {holding.symbol && <span className={styles.symbol}>{holding.symbol}</span>}
        {holding.isNewHolding && <span className={styles.tag}>новий актив</span>}
        {holding.revivesHolding && <span className={styles.tag}>повертається з видалених</span>}
        {holding.wouldGoNegative && <span className={styles.tagAlert}>позиція піде в мінус</span>}
      </div>

      <div className={styles.change}>
        {changesUnits && (
          <span className={styles.units}>
            {units(holding.quantityBefore)} → {units(holding.quantityAfter)}
          </span>
        )}
        {holding.valueBefore !== holding.valueAfter && (
          <span className={styles.value}>
            {money(holding.valueBefore, holding.currency)} → {money(holding.valueAfter, holding.currency)}
          </span>
        )}
        <span className={styles.counts}>
          +{holding.newTransactions} транз. · +{holding.newValuations} оцін.
          {holding.skippedRows > 0 && ` · ${holding.skippedRows} вже є`}
        </span>
      </div>
    </div>
  );
}

function Option({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean;
  onChange: () => void;
  label: string;
  hint: string;
}) {
  return (
    <label className={styles.option}>
      <input type="checkbox" checked={checked} onChange={onChange} />
      <span>
        {label}
        <em>{hint}</em>
      </span>
    </label>
  );
}

function Result({ result, onUndo, undone }: { result: ImportResult; onUndo: () => void; undone: boolean }) {
  const totals = result.preview.holdings.reduce(
    (sum, h) => ({
      transactions: sum.transactions + h.newTransactions,
      valuations: sum.valuations + h.newValuations,
    }),
    { transactions: 0, valuations: 0 },
  );

  return (
    <div className={styles.result}>
      <p className={styles.resultTitle}>
        {undone ? "Імпорт скасовано." : `Готово: +${totals.transactions} транзакцій, +${totals.valuations} оцінок.`}
      </p>
      {!undone && (
        <button type="button" className={styles.undo} onClick={onUndo}>
          Скасувати цей імпорт
        </button>
      )}
    </div>
  );
}
