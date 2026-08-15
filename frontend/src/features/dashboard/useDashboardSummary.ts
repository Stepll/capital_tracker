import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { AccountType } from "../accounts/types";

export interface AllocationItem {
  type: AccountType;
  value: number;
}

export interface NetWorthPoint {
  date: string;
  value: number;
}

export interface DashboardSummary {
  totalNetWorth: number;
  currency: string;
  allocationByType: AllocationItem[];
  netWorthHistory: NetWorthPoint[];
}

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: async () => (await apiClient.get<DashboardSummary>("/dashboard/summary")).data,
  });
}
