import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import type { AccountType } from "../accounts/types";

export interface ValuationPoint {
  date: string;
  value: number;
}

export interface HoldingDetail {
  id: string;
  accountId: string;
  accountName: string;
  accountType: AccountType;
  name: string;
  symbol: string | null;
  quantity: number | null;
  notes: string | null;
  currency: string;
  currentValue: number;
  sectorId: string | null;
  sectorName: string | null;
  createdAt: string;
  valuationHistory: ValuationPoint[];
  attributes: Record<string, string>;
  secretAttributeKeys: string[];
}

export function useHoldingDetail(id: string | undefined) {
  return useQuery({
    queryKey: ["holdings", id],
    queryFn: async () => (await apiClient.get<HoldingDetail>(`/holdings/${id}`)).data,
    enabled: id !== undefined,
  });
}

export interface AddValuationInput {
  value: number;
  date?: string;
}

export function useAddValuation(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: AddValuationInput) =>
      (await apiClient.post<HoldingDetail>(`/holdings/${holdingId}/valuations`, input)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["holdings", holdingId] });
      queryClient.invalidateQueries({ queryKey: ["dashboard", "summary"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
  });
}

export interface UpdateHoldingDetailsInput {
  quantity: number | null;
  notes: string | null;
  attributes?: Record<string, string>;
  secretAttributes?: Record<string, string>;
}

export function useUpdateHoldingDetails(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: UpdateHoldingDetailsInput) =>
      (await apiClient.put<HoldingDetail>(`/holdings/${holdingId}/details`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["holdings", holdingId] }),
  });
}

export function useDeleteSecretAttribute(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (key: string) =>
      (await apiClient.delete<HoldingDetail>(`/holdings/${holdingId}/secrets/${encodeURIComponent(key)}`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["holdings", holdingId] }),
  });
}

/** Not a useQuery — a deliberate one-shot fetch triggered by a "show" click, never cached. */
export async function revealSecretAttribute(holdingId: string, key: string): Promise<string> {
  const { data } = await apiClient.get<{ value: string }>(
    `/holdings/${holdingId}/secrets/${encodeURIComponent(key)}`,
  );
  return data.value;
}
