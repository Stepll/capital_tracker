import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";

export interface ValuationPoint {
  date: string;
  value: number;
}

export interface HoldingDetail {
  id: string;
  accountId: string;
  accountName: string;
  name: string;
  symbol: string | null;
  currency: string;
  currentValue: number;
  sectorId: string | null;
  sectorName: string | null;
  createdAt: string;
  valuationHistory: ValuationPoint[];
}

export function useHoldingDetail(id: string | undefined) {
  return useQuery({
    queryKey: ["holdings", id],
    queryFn: async () => (await apiClient.get<HoldingDetail>(`/holdings/${id}`)).data,
    enabled: id !== undefined,
  });
}

export function useAddValuation(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (value: number) =>
      (await apiClient.post<HoldingDetail>(`/holdings/${holdingId}/valuations`, { value })).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["holdings", holdingId] });
      queryClient.invalidateQueries({ queryKey: ["dashboard", "summary"] });
      queryClient.invalidateQueries({ queryKey: ["accounts"] });
    },
  });
}

export function useAssignSector(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (sectorId: string | null) =>
      (await apiClient.put<HoldingDetail>(`/holdings/${holdingId}/sector`, { sectorId })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["holdings", holdingId] }),
  });
}
