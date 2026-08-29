import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import {
  transferBasePath,
  type ImportBatch,
  type ImportOptions,
  type ImportPreview,
  type ImportResult,
  type TransferScope,
} from "./types";

function body(file: File, options: ImportOptions) {
  const form = new FormData();
  form.append("File", file);
  form.append("SkipDuplicateRows", String(options.skipDuplicateRows));
  form.append("ReplaceOpeningPositions", String(options.replaceOpeningPositions));
  form.append("AddMissingOpeningPositions", String(options.addMissingOpeningPositions));
  return form;
}

/**
 * The file goes up again for the commit rather than the rows coming back down: the server
 * re-parses and re-plans it, so what it writes is exactly what the preview computed and
 * not something the browser could have altered in between.
 */
export function usePreviewImport(scope: TransferScope, targetId?: string) {
  return useMutation({
    mutationFn: async ({ file, options }: { file: File; options: ImportOptions }) =>
      (
        await apiClient.post<ImportPreview>(
          `${transferBasePath(scope, targetId)}/import/preview`,
          body(file, options),
        )
      ).data,
  });
}

export function useCommitImport(scope: TransferScope, targetId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ file, options }: { file: File; options: ImportOptions }) =>
      (
        await apiClient.post<ImportResult>(
          `${transferBasePath(scope, targetId)}/import/commit`,
          body(file, options),
        )
      ).data,
    onSuccess: () => invalidateEverything(queryClient),
  });
}

export function useImportBatches() {
  return useQuery({
    queryKey: ["imports"],
    queryFn: async () => (await apiClient.get<ImportBatch[]>("/imports")).data,
  });
}

export function useUndoImport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (batchId: string) => apiClient.post(`/imports/${batchId}/undo`, {}),
    onSuccess: () => invalidateEverything(queryClient),
  });
}

/**
 * An import can touch accounts, holdings, transactions, valuations and the dashboard total
 * in one go — there is no narrower invalidation that would be honest.
 */
function invalidateEverything(queryClient: ReturnType<typeof useQueryClient>) {
  for (const key of [["accounts"], ["holdings"], ["transactions"], ["dashboard"], ["imports"]]) {
    queryClient.invalidateQueries({ queryKey: key });
  }
}
