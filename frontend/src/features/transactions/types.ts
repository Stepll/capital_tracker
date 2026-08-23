export type TransactionType =
  | "Buy"
  | "Sell"
  | "Dividend"
  | "Rent"
  | "Expense"
  | "Deposit"
  | "Withdrawal";

export interface Transaction {
  id: string;
  holdingId: string;
  /** Filled on every row so the account page can list several holdings in one stream. */
  holdingName: string;
  type: TransactionType;
  date: string;
  quantity: number;
  unitPrice: number;
  /** quantity × unitPrice, computed server-side. */
  amount: number;
  currency: string;
  notes: string | null;
}

export const TRANSACTION_TYPE_LABELS: Record<TransactionType, string> = {
  Buy: "Купівля",
  Sell: "Продаж",
  Dividend: "Дивіденди",
  Rent: "Оренда",
  Expense: "Витрата",
  Deposit: "Внесення",
  Withdrawal: "Виведення",
};

export const TRANSACTION_TYPES = Object.keys(TRANSACTION_TYPE_LABELS) as TransactionType[];

/**
 * What each type does to the position, mirroring HoldingPositions.Direction on the
 * server — which stays the authority. This copy exists only so the form can ask for a
 * quantity where one is meaningful, and the list can put a sign in front of it.
 */
export const TRANSACTION_DIRECTION: Record<TransactionType, 1 | -1 | 0> = {
  Buy: 1,
  Deposit: 1,
  Sell: -1,
  Withdrawal: -1,
  Dividend: 0,
  Rent: 0,
  Expense: 0,
};

/** Cash flows carry an amount but no units, so their form asks for one number, not two. */
export const movesUnits = (type: TransactionType) => TRANSACTION_DIRECTION[type] !== 0;
