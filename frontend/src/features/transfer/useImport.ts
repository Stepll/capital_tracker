import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../../shared/api/client";
import {
  transferBasePath,
  type ColumnMapping,
  type FileInspection,
  type ImportBatch,
  type ImportOptions,
  type ImportPreview,
  type ImportResult,
  type TransferScope,
} from "./types";

function body(file: File, options: ImportOptions, mapping?: ColumnMapping | null) {
  const form = new FormData();
  form.append("File", file);
  form.append("SkipDuplicateRows", String(options.skipDuplicateRows));
  form.append("ReplaceOpeningPositions", String(options.replaceOpeningPositions));
  form.append("AddMissingOpeningPositions", String(options.addMissingOpeningPositions));
  // A form field rather than a body, because the file it describes goes up as multipart.
  if (mapping) form.append("Mapping", JSON.stringify(mapping));
  return form;
}

/** A first look at an unknown file, before anything is mapped or imported. */
export function useInspectImport() {
  return useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append("File", file);
      return (await apiClient.post<FileInspection>("/import/inspect", form)).data;
    },
  });
}

/**
 * The file goes up again for the commit rather than the rows coming back down: the server
 * re-parses and re-plans it, so what it writes is exactly what the preview computed and
 * not something the browser could have altered in between.
 */
export function usePreviewImport(scope: TransferScope, targetId?: string) {
  return useMutation({
    mutationFn: async ({
      file,
      options,
      mapping,
    }: {
      file: File;
      options: ImportOptions;
      mapping?: ColumnMapping | null;
    }) =>
      (
        await apiClient.post<ImportPreview>(
          `${transferBasePath(scope, targetId)}/import/preview`,
          body(file, options, mapping),
        )
      ).data,
  });
}

export function useCommitImport(scope: TransferScope, targetId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      file,
      options,
      mapping,
    }: {
      file: File;
      options: ImportOptions;
      mapping?: ColumnMapping | null;
    }) =>
      (
        await apiClient.post<ImportResult>(
          `${transferBasePath(scope, targetId)}/import/commit`,
          body(file, options, mapping),
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
