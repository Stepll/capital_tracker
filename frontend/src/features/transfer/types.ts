export type TransferScope = "Portfolio" | "Account" | "Holding";

export interface ImportProblem {
  line: number;
  message: string;
}

export interface ImportPreviewHolding {
  name: string;
  symbol: string | null;
  accountName: string;
  isNewHolding: boolean;
  isNewAccount: boolean;
  currency: string;
  quantityBefore: number | null;
  quantityAfter: number | null;
  valueBefore: number;
  valueAfter: number;
  newTransactions: number;
  newValuations: number;
  /** Rows already present, left untouched — the normal outcome of an overlapping file. */
  skippedRows: number;
  replacesOpeningPosition: boolean;
  /** True when one exists to replace — the toggle is only worth offering then. */
  hasOpeningPosition: boolean;
  /** The asset was deleted and this import brings it back, so its rows are visible. */
  revivesHolding: boolean;
  wouldGoNegative: boolean;
  deletedOn: string | null;
}

export interface ImportBatch {
  id: string;
  createdAt: string;
  scope: TransferScope;
  fileName: string;
  accountsCreated: number;
  holdingsCreated: number;
  transactionsCreated: number;
  valuationsWritten: number;
  undoneAt: string | null;
}

export interface ImportPreview {
  fileName: string;
  problems: ImportProblem[];
  holdings: ImportPreviewHolding[];
  accountsToCreate: string[];
  sameFileImportedBefore: ImportBatch | null;
  canCommit: boolean;
}

export interface ImportResult {
  batchId: string;
  preview: ImportPreview;
}

export interface ImportOptions {
  skipDuplicateRows: boolean;
  replaceOpeningPositions: boolean;
  addMissingOpeningPositions: boolean;
}

export const DEFAULT_IMPORT_OPTIONS: ImportOptions = {
  skipDuplicateRows: true,
  replaceOpeningPositions: false,
  addMissingOpeningPositions: false,
};

/** Both halves of the transfer live at the same paths, differing only in the verb. */
export function transferBasePath(scope: TransferScope, targetId?: string): string {
  if (scope === "Account") return `/accounts/${targetId}`;
  if (scope === "Holding") return `/holdings/${targetId}`;
  return "";
}

export interface FileInspection {
  fileName: string;
  /** The top of the file exactly as it is, so the header row can be picked by eye. */
  rows: string[][];
  headerRow: number;
  columns: Record<string, number>;
  /** Column index (as a JSON key) -> its distinct values, when few enough to be a category. */
  distinctValues: Record<string, string[]>;
  /** Our own export coming back: no mapping to fill in. */
  looksCanonical: boolean;
  problem: string | null;
}

export interface EventMapping {
  column?: number | null;
  values?: Record<string, string> | null;
  fixed?: string | null;
  whenPositive?: string | null;
  whenNegative?: string | null;
}

export interface ColumnMapping {
  headerRow: number;
  columns: Record<string, number>;
  event: EventMapping;
}

/** The canonical columns worth pointing a foreign statement at. */
export const MAPPABLE_FIELDS = [
  "Дата",
  "Сума",
  "Кількість",
  "Ціна",
  "Валюта",
  "Актив",
  "Тікер",
  "Нотатка",
] as const;

export const EVENT_LABELS = [
  "Купівля",
  "Продаж",
  "Дивіденди",
  "Оренда",
  "Витрата",
  "Внесення",
  "Виведення",
  "Оцінка",
] as const;

/** Where the direction of a row comes from — different in every real statement. */
export type EventSource = "column" | "sign" | "fixed";
