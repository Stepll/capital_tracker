export type AccountType = "Brokerage" | "Bank" | "RealEstate" | "Cash" | "Crypto" | "Other";

export interface Account {
  id: string;
  name: string;
  type: AccountType;
  currency: string;
  createdAt: string;
  totalValue: number;
}

export const ACCOUNT_TYPE_LABELS: Record<AccountType, string> = {
  Brokerage: "Брокерський рахунок",
  Bank: "Банківський рахунок",
  RealEstate: "Нерухомість",
  Cash: "Готівка",
  Crypto: "Криптовалюта",
  Other: "Інше",
};
