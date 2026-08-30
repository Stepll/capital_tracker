import type { ValuationAge } from "../../shared/ui/valuationAge";

export interface Holding {
  id: string;
  accountId: string;
  name: string;
  symbol: string | null;
  currency: string;
  currentValue: number;
  createdAt: string;
  lastValuedOn: string | null;
  valuationAge: ValuationAge | null;
  /** Set once the position was sold out — from that day the asset is worth nothing. */
  closedOn: string | null;
}
