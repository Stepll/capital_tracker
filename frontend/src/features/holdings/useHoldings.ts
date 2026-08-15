import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { Holding } from "./types";

export interface CreateHoldingInput {
  accountId: string;
  name: string;
  symbol?: string;
  initialValue: number;
}

export function useCreateHolding() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ accountId, ...body }: CreateHoldingInput) =>
      (await apiClient.post<Holding>(`/accounts/${accountId}/holdings`, body)).data,
    onSuccess: (_data, { accountId }) =>
      queryClient.invalidateQueries({ queryKey: ["accounts", accountId] }),
  });
}

export function useDeleteHolding(accountId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/holdings/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["accounts", accountId] }),
  });
}
