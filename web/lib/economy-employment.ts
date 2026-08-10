/** Format a 0–1 employment share (unemployment / job vacancy) as a whole percent. */
export function formatEmploymentShare(rate: number): string {
  const pct = Math.round(Math.min(1, Math.max(0, rate)) * 100);
  return `${pct}%`;
}
