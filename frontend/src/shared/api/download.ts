import { apiClient } from "./client";

/**
 * Pulls the filename the server chose out of Content-Disposition. ASP.NET writes both
 * `filename` (ASCII fallback) and `filename*` (RFC 5987, percent-encoded UTF-8); the
 * starred one is the only place a Ukrainian name survives intact.
 */
function filenameFrom(header: string | undefined): string | null {
  if (!header) return null;

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1]);
    } catch {
      // Malformed encoding shouldn't cost the user their download.
    }
  }

  return /filename="?([^";]+)"?/i.exec(header)?.[1] ?? null;
}

/**
 * Downloads through the API client rather than a plain link: every endpoint needs the JWT,
 * and an <a href> carries no Authorization header.
 */
export async function downloadFile(path: string, fallbackName: string): Promise<void> {
  const response = await apiClient.get<Blob>(path, { responseType: "blob" });

  const url = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = url;
  link.download = filenameFrom(response.headers["content-disposition"] as string | undefined) ?? fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
