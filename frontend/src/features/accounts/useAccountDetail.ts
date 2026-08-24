import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { Holding } from "../holdings/types";
import type { Account } from "./types";

export interface AccountAllocationItem {
  holdingId: string;
  name: string;
  /** Already converted into the account's currency server-side. */
  value: number;
}

export interface AccountValuePoint {
  date: string;
  value: number;
}

export interface AccountDetail extends Account {
  holdings: Holding[];
  allocationByHolding: AccountAllocationItem[];
  valueHistory: AccountValuePoint[];
}

export function useAccountDetail(id: string | undefined) {
  return useQuery({
    queryKey: ["accounts", id],
    queryFn: async () => (await apiClient.get<AccountDetail>(`/accounts/${id}`)).data,
    enabled: id !== undefined,
  });
}
