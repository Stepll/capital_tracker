export interface Holding {
  id: string;
  accountId: string;
  name: string;
  symbol: string | null;
  currency: string;
  currentValue: number;
  createdAt: string;
}
