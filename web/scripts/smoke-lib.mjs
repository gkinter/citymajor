export const BASE_URL = (process.env.BASE_URL ?? "http://localhost:3000").replace(
  /\/$/,
  "",
);
export const TIMEOUT_MS = Number(process.env.TIMEOUT_MS ?? 60_000);
export const HEADLESS = process.env.HEADLESS !== "0";
export const WASM_EXPECTED = process.env.WASM_EXPECTED === "1";
export const SCREENSHOT = process.env.SCREENSHOT === "1";
/** Match CityCanvas / Forge default — Playwright default viewport breaks WebGL readPixels. */
function parsePlayViewport() {
  const raw = process.env.PLAY_VIEWPORT?.trim();
  if (raw) {
    const m = /^(\d{2,5})x(\d{2,5})$/.exec(raw);
    if (m) return { width: Number(m[1]), height: Number(m[2]) };
  }
  return { width: 1280, height: 720 };
}
export const PLAY_VIEWPORT = parsePlayViewport();
/** Set SMOKE_SKIP_SAVES=1 to skip /api/saves checks (e.g. prod without CITYMAJOR_SESSION_SECRET). */
export const SMOKE_SKIP_SAVES = process.env.SMOKE_SKIP_SAVES === "1";
/** Set SMOKE_NARRATIVE=1 to assert POST /api/narrative/event returns a valid source field. */
export const SMOKE_NARRATIVE = process.env.SMOKE_NARRATIVE === "1";
/** When SMOKE_NARRATIVE=1, expect source=llm (local dev with keys + NARRATIVE_LLM_DEV_OVERRIDE). */
export const SMOKE_NARRATIVE_LLM = process.env.SMOKE_NARRATIVE_LLM === "1";

export function fail(tag, message) {
  console.error(`[${tag}] FAIL: ${message}`);
  process.exit(1);
}

export function pass(tag, message) {
  console.log(`[${tag}] OK: ${message}`);
}

/**
 * POST /api/narrative/event — assert response includes source=template|llm.
 * @param {string} tag
 */
export async function assertNarrativeEventApi(tag) {
  const headers = { "Content-Type": "application/json" };
  if (process.env.SMOKE_NARRATIVE_TIER) {
    headers["X-CityMajor-Tier"] = process.env.SMOKE_NARRATIVE_TIER;
  }

  const res = await fetch(`${BASE_URL}/api/narrative/event`, {
    method: "POST",
    headers,
    body: JSON.stringify({ bucket: "default" }),
  });

  if (!res.ok) {
    fail(tag, `POST /api/narrative/event returned HTTP ${res.status}`);
  }

  let json;
  try {
    json = await res.json();
  } catch {
    fail(tag, "POST /api/narrative/event returned non-JSON body");
  }

  if (json.source !== "template" && json.source !== "llm") {
    fail(tag, `POST /api/narrative/event missing valid source (got ${json.source ?? "null"})`);
  }

  const expectedSource = process.env.SMOKE_NARRATIVE_LLM === "1" ? "llm" : "template";
  if (json.source !== expectedSource) {
    fail(
      tag,
      `POST /api/narrative/event expected source=${expectedSource}, got ${json.source}`,
    );
  }

  if (typeof json.headline !== "string" || !json.headline.trim()) {
    fail(tag, "POST /api/narrative/event missing headline");
  }

  pass(tag, `narrative event source=${json.source}`);
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
