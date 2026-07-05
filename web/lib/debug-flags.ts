/** True when `/play?debug=chunks` — show chunk streaming stats in Diagnostics HUD. */
export function isChunkDebugEnabled(): boolean {
  if (typeof window === "undefined") return false;
  return new URLSearchParams(window.location.search).get("debug") === "chunks";
}
