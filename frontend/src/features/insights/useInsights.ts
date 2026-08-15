import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";

export interface AiInsight {
  id: string;
  sectorId: string | null;
  sectorName: string | null;
  holdingId: string | null;
  generatedAt: string;
  summary: string;
  sourceUrls: string[];
}

export function useInsights() {
  return useQuery({
    queryKey: ["insights"],
    queryFn: async () => (await apiClient.get<AiInsight[]>("/insights")).data,
  });
}

export function useGenerateInsight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (sectorId: string) =>
      (await apiClient.post<AiInsight>("/insights/generate", { sectorId })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["insights"] }),
  });
}

export function useHoldingInsights(holdingId: string) {
  return useQuery({
    queryKey: ["holdings", holdingId, "insights"],
    queryFn: async () => (await apiClient.get<AiInsight[]>(`/holdings/${holdingId}/insights`)).data,
  });
}

export function useGenerateHoldingInsight(holdingId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () =>
      (await apiClient.post<AiInsight>(`/holdings/${holdingId}/insights/generate`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["holdings", holdingId, "insights"] }),
  });
}
