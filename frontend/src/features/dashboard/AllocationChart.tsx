import { ACCOUNT_TYPE_COLORS } from "../accounts/accountTypeColors";
import { ACCOUNT_TYPE_LABELS } from "../accounts/types";
import { DonutChart } from "../../shared/ui/DonutChart";
import type { AllocationItem } from "./useDashboardSummary";

interface Props {
  data: AllocationItem[];
  currency: string;
  size?: "normal" | "large";
}

/**
 * Capital by account type. Unlike the account page's donut, the colour here belongs to
 * the entity rather than to its rank — a type keeps its own colour whether or not it is
 * the biggest slice, and shares it with the account cards in the list.
 */
export function AllocationChart({ data, currency, size = "normal" }: Props) {
  const slices = data.map((item) => ({
    name: ACCOUNT_TYPE_LABELS[item.type],
    value: item.value,
    color: ACCOUNT_TYPE_COLORS[item.type],
  }));

  return <DonutChart slices={slices} currency={currency} size={size} />;
}
