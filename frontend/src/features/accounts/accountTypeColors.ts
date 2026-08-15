import type { AccountType } from "./types";

// Fixed categorical order, validated for dark-mode CVD/contrast via the
// dataviz skill's palette validator (6 slots, all checks pass against this
// app's --surface). Never cycle or reassign per-render — identity must stay
// stable across the account list and the allocation chart.
export const ACCOUNT_TYPE_COLORS: Record<AccountType, string> = {
  Bank: "#3987e5",
  Brokerage: "#d95926",
  RealEstate: "#199e70",
  Cash: "#c98500",
  Crypto: "#d55181",
  Other: "#008300",
};
