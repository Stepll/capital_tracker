import { useQuery } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";

export interface Sector {
  id: string;
  name: string;
}

export function useSectors() {
  return useQuery({
    queryKey: ["sectors"],
    queryFn: async () => (await apiClient.get<Sector[]>("/sectors")).data,
  });
}
