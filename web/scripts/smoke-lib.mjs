export const BASE_URL = (process.env.BASE_URL ?? "http://localhost:3000").replace(
  /\/$/,
  "",
);
export const TIMEOUT_MS = Number(process.env.TIMEOUT_MS ?? 60_000);
export const HEADLESS = process.env.HEADLESS !== "0";
export const WASM_EXPECTED = process.env.WASM_EXPECTED === "1";
export const SCREENSHOT = process.env.SCREENSHOT === "1";

export function fail(tag, message) {
  console.error(`[${tag}] FAIL: ${message}`);
  process.exit(1);
}

export function pass(tag, message) {
  console.log(`[${tag}] OK: ${message}`);
}

/** @param {string} path */
export async function assertHttpOk(tag, path) {
  const res = await fetch(`${BASE_URL}${path}`, { redirect: "follow" });
  if (!res.ok) {
    fail(tag, `${path} returned HTTP ${res.status} — is \`pnpm dev\` running?`);
  }
  pass(tag, `${path} HTTP ${res.status}`);
  return res;
}
