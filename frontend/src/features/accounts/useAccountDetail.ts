import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { Holding } from "../holdings/types";
import type { Account } from "./types";

export interface AccountDetail extends Account {
  holdings: Holding[];
}

export function useAccountDetail(id: string | undefined) {
  return useQuery({
    queryKey: ["accounts", id],
    queryFn: async () => (await apiClient.get<AccountDetail>(`/accounts/${id}`)).data,
    enabled: id !== undefined,
  });
}
