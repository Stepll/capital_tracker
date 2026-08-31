import { useEffect, useState } from "react";

/**
 * Whether the viewport is phone-sized. Used where CSS cannot reach — a chart drawn into
 * SVG by a library decides its own geometry, and slice labels that fit on a desktop have
 * nowhere to go at 390px.
 */
export function useIsNarrow(maxWidth = 640): boolean {
  const query = `(max-width: ${maxWidth}px)`;
  const [narrow, setNarrow] = useState(() => window.matchMedia(query).matches);

  useEffect(() => {
    const media = window.matchMedia(query);
    const update = () => setNarrow(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, [query]);

  return narrow;
}
