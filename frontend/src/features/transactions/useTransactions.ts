import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { AxiosError } from "axios";
import { apiClient } from "../../shared/api/client";
import type { Transaction, TransactionType } from "./types";

export interface SaveTransactionInput {
  type: TransactionType;
  date: string;
  quantity: number;
  unitPrice: number;
  currency?: string;
  notes?: string | null;
}

export function useHoldingTransactions(holdingId: string | undefined) {
  return useQuery({
    queryKey: ["transactions", "holding", holdingId],
    queryFn: async () => (await apiClient.get<Transaction[]>(`/holdings/${holdingId}/transactions`)).data,
    enabled: holdingId !== undefined,
  });
}

export function useAccountTransactions(accountId: string | undefined) {
  return useQuery({
    queryKey: ["transactions", "account", accountId],
    queryFn: async () => (await apiClient.get<Transaction[]>(`/accounts/${accountId}/transactions`)).data,
    enabled: accountId !== undefined,
  });
}

/**
 * Every write invalidates holdings and accounts as well as the lists: a transaction is
 * where a holding's quantity comes from now, so saving one changes the header of its
 * page and whether the price job will touch it at all.
 */
function useTransactionMutation<TInput>(mutationFn: (input: TInput) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      queryClient.invalidateQueries({ queryKey: ["holdings"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
  });
}

export function useAddTransaction(holdingId: string) {
  return useTransactionMutation<SaveTransactionInput>(async (input) =>
    (await apiClient.post<Transaction>(`/holdings/${holdingId}/transactions`, input)).data,
  );
}

export function useUpdateTransaction(id: string) {
  return useTransactionMutation<SaveTransactionInput>(async (input) =>
    (await apiClient.put<Transaction>(`/transactions/${id}`, input)).data,
  );
}

export function useDeleteTransaction() {
  return useTransactionMutation<string>((id) => apiClient.delete(`/transactions/${id}`));
}

/**
 * The server rejects a write the owner can fix — selling more units than are held — with
 * a 400 whose title is the sentence to show. Anything else is a real failure and gets a
 * generic line, because its message was never written for a person to read.
 */
export function saveErrorMessage(error: unknown): string {
  const problem = (error as AxiosError<{ title?: string }>)?.response;
  if (problem?.status === 400 && problem.data?.title) {
    return problem.data.title;
  }
  return "Не вдалося зберегти транзакцію. Спробуйте ще раз.";
}
