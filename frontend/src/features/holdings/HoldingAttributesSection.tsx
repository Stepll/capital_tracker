import { useState } from "react";
import type { HoldingDetail } from "./useHoldingDetail";
import { useDeleteSecretAttribute, useUpdateHoldingDetails } from "./useHoldingDetail";
import { ATTRIBUTE_TEMPLATES, SECRET_ATTRIBUTE_TEMPLATES } from "./attributeTemplates";
import { SecretField } from "./SecretField";
import styles from "./HoldingDetailPage.module.css";

interface CustomRow {
  key: string;
  value: string;
}

function extraEntries(attributes: Record<string, string>, templateKeys: string[]): CustomRow[] {
  return Object.entries(attributes)
    .filter(([key]) => !templateKeys.includes(key))
    .map(([key, value]) => ({ key, value }));
}

interface Props {
  holding: HoldingDetail;
  readOnly?: boolean;
}

export function HoldingAttributesSection({ holding, readOnly = false }: Props) {
  const template = ATTRIBUTE_TEMPLATES[holding.accountType];
  const secretTemplate = SECRET_ATTRIBUTE_TEMPLATES[holding.accountType];
  const templateKeys = template.map((f) => f.key);

  const [quantity, setQuantity] = useState(holding.quantity?.toString() ?? "");
  const [notes, setNotes] = useState(holding.notes ?? "");
  const [attrValues, setAttrValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(template.map((f) => [f.key, holding.attributes[f.key] ?? ""])),
  );
  const [customRows, setCustomRows] = useState<CustomRow[]>(() => extraEntries(holding.attributes, templateKeys));
  const [secretDrafts, setSecretDrafts] = useState<Record<string, string>>({});
  const [customSecretKey, setCustomSecretKey] = useState("");
  const [customSecretValue, setCustomSecretValue] = useState("");
  const [excludeFromAi, setExcludeFromAi] = useState(holding.excludeFromAiAnalysis);

  const updateDetails = useUpdateHoldingDetails(holding.id);
  const deleteSecret = useDeleteSecretAttribute(holding.id);

  const handleSave = async () => {
    const attributes: Record<string, string> = {};
    for (const [key, value] of Object.entries(attrValues)) {
      if (value.trim()) attributes[key] = value.trim();
    }
    for (const row of customRows) {
      if (row.key.trim() && row.value.trim()) attributes[row.key.trim()] = row.value.trim();
    }

    const secretAttributes: Record<string, string> = { ...secretDrafts };
    if (customSecretKey.trim() && customSecretValue.trim()) {
      secretAttributes[customSecretKey.trim()] = customSecretValue.trim();
    }

    await updateDetails.mutateAsync({
      quantity: quantity ? Number(quantity) : null,
      notes: notes.trim() || null,
      attributes,
      secretAttributes: Object.keys(secretAttributes).length > 0 ? secretAttributes : undefined,
      excludeFromAiAnalysis: excludeFromAi,
    });

    setSecretDrafts({});
    setCustomSecretKey("");
    setCustomSecretValue("");
  };

  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>Деталі активу</h2>

      {/* A disabled fieldset switches off every control inside it in one go — cheaper and
          harder to get wrong than threading `disabled` through each input. */}
      <fieldset className={styles.fieldset} disabled={readOnly}>

      <label className={styles.field}>
        <span>Кількість одиниць</span>
        <input
          type="number"
          min="0"
          step="any"
          placeholder="напр. 10 (акцій), 0.5 (BTC)"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
        />
        {holding.pricingMode === "NeedsQuantity" && (
          <em className={styles.fieldHint}>
            Вкажіть кількість — тоді вартість оновлюватиметься щодня автоматично.
          </em>
        )}
      </label>

      {template.map((f) => (
        <label key={f.key} className={styles.field}>
          <span>{f.label}</span>
          <input
            type={f.type}
            value={attrValues[f.key] ?? ""}
            onChange={(e) => setAttrValues((prev) => ({ ...prev, [f.key]: e.target.value }))}
          />
        </label>
      ))}

      {customRows.map((row, i) => (
        <div key={i} className={styles.customRow}>
          <input
            placeholder="Назва поля"
            value={row.key}
            onChange={(e) =>
              setCustomRows((prev) => prev.map((r, idx) => (idx === i ? { ...r, key: e.target.value } : r)))
            }
          />
          <input
            placeholder="Значення"
            value={row.value}
            onChange={(e) =>
              setCustomRows((prev) => prev.map((r, idx) => (idx === i ? { ...r, value: e.target.value } : r)))
            }
          />
          <button
            type="button"
            className={styles.linkButtonDanger}
            onClick={() => setCustomRows((prev) => prev.filter((_, idx) => idx !== i))}
          >
            ✕
          </button>
        </div>
      ))}
      <button
        type="button"
        className={styles.linkButton}
        onClick={() => setCustomRows((prev) => [...prev, { key: "", value: "" }])}
      >
        + Додати своє поле
      </button>

      <label className={styles.field}>
        <span>Нотатки</span>
        <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </label>

      {(secretTemplate.length > 0 || holding.secretAttributeKeys.length > 0) && (
        <>
          <h3 className={styles.subTitle}>Секретні дані</h3>
          {secretTemplate.map((f) => (
            <SecretField
              key={f.key}
              holdingId={holding.id}
              fieldKey={f.key}
              label={f.label}
              isSet={holding.secretAttributeKeys.includes(f.key)}
              draftValue={secretDrafts[f.key] ?? ""}
              onDraftChange={(v) => setSecretDrafts((prev) => ({ ...prev, [f.key]: v }))}
              onDelete={() => deleteSecret.mutate(f.key)}
            />
          ))}
          {holding.secretAttributeKeys
            .filter((key) => !secretTemplate.some((f) => f.key === key))
            .map((key) => (
              <SecretField
                key={key}
                holdingId={holding.id}
                fieldKey={key}
                label={key}
                isSet
                draftValue={secretDrafts[key] ?? ""}
                onDraftChange={(v) => setSecretDrafts((prev) => ({ ...prev, [key]: v }))}
                onDelete={() => deleteSecret.mutate(key)}
              />
            ))}
          <div className={styles.customRow}>
            <input
              placeholder="Назва (напр. PIN)"
              value={customSecretKey}
              onChange={(e) => setCustomSecretKey(e.target.value)}
            />
            <input
              placeholder="Значення"
              type="password"
              autoComplete="new-password"
              value={customSecretValue}
              onChange={(e) => setCustomSecretValue(e.target.value)}
            />
          </div>
        </>
      )}

      <label className={styles.checkboxField}>
        <input type="checkbox" checked={excludeFromAi} onChange={(e) => setExcludeFromAi(e.target.checked)} />
        <span>
          Не аналізувати через AI
          <em>Поля вище й нотатки не надсилатимуться до сторонньої моделі та у веб-пошук.</em>
        </span>
      </label>

      {!readOnly && (
        <button className={styles.primaryButton} onClick={handleSave} disabled={updateDetails.isPending}>
          {updateDetails.isPending ? "Зберігаємо…" : "Зберегти деталі"}
        </button>
      )}
      </fieldset>
    </section>
  );
}
