import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";

export interface Settings {
  displayCurrency: string;
  availableCurrencies: string[];
}

export interface ExchangeRate {
  currency: string;
  rateToUah: number;
  date: string;
}

const SETTINGS_KEY = ["settings"];

export function useSettings() {
  return useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: async () => (await apiClient.get<Settings>("/settings")).data,
  });
}

export function useUpdateDisplayCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (displayCurrency: string) =>
      (await apiClient.put<Settings>("/settings", { displayCurrency })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SETTINGS_KEY }),
  });
}

export function useLatestExchangeRates() {
  return useQuery({
    queryKey: ["exchange-rates", "latest"],
    queryFn: async () =>
      (await apiClient.get<ExchangeRate[]>("/exchange-rates/latest")).data,
  });
}
