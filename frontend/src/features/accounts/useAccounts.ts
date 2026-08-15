import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { Account, AccountType } from "./types";

const ACCOUNTS_KEY = ["accounts"];

export function useAccounts() {
  return useQuery({
    queryKey: ACCOUNTS_KEY,
    queryFn: async () => (await apiClient.get<Account[]>("/accounts")).data,
  });
}

export interface CreateAccountInput {
  name: string;
  type: AccountType;
  currency: string;
}

export function useCreateAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: CreateAccountInput) =>
      (await apiClient.post<Account>("/accounts", input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ACCOUNTS_KEY }),
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/accounts/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ACCOUNTS_KEY }),
  });
}
