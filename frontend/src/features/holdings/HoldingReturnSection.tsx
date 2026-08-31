import { ReturnBreakdown } from "../../shared/ui/ReturnBreakdown";
import type { HoldingDetail } from "./useHoldingDetail";
import styles from "./HoldingDetailPage.module.css";

interface Props {
  holding: HoldingDetail;
}

export function HoldingReturnSection({ holding }: Props) {
  // Nothing was ever bought — an asset carried purely as valuations has no cost to compare.
  if (holding.return.invested === 0) return null;

  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>Результат</h2>
      <ReturnBreakdown
        result={holding.return}
        currency={holding.currency}
        hint="Собівартість — за середньою ціною купівлі. Покупки в іншій валюті переведені курсом на дату угоди."
      />
    </section>
  );
}
