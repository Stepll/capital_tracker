import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";

export interface AiInsight {
  id: string;
  sectorId: string;
  sectorName: string;
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
