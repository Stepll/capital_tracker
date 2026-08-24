import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { AccountType } from "../accounts/types";
import type { ValuationAge } from "../../shared/ui/valuationAge";

export interface AllocationItem {
  type: AccountType;
  value: number;
}

export interface NetWorthPoint {
  date: string;
  value: number;
}

export interface StaleValuation {
  holdingId: string;
  name: string;
  accountName: string;
  valuationAge: ValuationAge;
  /** Already in the display currency, so it can be compared against the total directly. */
  valueInDisplayCurrency: number;
}

export interface DashboardSummary {
  totalNetWorth: number;
  currency: string;
  allocationByType: AllocationItem[];
  netWorthHistory: NetWorthPoint[];
  staleValuations: StaleValuation[];
}

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: async () => (await apiClient.get<DashboardSummary>("/dashboard/summary")).data,
  });
}
