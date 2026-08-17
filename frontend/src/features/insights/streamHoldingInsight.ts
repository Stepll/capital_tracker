import { apiClient, handleUnauthorized, TOKEN_STORAGE_KEY } from "../../shared/api/client";
import type { AiInsight } from "./useInsights";
import type { InsightErrorCode, InsightPhase } from "./insightTypes";

export type InsightStreamEvent =
  | { type: "Phase"; phase: InsightPhase; detail: string | null }
  | { type: "Completed"; insight: AiInsight }
  | { type: "Failed"; errorCode: InsightErrorCode; retryAt: string | null };

/**
 * Runs one analysis and reports progress as it streams in.
 *
 * Not a hook and not a mutation: it is driven by an explicit click, exactly like
 * revealSecretAttribute() next door. Notably it is NOT wired to a useEffect — under
 * StrictMode an effect would fire twice in development and start two analyses, and
 * each one costs real money.
 *
 * EventSource can't carry an Authorization header, so this is a plain fetch over the
 * body stream. That also means the axios interceptors don't apply — hence the manual
 * 401 handling below.
 */
export async function streamHoldingInsight(
  holdingId: string,
  onEvent: (event: InsightStreamEvent) => void,
  signal: AbortSignal,
): Promise<void> {
  let sawTerminalEvent = false;

  try {
    const response = await fetch(`${apiClient.defaults.baseURL}/holdings/${holdingId}/insights/stream`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${localStorage.getItem(TOKEN_STORAGE_KEY) ?? ""}`,
        "Content-Type": "application/json",
        Accept: "text/event-stream",
      },
      // Not bodyless: a POST with no Content-Length is rejected by nginx with a 400
      // before it reaches the app (same reason the axios calls send {}).
      body: "{}",
      signal,
    });

    if (response.status === 401) {
      handleUnauthorized();
      return;
    }

    if (!response.ok || !response.body) {
      onEvent({ type: "Failed", errorCode: "Upstream", retryAt: null });
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // Frames are separated by a blank line; whatever trails the last one is a
      // partial frame and has to wait for more bytes.
      const frames = buffer.split("\n\n");
      buffer = frames.pop() ?? "";

      for (const frame of frames) {
        const event = parseFrame(frame);
        if (!event) continue;

        if (event.type !== "Phase") sawTerminalEvent = true;
        onEvent(event);
      }
    }
  } catch (error) {
    // An abort is the user closing the modal, not a failure worth reporting.
    if (signal.aborted) return;
    console.error("Analysis stream failed", error);
    onEvent({ type: "Failed", errorCode: "Upstream", retryAt: null });
    return;
  }

  // The connection ended without a verdict — the server aborted mid-flight. Say so,
  // rather than leaving the modal waiting on a stream that will never speak again.
  if (!sawTerminalEvent && !signal.aborted) {
    onEvent({ type: "Failed", errorCode: "Upstream", retryAt: null });
  }
}

function parseFrame(frame: string): InsightStreamEvent | null {
  const dataLines: string[] = [];

  for (const line of frame.split("\n")) {
    // Comments (": ping") are the server's keep-alive — nothing to parse.
    if (line.startsWith(":") || line.length === 0) continue;
    if (line.startsWith("data:")) dataLines.push(line.slice(5).trimStart());
  }

  if (dataLines.length === 0) return null;

  try {
    return JSON.parse(dataLines.join("\n")) as InsightStreamEvent;
  } catch {
    console.warn("Skipping unparseable SSE frame", frame);
    return null;
  }
}
