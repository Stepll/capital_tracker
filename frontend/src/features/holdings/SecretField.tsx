import { useState } from "react";
import { revealSecretAttribute } from "./useHoldingDetail";
import styles from "./HoldingDetailPage.module.css";

interface Props {
  holdingId: string;
  fieldKey: string;
  label: string;
  isSet: boolean;
  draftValue: string;
  onDraftChange: (value: string) => void;
  onDelete: () => void;
}

/**
 * A secret field never holds a decrypted value at rest in component state
 * beyond an explicit "show" click — closing/hiding drops it again. Typing a
 * new value here doesn't change anything on the server until Save is
 * pressed, same as the rest of the form.
 */
export function SecretField({ holdingId, fieldKey, label, isSet, draftValue, onDraftChange, onDelete }: Props) {
  const [revealed, setRevealed] = useState<string | null>(null);
  const [isRevealing, setRevealing] = useState(false);

  const handleReveal = async () => {
    setRevealing(true);
    try {
      setRevealed(await revealSecretAttribute(holdingId, fieldKey));
    } finally {
      setRevealing(false);
    }
  };

  return (
    <div className={styles.field}>
      <span>{label}</span>
      {isSet && revealed === null && (
        <div className={styles.secretRow}>
          <span className={styles.secretMask}>••••••••</span>
          <button type="button" className={styles.linkButton} onClick={handleReveal} disabled={isRevealing}>
            {isRevealing ? "…" : "Показати"}
          </button>
          <button type="button" className={styles.linkButtonDanger} onClick={onDelete}>
            Видалити
          </button>
        </div>
      )}
      {isSet && revealed !== null && (
        <div className={styles.secretRow}>
          <span className={styles.secretRevealed}>{revealed}</span>
          <button type="button" className={styles.linkButton} onClick={() => setRevealed(null)}>
            Сховати
          </button>
        </div>
      )}
      <input
        type="text"
        placeholder={isSet ? "Замінити значення…" : "Не вказано"}
        value={draftValue}
        onChange={(e) => onDraftChange(e.target.value)}
        autoComplete="new-password"
      />
    </div>
  );
}
