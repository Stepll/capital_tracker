import { useMemo } from "react";
import modal from "../../shared/ui/Modal.module.css";
import styles from "./ImportModal.module.css";
import {
  EVENT_LABELS,
  MAPPABLE_FIELDS,
  type ColumnMapping,
  type EventSource,
  type FileInspection,
} from "./types";

interface Props {
  inspection: FileInspection;
  mapping: ColumnMapping;
  onChange: (mapping: ColumnMapping) => void;
}

/**
 * Where a foreign statement is told what its columns mean. Everything here is the owner's
 * choice rather than a guess applied silently: the app suggests a header row and a few
 * columns, and this is where that suggestion gets corrected.
 */
export function MappingStep({ inspection, mapping, onChange }: Props) {
  const header = inspection.rows[mapping.headerRow] ?? [];
  const width = useMemo(
    () => inspection.rows.reduce((max, row) => Math.max(max, row.length), 0),
    [inspection.rows],
  );

  const columnLabel = (index: number) => {
    const name = (header[index] ?? "").replace(/\s+/g, " ").trim();
    return name.length > 0 ? `${index + 1}. ${name}` : `${index + 1}. (без назви)`;
  };

  const source: EventSource = mapping.event.column != null ? "column" : mapping.event.fixed != null ? "fixed" : "sign";
  const eventValues = mapping.event.column != null ? inspection.distinctValues[String(mapping.event.column)] ?? [] : [];

  const setEvent = (patch: Partial<ColumnMapping["event"]>) =>
    onChange({ ...mapping, event: { ...mapping.event, ...patch } });

  const chooseSource = (next: EventSource) =>
    onChange({
      ...mapping,
      // Cleared rather than merged: leaving a stale column behind would keep deciding the
      // direction after the owner has said it comes from somewhere else.
      event:
        next === "column"
          ? { column: mapping.event.column ?? 0, values: {} }
          : next === "sign"
            ? { whenPositive: "Внесення", whenNegative: "Виведення" }
            : { fixed: "Купівля" },
    });

  return (
    <>
      {/* The top of the file as it is. The header is rarely the first row — a bank puts its
          letterhead above the table — so it is picked here rather than assumed. */}
      <div className={styles.gridPreview}>
        <table>
          <tbody>
            {inspection.rows.slice(0, 8).map((row, index) => (
              <tr
                key={index}
                className={index === mapping.headerRow ? styles.headerRowActive : undefined}
                onClick={() => onChange({ ...mapping, headerRow: index })}
                title="Зробити цей рядок шапкою"
              >
                <td className={styles.rowNumber}>{index + 1}</td>
                {Array.from({ length: width }, (_, column) => (
                  <td key={column}>{(row[column] ?? "").replace(/\s+/g, " ").slice(0, 18)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className={modal.hint}>Клікни на рядок, який є шапкою таблиці.</p>

      <div className={styles.mapGrid}>
        {MAPPABLE_FIELDS.map((field) => (
          <label key={field} className={styles.mapField}>
            <span>
              {field}
              {field === "Дата" && " *"}
            </span>
            <select
              value={mapping.columns[field] ?? ""}
              onChange={(e) => {
                const columns = { ...mapping.columns };
                if (e.target.value === "") delete columns[field];
                else columns[field] = Number(e.target.value);
                onChange({ ...mapping, columns });
              }}
            >
              <option value="">—</option>
              {Array.from({ length: width }, (_, index) => (
                <option key={index} value={index}>
                  {columnLabel(index)}
                </option>
              ))}
            </select>
          </label>
        ))}
      </div>

      <div className={styles.eventSource}>
        <span className={styles.eventTitle}>Звідки брати подію</span>
        <div className={styles.sourceTabs}>
          {(
            [
              ["column", "З колонки"],
              ["sign", "Зі знаку суми"],
              ["fixed", "Усі рядки однакові"],
            ] as [EventSource, string][]
          ).map(([value, label]) => (
            <button
              key={value}
              type="button"
              className={source === value ? styles.sourceTabActive : styles.sourceTab}
              onClick={() => chooseSource(value)}
            >
              {label}
            </button>
          ))}
        </div>

        {source === "column" && (
          <>
            <select
              className={styles.wideSelect}
              value={mapping.event.column ?? 0}
              onChange={(e) => setEvent({ column: Number(e.target.value), values: {} })}
            >
              {Array.from({ length: width }, (_, index) => (
                <option key={index} value={index}>
                  {columnLabel(index)}
                </option>
              ))}
            </select>

            {eventValues.length === 0 ? (
              <p className={modal.hint}>
                У цій колонці забагато різних значень, щоб зіставляти по одному — схоже, це не
                колонка типу операції.
              </p>
            ) : (
              eventValues.map((value) => (
                <label key={value} className={styles.valueRow}>
                  <span className={styles.rawValue}>{value}</span>
                  <select
                    value={mapping.event.values?.[value.toLowerCase()] ?? ""}
                    onChange={(e) => {
                      const values = { ...(mapping.event.values ?? {}) };
                      if (e.target.value === "") delete values[value.toLowerCase()];
                      else values[value.toLowerCase()] = e.target.value;
                      setEvent({ values });
                    }}
                  >
                    <option value="">не імпортувати</option>
                    {EVENT_LABELS.map((label) => (
                      <option key={label} value={label}>
                        {label}
                      </option>
                    ))}
                  </select>
                </label>
              ))
            )}
          </>
        )}

        {source === "sign" && (
          <div className={styles.signRow}>
            <label className={styles.mapField}>
              <span>Додатна сума</span>
              <select value={mapping.event.whenPositive ?? ""} onChange={(e) => setEvent({ whenPositive: e.target.value })}>
                {EVENT_LABELS.map((label) => (
                  <option key={label} value={label}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label className={styles.mapField}>
              <span>Від'ємна сума</span>
              <select value={mapping.event.whenNegative ?? ""} onChange={(e) => setEvent({ whenNegative: e.target.value })}>
                {EVENT_LABELS.map((label) => (
                  <option key={label} value={label}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}

        {source === "fixed" && (
          <select
            className={styles.wideSelect}
            value={mapping.event.fixed ?? ""}
            onChange={(e) => setEvent({ fixed: e.target.value })}
          >
            {EVENT_LABELS.map((label) => (
              <option key={label} value={label}>
                {label}
              </option>
            ))}
          </select>
        )}
      </div>
    </>
  );
}
