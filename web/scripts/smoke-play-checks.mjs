import {
  BASE_URL,
  PLAY_VIEWPORT,
  SMOKE_SKIP_SAVES,
  fail,
  pass,
  WASM_EXPECTED,
} from "./smoke-lib.mjs";
import { runPerfGate } from "./perf-gate.mjs";

const PERF_GATE = process.env.PERF_GATE === "1";

const CMJR_MAGIC = 0x434d4a52;
const FORMAT_VERSION_CMJR_V1 = 1;
const SNAPSHOT_SCHEMA_VERSION = 1;
const CMJR_HEADER_BYTES = 256;
const DEFAULT_WORLD_SIZE = 256;

/** @param {Uint8Array} data */
function crc32(data) {
  let crc = 0xffffffff;
  for (const b of data) {
    crc ^= b;
    for (let i = 0; i < 8; i++) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return (~crc) >>> 0;
}

/** @param {Uint8Array} buf @param {number} offset @param {number} value */
function writeU32LE(buf, offset, value) {
  buf[offset] = value & 0xff;
  buf[offset + 1] = (value >>> 8) & 0xff;
  buf[offset + 2] = (value >>> 16) & 0xff;
  buf[offset + 3] = (value >>> 24) & 0xff;
}

/** @param {Uint8Array} buf @param {number} offset @param {number} value */
function writeU16LE(buf, offset, value) {
  buf[offset] = value & 0xff;
  buf[offset + 1] = (value >>> 8) & 0xff;
}

/** @param {number} chunkId @param {Uint8Array} body */
function writeChunk(chunkId, body) {
  const chunk = new Uint8Array(5 + body.length);
  chunk[0] = chunkId;
  writeU32LE(chunk, 1, body.length);
  chunk.set(body, 5);
  return chunk;
}

/** Minimal valid CMJR v1 blob for API envelope smoke (SAVE_FORMAT_WEB.md §3). */
function buildMinimalCmjrFixtureBase64() {
  const json = JSON.stringify({
    tick: 0,
    population: 0,
    cityFunds: 0,
    era: 0,
    zones: [],
    roads: [],
    activeEvents: [],
  });
  const jsonBytes = new TextEncoder().encode(json);
  const snapshotChunk = writeChunk(0x01, jsonBytes);
  const endChunk = writeChunk(0xff, new Uint8Array(0));
  const payload = new Uint8Array(snapshotChunk.length + endChunk.length);
  payload.set(snapshotChunk, 0);
  payload.set(endChunk, snapshotChunk.length);

  const header = new Uint8Array(CMJR_HEADER_BYTES);
  writeU32LE(header, 0, CMJR_MAGIC);
  writeU32LE(header, 4, FORMAT_VERSION_CMJR_V1);
  writeU32LE(header, 8, crc32(payload));
  writeU32LE(header, 12, payload.length);
  writeU32LE(header, 16, payload.length);
  header[20] = 0;
  writeU16LE(header, 21, SNAPSHOT_SCHEMA_VERSION);
  writeU16LE(header, 23, DEFAULT_WORLD_SIZE);

  const file = new Uint8Array(CMJR_HEADER_BYTES + payload.length);
  file.set(header, 0);
  file.set(payload, CMJR_HEADER_BYTES);
  return Buffer.from(file).toString("base64");
}

/** @param {Headers} headers */
function mergeSetCookie(jar, headers) {
  const raw =
    typeof headers.getSetCookie === "function"
      ? headers.getSetCookie()
      : headers.get("set-cookie")
        ? [headers.get("set-cookie")]
        : [];
  if (raw.length === 0) return jar;

  const map = new Map(
    jar
      .split("; ")
      .filter(Boolean)
      .map((pair) => {
        const idx = pair.indexOf("=");
        return idx > 0 ? [pair.slice(0, idx), pair.slice(idx + 1)] : [pair, ""];
      }),
  );
  for (const line of raw) {
    const part = line.split(";")[0]?.trim();
    if (!part) continue;
    const idx = part.indexOf("=");
    if (idx <= 0) continue;
    map.set(part.slice(0, idx), part.slice(idx + 1));
  }
  return [...map.entries()].map(([k, v]) => `${k}=${v}`).join("; ");
}

function createSaveApiSession() {
  let cookieJar = "";
  return {
    /** @param {string} path @param {RequestInit} [init] */
    async fetch(path, init = {}) {
      const res = await fetch(`${BASE_URL}${path}`, {
        ...init,
        headers: {
          ...(init.headers ?? {}),
          ...(cookieJar ? { Cookie: cookieJar } : {}),
        },
      });
      cookieJar = mergeSetCookie(cookieJar, res.headers);
      return res;
    },
    /** @param {string} saveId */
    async deleteSave(saveId) {
      const res = await this.fetch(`/api/saves/${saveId}`, { method: "DELETE" });
      if (res.status !== 204) {
        console.warn(
          `[smoke] WARN: DELETE /api/saves/${saveId} returned ${res.status} (non-fatal)`,
        );
      }
    },
  };
}

/**
 * @param {unknown} save
 * @param {string} tag
 * @param {string} label
 */
function assertCmjrSaveEnvelope(save, tag, label) {
  if (!save || typeof save !== "object") {
    fail(tag, `POST /api/saves (${label}) missing save object`);
  }
  if (save.formatVersion !== FORMAT_VERSION_CMJR_V1) {
    fail(
      tag,
      `POST /api/saves (${label}) expected formatVersion ${FORMAT_VERSION_CMJR_V1}, got ${save.formatVersion}`,
    );
  }
  if (save.snapshotSchemaVersion !== SNAPSHOT_SCHEMA_VERSION) {
    fail(
      tag,
      `POST /api/saves (${label}) expected snapshotSchemaVersion ${SNAPSHOT_SCHEMA_VERSION}, got ${save.snapshotSchemaVersion}`,
    );
  }
  if (typeof save.summary !== "object" || save.summary === null) {
    fail(tag, `POST /api/saves (${label}) missing summary`);
  }
  if (typeof save.blobKey !== "string" || !save.blobKey.endsWith(".cmjr")) {
    fail(tag, `POST /api/saves (${label}) expected blobKey ending in .cmjr`);
  }
  if (typeof save.blobBytes !== "number" || save.blobBytes < CMJR_HEADER_BYTES) {
    fail(tag, `POST /api/saves (${label}) missing or tiny blobBytes`);
  }
}

/**
 * @param {string} tag
 * @param {string} saveId
 * @param {{ fetch: (path: string, init?: RequestInit) => Promise<Response> }} session
 */
async function assertSaveListed(tag, saveId, session) {
  const res = await session.fetch("/api/saves");
  if (!res.ok) {
    fail(tag, `GET /api/saves after POST expected 200, got ${res.status}`);
  }
  let json;
  try {
    json = await res.json();
  } catch {
    fail(tag, "GET /api/saves after POST returned non-JSON body");
  }
  if (!Array.isArray(json.saves)) {
    fail(tag, "GET /api/saves after POST missing saves array");
  }
  const found = json.saves.some((s) => s?.id === saveId);
  if (!found) {
    fail(tag, `GET /api/saves missing posted save id=${saveId}`);
  }
  pass(tag, `GET /api/saves lists posted save (${json.count}/${json.maxSlots} slots)`);
}

/**
 * Assert POST /api/saves accepts wasmBlobBase64 and returns formatVersion 1 envelope.
 * @param {string} tag
 * @param {string} wasmBlobBase64
 * @param {{ fetch: (path: string, init?: RequestInit) => Promise<Response>; deleteSave: (id: string) => Promise<void> }} session
 * @param {string} label
 * @param {{ verifyList?: boolean }} [options]
 * @returns {Promise<{ id: string; name: string; blobBytes: number }>}
 */
async function assertCmjrSavePost(tag, wasmBlobBase64, session, label, options = {}) {
  const name = `smoke-cmjr-${label}-${Date.now()}`;
  const res = await session.fetch("/api/saves", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name, wasmBlobBase64 }),
  });

  if (res.status !== 201) {
    let detail = "";
    try {
      const errJson = await res.json();
      detail =
        typeof errJson?.error === "string" ? `: ${errJson.error}` : ` body=${JSON.stringify(errJson)}`;
    } catch {
      detail = "";
    }
    fail(tag, `POST /api/saves (${label}) expected 201, got ${res.status}${detail}`);
  }

  let json;
  try {
    json = await res.json();
  } catch {
    fail(tag, `POST /api/saves (${label}) returned non-JSON body`);
  }

  const save = json?.save;
  assertCmjrSaveEnvelope(save, tag, label);

  if (options.verifyList) {
    await assertSaveListed(tag, save.id, session);
  }

  await session.deleteSave(save.id);
  pass(
    tag,
    `POST /api/saves (${label}) formatVersion=1 envelope OK (blobBytes=${save.blobBytes})`,
  );
  return save;
}

/**
 * export_cmjr via PlayClient smoke hook (window.__citymajorSimApi).
 * @param {import('playwright').Page} page
 * @param {string} tag
 * @returns {Promise<string | null>}
 */
async function exportWasmBlobViaHook(page, tag) {
  try {
    await page.waitForFunction(
      () => typeof window.__citymajorSimApi?.exportWasmSave === "function",
      undefined,
      { timeout: 30_000 },
    );
  } catch {
    console.log(`[${tag}] SKIP: __citymajorSimApi.exportWasmSave not ready`);
    return null;
  }

  const base64 = await page.evaluate(async () => {
    const api = window.__citymajorSimApi;
    if (!api) return null;
    try {
      return await api.exportWasmSave();
    } catch {
      return null;
    }
  });

  if (typeof base64 !== "string" || base64.length < 64) {
    console.log(`[${tag}] SKIP: exportWasmSave returned empty or tiny blob`);
    return null;
  }
  return base64;
}

/**
 * export_cmjr → POST wasmBlobBase64 → GET /api/saves lists the slot.
 * @param {import('playwright').Page} page
 * @param {string} tag
 * @param {{ fetch: (path: string, init?: RequestInit) => Promise<Response>; deleteSave: (id: string) => Promise<void> }} session
 * @returns {Promise<boolean>}
 */
async function tryWasmCmjrExportAndPost(page, tag, session) {
  const wasmBlobBase64 = await exportWasmBlobViaHook(page, tag);
  if (!wasmBlobBase64) return false;

  const save = await assertCmjrSavePost(tag, wasmBlobBase64, session, "wasm-export", {
    verifyList: true,
  });
  pass(
    tag,
    `WASM export_cmjr → POST → GET list round-trip (blobBytes=${save.blobBytes})`,
  );
  return true;
}

/**
 * When WASM sim is live, Save UI triggers export_cmjr and POST with wasmBlobBase64.
 * @param {import('playwright').Page} page
 * @param {string} tag
 * @returns {Promise<boolean>} true when a formatVersion 1 save round-trip succeeded
 */
async function tryWasmCmjrSaveViaUi(page, tag) {
  const saveBtn = page
    .getByRole("group", { name: "Save and load" })
    .getByRole("button", { name: "Save", exact: true });
  if ((await saveBtn.count()) === 0) return false;
  if (await saveBtn.isDisabled()) {
    console.log(`[${tag}] SKIP: Save button disabled (slots full or sim not ready)`);
    return false;
  }

  const saveName = `smoke-wasm-export-${Date.now()}`;
  page.once("dialog", (dialog) => dialog.accept(saveName));

  const responsePromise = page.waitForResponse(
    (response) =>
      response.url().includes("/api/saves") && response.request().method() === "POST",
    { timeout: 30_000 },
  );

  await saveBtn.click();
  let response;
  try {
    response = await responsePromise;
  } catch {
    console.log(`[${tag}] SKIP: WASM save POST did not complete (export_cmjr unavailable?)`);
    return false;
  }

  if (response.status() !== 201) {
    console.log(
      `[${tag}] SKIP: WASM save POST returned ${response.status()} (export_cmjr may be unavailable)`,
    );
    return false;
  }

  const json = await response.json();
  const save = json?.save;
  if (!save || save.formatVersion !== FORMAT_VERSION_CMJR_V1) {
    console.log(
      `[${tag}] SKIP: WASM save POST formatVersion=${save?.formatVersion ?? "null"} (no CMJR export)`,
    );
    return false;
  }

  const listRes = await page.request.get(`${BASE_URL}/api/saves`);
  if (!listRes.ok()) {
    fail(tag, `GET /api/saves after UI save expected 200, got ${listRes.status()}`);
  }
  const listJson = await listRes.json();
  if (!Array.isArray(listJson.saves) || !listJson.saves.some((s) => s?.id === save.id)) {
    fail(tag, `GET /api/saves missing UI-posted save id=${save.id}`);
  }
  pass(tag, `GET /api/saves lists UI-posted save (${listJson.count}/${listJson.maxSlots} slots)`);

  const del = await page.request.delete(`${BASE_URL}/api/saves/${save.id}`);
  if (!del.ok() && del.status() !== 204) {
    console.warn(`[${tag}] WARN: DELETE wasm save ${save.id} returned ${del.status()}`);
  }

  pass(tag, `WASM export_cmjr save round-trip (formatVersion=1, name="${save.name}")`);
  return true;
}

/**
 * POST wasmBlobBase64 — live WASM export when available, else minimal CMJR fixture.
 * @param {string} tag
 * @param {import('playwright').Page} [page]
 * @param {{ simIsWasm?: boolean }} [options]
 */
export async function assertCmjrSaveRoundTrip(tag, page, options = {}) {
  const simIsWasm = options.simIsWasm ?? false;
  const session = createSaveApiSession();

  if (page && simIsWasm) {
    const viaExport = await tryWasmCmjrExportAndPost(page, tag, session);
    if (viaExport) return;

    const viaUi = await tryWasmCmjrSaveViaUi(page, tag);
    if (viaUi) return;

    if (WASM_EXPECTED) {
      fail(
        tag,
        "WASM export_cmjr save round-trip failed (export_cmjr unavailable or POST/GET rejected)",
      );
    }
  }

  const fixture = buildMinimalCmjrFixtureBase64();
  await assertCmjrSavePost(tag, fixture, session, "fixture");
}

/**
 * GET /api/saves — list endpoint must return a well-formed envelope.
 * Prod requires CITYMAJOR_SESSION_SECRET (≥32 chars) on the target app; without it
 * the route returns 500 "Session configuration error" — we skip save checks then.
 * @param {string} tag
 * @returns {Promise<boolean>} false when save API checks were skipped
 */
export async function assertSaveApiHealth(tag) {
  if (SMOKE_SKIP_SAVES) {
    console.log(
      `[${tag}] SKIP: save API checks (SMOKE_SKIP_SAVES=1; prod needs CITYMAJOR_SESSION_SECRET)`,
    );
    return false;
  }

  const res = await fetch(`${BASE_URL}/api/saves`, { redirect: "follow" });
  if (!res.ok) {
    if (res.status === 500) {
      let errJson;
      try {
        errJson = await res.json();
      } catch {
        errJson = null;
      }
      if (errJson?.error === "Session configuration error") {
        console.log(
          `[${tag}] SKIP: save API checks (target missing CITYMAJOR_SESSION_SECRET)`,
        );
        return false;
      }
    }
    fail(tag, `GET /api/saves returned HTTP ${res.status}`);
  }

  let json;
  try {
    json = await res.json();
  } catch {
    fail(tag, "GET /api/saves returned non-JSON body");
  }

  if (!Array.isArray(json.saves)) {
    fail(tag, "GET /api/saves missing saves array");
  }
  if (typeof json.count !== "number" || json.count < 0) {
    fail(tag, "GET /api/saves missing or invalid count");
  }
  if (typeof json.maxSlots !== "number" || json.maxSlots < 1) {
    fail(tag, "GET /api/saves missing or invalid maxSlots");
  }
  if (json.tier !== "free" && json.tier !== "founder_pass") {
    fail(tag, `GET /api/saves unexpected tier: ${json.tier ?? "null"}`);
  }
  pass(tag, `save API healthy (${json.count}/${json.maxSlots} slots, tier=${json.tier})`);

  const badBody = await fetch(`${BASE_URL}/api/saves`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: "{not-json",
  });
  if (badBody.status !== 400) {
    fail(tag, `POST /api/saves with invalid JSON expected 400, got ${badBody.status}`);
  }
  pass(tag, "POST /api/saves rejects malformed JSON");
  return true;
}

/**
 * Research + Herald HUD panels open and render expected chrome.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertHudPanels(page, tag) {
  const researchBtn = page.getByRole("button", { name: /^Research\b/ });
  await researchBtn.waitFor({ state: "visible" });
  await researchBtn.click();

  const researchPanel = page.getByRole("dialog", { name: "Research catalog" });
  await researchPanel.waitFor({ state: "visible" });
  await page.getByText(/Technologies \(\d+\)/).waitFor({ state: "visible" });
  pass(tag, "research panel opens with tech catalog");

  await researchPanel.getByRole("button", { name: "Close" }).click();
  await researchPanel.waitFor({ state: "hidden" });
  pass(tag, "research panel closes");

  const heraldBtn = page.getByRole("button", { name: /Herald/ });
  await heraldBtn.waitFor({ state: "visible" });
  await page.locator('button:has-text("Herald"):not([disabled])').waitFor({
    state: "visible",
    timeout: 15_000,
  });
  await heraldBtn.click();

  const heraldPanel = page.getByRole("dialog", { name: /Daily Herald/ });
  await heraldPanel.waitFor({ state: "visible" });
  await page.locator("#herald-event-headline").waitFor({ state: "visible", timeout: 15_000 });
  const headline = await page.locator("#herald-event-headline").innerText();
  if (!headline.trim()) {
    fail(tag, "Herald panel loaded but headline is empty");
  }
  pass(tag, `herald panel loads story: "${headline.slice(0, 48)}${headline.length > 48 ? "…" : ""}"`);

  await page.keyboard.press("Escape");
  await heraldPanel.waitFor({ state: "hidden" });
  pass(tag, "herald panel closes");
}

/**
 * Era Quest panel shows population + tech-count gates from WasmEraDeriver.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertEraQuestPanel(page, tag) {
  const panel = page.getByLabel("Era quest progress");
  await panel.waitFor({ state: "visible", timeout: 15_000 });

  const checklist = panel.getByRole("list", { name: "Era quest objectives" });
  await checklist.waitFor({ state: "visible" });

  const populationGate = checklist.getByRole("listitem", { name: /Population:/ });
  const techGate = checklist.getByRole("listitem", {
    name: /Technologies researched:/,
  });

  await populationGate.waitFor({ state: "visible" });
  await techGate.waitFor({ state: "visible" });

  const popText = await populationGate.innerText();
  const techText = await techGate.innerText();
  if (!/\/\s*400\b/.test(popText)) {
    fail(tag, `Era Quest population gate missing Industrial threshold (400): ${popText}`);
  }
  if (!/\/\s*5\b/.test(techText)) {
    fail(tag, `Era Quest tech gate missing Industrial threshold (5): ${techText}`);
  }

  pass(tag, `era quest panel shows pop + tech gates (${popText.trim()} · ${techText.trim()})`);
}

/**
 * Economy HUD panel opens, shows shortages/surpluses (or RCI fallback), closes.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertEconomyPanel(page, tag) {
  const economyBtn = page.getByRole("button", { name: /^Economy\b/ });
  await economyBtn.waitFor({ state: "visible" });
  await clickHudToolbarButton(economyBtn);

  const economyPanel = page.getByLabel("City economy");
  await economyPanel.waitFor({ state: "visible" });
  await economyPanel.locator('[aria-label="Shortages"]').waitFor({ state: "visible" });
  await economyPanel.locator('[aria-label="Surpluses"]').waitFor({ state: "visible" });

  const rciFallback = economyPanel.getByText(/RCI demand \(goods export pending/);
  const goodsRows = economyPanel.locator(".hud-economy-list__item");
  const emptyRows = economyPanel.getByText("None detected");
  const hasRciFallback = (await rciFallback.count()) > 0;
  const hasGoodsRows = (await goodsRows.count()) > 0;
  const hasEmptyState = (await emptyRows.count()) > 0;

  if (!hasRciFallback && !hasGoodsRows && !hasEmptyState) {
    fail(tag, "Economy panel missing shortages/surpluses content and RCI fallback");
  }

  if (hasRciFallback) {
    pass(tag, "economy panel opens with RCI fallback");
  } else if (hasGoodsRows) {
    pass(tag, `economy panel opens with goods imbalances (${await goodsRows.count()} rows)`);
  } else {
    pass(tag, "economy panel opens with empty shortages/surpluses");
  }

  const tradeRoutesSection = economyPanel.getByLabel("Trade routes");
  try {
    await tradeRoutesSection.waitFor({ state: "visible", timeout: 5_000 });
    const sb3728Link = tradeRoutesSection.getByRole("link", { name: /SB-3728/ });
    await sb3728Link.waitFor({ state: "visible", timeout: 5_000 });
    const href = await sb3728Link.getAttribute("href");
    if (!href?.includes("SB-3728")) {
      fail(tag, `Trade routes SB-3728 link href missing issue id: ${href ?? "null"}`);
    }
    pass(tag, "economy panel shows Trade routes stub with SB-3728 link");
  } catch {
    console.log(
      `[${tag}] SKIP: Trade routes section not on target (deploy may lag branch smoke)`,
    );
  }

  await economyPanel.getByRole("button", { name: "Close" }).click();
  await economyPanel.waitFor({ state: "hidden" });
  pass(tag, "economy panel closes");
}

/**
 * Citizens HUD panel opens and shows seeded household count from WASM status.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertCitizenPanel(page, tag) {
  const citizenBtn = page.getByRole("button", { name: /^Citizens\b/ });
  await citizenBtn.waitFor({ state: "visible" });

  const badgeMatch = (await citizenBtn.innerText()).match(
    /Citizens\s*([\d,]+|—)/,
  );
  const badgeCount = badgeMatch?.[1];
  if (!badgeCount || badgeCount === "—") {
    fail(tag, "Citizens button badge missing household count");
  }
  pass(tag, `citizens button shows household badge (${badgeCount})`);

  await clickHudToolbarButton(citizenBtn);

  const citizenPanel = page.getByRole("dialog", { name: "Citizen households" });
  await citizenPanel.waitFor({ state: "visible" });

  const subtitle = citizenPanel
    .locator("header")
    .getByText(/\d[\d,]* households · \d[\d,]* residents/);
  await subtitle.waitFor({ state: "visible" });

  const householdsValue = citizenPanel
    .locator(".hud-citizen-stat__label", { hasText: "Households" })
    .locator("..")
    .locator(".hud-citizen-stat__value");
  await householdsValue.waitFor({ state: "visible" });
  const hhText = (await householdsValue.innerText()).trim();
  const hhCount = Number.parseInt(hhText.replace(/,/g, ""), 10);
  if (!Number.isFinite(hhCount) || hhCount < 1) {
    fail(tag, `Citizen panel household count invalid: ${hhText}`);
  }
  pass(tag, `citizen panel opens with ${hhCount} households`);

  await citizenPanel.getByRole("button", { name: "Close" }).click();
  await citizenPanel.waitFor({ state: "hidden" });
  pass(tag, "citizen panel closes");
}

/**
 * Laws HUD panel opens and shows WASM definition + active ordinance counts.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertLawPanel(page, tag) {
  const lawBtn = page.getByRole("button", { name: /^Laws\b/ });
  await lawBtn.waitFor({ state: "visible" });

  if (WASM_EXPECTED) {
    const badgeMatch = (await lawBtn.innerText()).match(/Laws\s*([\d,]+)/);
    const activeBadge = badgeMatch?.[1];
    if (!activeBadge) {
      fail(tag, "Laws button badge missing active ordinance count");
    }
    pass(tag, `laws button shows active badge (${activeBadge})`);
  }

  await clickHudToolbarButton(lawBtn);

  const lawPanel = page.getByLabel("City laws");
  await lawPanel.waitFor({ state: "visible" });
  await lawPanel.getByLabel("Law counts").waitFor({ state: "visible" });

  const definitionValue = lawPanel
    .locator(".hud-law-stat__label", { hasText: "Definitions loaded" })
    .locator("..")
    .locator(".hud-law-stat__value");
  const activeValue = lawPanel
    .locator(".hud-law-stat__label", { hasText: "Ordinances in effect" })
    .locator("..")
    .locator(".hud-law-stat__value");

  await definitionValue.waitFor({ state: "visible" });
  await activeValue.waitFor({ state: "visible" });

  const defText = (await definitionValue.innerText()).trim();
  const activeText = (await activeValue.innerText()).trim();

  if (WASM_EXPECTED) {
    const defCount = Number.parseInt(defText.replace(/,/g, ""), 10);
    if (!Number.isFinite(defCount) || defCount < 1) {
      fail(tag, `Law panel definition count invalid: ${defText}`);
    }
    const activeCount = Number.parseInt(activeText.replace(/,/g, ""), 10);
    if (!Number.isFinite(activeCount) || activeCount < 0) {
      fail(tag, `Law panel active count invalid: ${activeText}`);
    }
    pass(
      tag,
      `law panel opens with ${defCount} definitions · ${activeCount} active`,
    );
  } else if (defText === "—" && activeText === "—") {
    pass(tag, "law panel opens (WASM counts pending)");
  } else {
    pass(tag, `law panel opens (${defText} definitions · ${activeText} active)`);
  }

  await lawPanel.getByRole("button", { name: "Close" }).click();
  await lawPanel.waitFor({ state: "hidden" });
  pass(tag, "law panel closes");
}

/**
 * Click a HUD toolbar button under the diagnostics panel (bottom-left stack).
 * FpsHud sits above ServicesToolbar with pointer-events:none; force avoids
 * Playwright obscured-element flakes when sim ticks re-render the HUD.
 * @param {import('playwright').Locator} button
 */
async function clickHudToolbarButton(button) {
  await button.waitFor({ state: "attached" });
  await button.evaluate((node) => {
    if (node instanceof HTMLElement) node.click();
  });
}

/**
 * Poll aria-pressed until expected or timeout.
 * @param {import('playwright').Locator} button
 * @param {"true" | "false"} expected
 * @param {string} tag
 * @param {string} message
 */
async function expectAriaPressed(button, expected, tag, message) {
  const deadline = Date.now() + 3_000;
  while (Date.now() < deadline) {
    if ((await button.getAttribute("aria-pressed")) === expected) return;
    await button.page().waitForTimeout(50);
  }
  fail(tag, message);
}

/**
 * Optional Services toolbar + Traffic toggle — skipped when UI not mounted.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertOptionalOverlayToolbars(page, tag) {
  const servicesToolbar = page.getByTestId("services-toolbar");

  if ((await servicesToolbar.count()) > 0) {
    await servicesToolbar.waitFor({ state: "attached" });
    const onBtn = servicesToolbar.getByRole("button", { name: "On", exact: true });
    const offBtn = servicesToolbar.getByRole("button", { name: "Off", exact: true });
    const healthBtn = servicesToolbar.getByRole("button", { name: "Health", exact: true });
    const policeBtn = servicesToolbar.getByRole("button", { name: "Police", exact: true });
    const fireBtn = servicesToolbar.getByRole("button", { name: "Fire", exact: true });

    // Default boot state: overlay off.
    await expectAriaPressed(offBtn, "true", tag, "Services Off not pressed on initial load");
    if ((await onBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services On pressed while overlay is off");
    }
    for (const [label, btn] of [
      ["Health", healthBtn],
      ["Police", policeBtn],
      ["Fire", fireBtn],
    ]) {
      if ((await btn.isDisabled()) !== true) {
        fail(tag, `Services ${label} submode enabled while overlay is off`);
      }
    }
    pass(tag, "services toolbar boots with overlay off");

    await clickHudToolbarButton(onBtn);
    await expectAriaPressed(onBtn, "true", tag, "Services On button did not activate (aria-pressed)");
    if ((await offBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services On and Off both pressed (aria-pressed)");
    }
    await expectAriaPressed(healthBtn, "true", tag, "Services On did not default to Health submode");
    if (await policeBtn.isDisabled()) {
      fail(tag, "Services Police submode still disabled after On");
    }
    pass(tag, "services toolbar On defaults to Health");

    await clickHudToolbarButton(policeBtn);
    await expectAriaPressed(policeBtn, "true", tag, "Services Police submode did not activate");
    if ((await healthBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services Health still pressed after Police select");
    }
    pass(tag, "services toolbar Police submode selects");

    await clickHudToolbarButton(fireBtn);
    await expectAriaPressed(fireBtn, "true", tag, "Services Fire submode did not activate");
    if ((await policeBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services Police still pressed after Fire select");
    }
    pass(tag, "services toolbar Fire submode selects");

    await clickHudToolbarButton(offBtn);
    await expectAriaPressed(offBtn, "true", tag, "Services Off button did not activate (aria-pressed)");
    if ((await onBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services On still pressed after Off click");
    }
    for (const [label, btn] of [
      ["Health", healthBtn],
      ["Police", policeBtn],
      ["Fire", fireBtn],
    ]) {
      if ((await btn.getAttribute("aria-pressed")) === "true") {
        fail(tag, `Services ${label} submode still pressed after Off`);
      }
      if ((await btn.isDisabled()) !== true) {
        fail(tag, `Services ${label} submode enabled after Off`);
      }
    }
    pass(tag, "services toolbar Off hides overlay and disables submodes");
  } else {
    console.log(`[${tag}] SKIP: services toolbar not on page`);
  }

  const trafficToolbar = page.getByTestId("traffic-overlay-toggle");

  if ((await trafficToolbar.count()) > 0) {
    await trafficToolbar.waitFor({ state: "attached" });
    const onBtn = trafficToolbar.getByRole("button", { name: "On", exact: true });
    const offBtn = trafficToolbar.getByRole("button", { name: "Off", exact: true });

    await expectAriaPressed(offBtn, "true", tag, "Traffic Off not pressed on initial load");
    pass(tag, "traffic toggle boots with overlay off");

    await clickHudToolbarButton(onBtn);
    await expectAriaPressed(onBtn, "true", tag, "Traffic On button did not activate (aria-pressed)");
    if ((await offBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Traffic On and Off both pressed (aria-pressed)");
    }
    await clickHudToolbarButton(offBtn);
    await expectAriaPressed(offBtn, "true", tag, "Traffic Off button did not activate (aria-pressed)");
    if ((await onBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Traffic On and Off both pressed after Off click");
    }
    pass(tag, "traffic toggle On/Off mutually exclusive");
  } else {
    console.log(`[${tag}] SKIP: traffic overlay toggle not on page`);
  }
}

/** @param {import('playwright').Page} page @param {string} tag */
async function readDiagnosticsHud(page) {
  return page.locator("div").filter({ hasText: "Diagnostics" }).first().innerText();
}

/** @param {string} hudText */
function parseHudRenderStats(hudText) {
  const buildingsMatch = hudText.match(/Buildings:\s*(\d+)\/(\d+)/);
  const chunksMatch = hudText.match(/Chunks:\s*(\d+)\/(\d+)/);
  const fpsMatch = hudText.match(/FPS:\s*(\d+|—)/);
  return {
    visibleBuildings: Number(buildingsMatch?.[1] ?? 0),
    totalBuildings: Number(buildingsMatch?.[2] ?? 0),
    visibleChunks: Number(chunksMatch?.[1] ?? 0),
    fps: fpsMatch?.[1] ?? "—",
  };
}

/**
 * Canvas render health — HUD is authoritative in Playwright.
 *
 * Headless/headed Chromium automation cannot read WebGL canvas pixels: readPixels,
 * canvas2d drawImage, and toDataURL stay all-zero even when interactive browsers
 * show the city (see CanvasRenderHealth for prod EffectComposer guardrails).
 *
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertCanvasRenderHealth(page, tag) {
  const deadline = Date.now() + 30_000;
  let hudText = "";
  let stats = { visibleBuildings: 0, totalBuildings: 0, visibleChunks: 0, fps: "—" };

  while (Date.now() < deadline) {
    hudText = await readDiagnosticsHud(page);
    stats = parseHudRenderStats(hudText);
    if (stats.visibleBuildings > 0 && stats.visibleChunks > 0 && stats.totalBuildings > 0) {
      break;
    }
    await page.waitForTimeout(500);
  }

  const pixelSum = await page.evaluate(async () => {
    const sampleReadPixels = () => {
      const canvas = document.querySelector('[data-testid="city-canvas"] canvas');
      if (!canvas) return 0;
      const gl =
        canvas.getContext("webgl2", { preserveDrawingBuffer: true }) ??
        canvas.getContext("webgl", { preserveDrawingBuffer: true });
      if (!gl) return 0;
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      gl.finish();
      const buf = new Uint8Array(4);
      const x = Math.max(0, Math.floor(canvas.width / 2) - 1);
      const y = Math.max(0, Math.floor(canvas.height / 2) - 1);
      gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, buf);
      return buf[0] + buf[1] + buf[2];
    };

    const afterFrame = () =>
      new Promise((resolve) => {
        requestAnimationFrame(() => {
          requestAnimationFrame(() => resolve(sampleReadPixels()));
        });
      });

    let best = 0;
    for (let attempt = 0; attempt < 4; attempt++) {
      best = Math.max(best, await afterFrame());
      if (best > 0) break;
      await new Promise((r) => setTimeout(r, 250));
    }
    return best;
  });

  if (stats.visibleBuildings > 0 && stats.visibleChunks > 0 && stats.totalBuildings > 0) {
    if (pixelSum > 0) {
      pass(tag, "main canvas has non-zero pixels after load");
    } else {
      console.warn(
        `[${tag}] NOTE: readPixels zero in Playwright (WebGL buffer not readable in automation); HUD confirms ${stats.visibleBuildings}/${stats.totalBuildings} buildings, ${stats.visibleChunks} chunks`,
      );
      pass(
        tag,
        `canvas render verified via HUD (${stats.visibleBuildings}/${stats.totalBuildings} buildings, ${stats.visibleChunks} chunks)`,
      );
    }
    return;
  }

  if (pixelSum > 0) {
    pass(tag, "main canvas has non-zero pixels (HUD still warming up)");
    return;
  }

  fail(
    tag,
    `Canvas not rendering (HUD: buildings ${stats.visibleBuildings}/${stats.totalBuildings}, chunks ${stats.visibleChunks}, fps ${stats.fps})`,
  );
}

/**
 * Deep /play checks — WebGL canvas, HUD, COOP/COEP, sim tick.
 * @param {import('playwright').Page} page
 * @param {{ tag?: string; screenshotPath?: string }} [options]
 */
export async function runPlayChecks(page, options = {}) {
  const tag = options.tag ?? "smoke-play";

  const coopRes = await fetch(`${BASE_URL}/play`, { redirect: "follow" });
  const coop = coopRes.headers.get("cross-origin-opener-policy");
  const coep = coopRes.headers.get("cross-origin-embedder-policy");
  if (coop !== "same-origin" || coep !== "require-corp") {
    fail(
      tag,
      `COOP/COEP missing or wrong (got COOP=${coop ?? "null"}, COEP=${coep ?? "null"})`,
    );
  }
  pass(tag, "COOP/COEP headers on /play");

  const consoleErrors = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") consoleErrors.push(msg.text());
  });
  page.on("pageerror", (err) => {
    consoleErrors.push(String(err));
  });

  await page.addInitScript(() => {
    window.localStorage.setItem("citymajor_onboarding_done", "1");
    window.localStorage.setItem("citymajor_smoke", "1");
    // Traffic overlay defaults to on when unset — pin off for deterministic toolbar smoke.
    window.localStorage.setItem("citymajor_traffic_overlay", "off");
    // High quality enables EffectComposer bloom — pin low so readPixels smoke is stable on CI GPUs.
    window.localStorage.setItem("citymajor_graphics_quality", "low");
  });

  // Match Forge canvas backing store; pixel probes use HUD (readPixels unreliable in Playwright).
  await page.setViewportSize(PLAY_VIEWPORT);
  await page.goto(`${BASE_URL}/play`, { waitUntil: "domcontentloaded" });

  const hud = page.getByText("Diagnostics");
  await hud.waitFor({ state: "visible" });
  pass(tag, "diagnostics HUD visible");

  const wordmark = page.locator(".hud-wordmark__title");
  await wordmark.waitFor({ state: "visible" });
  pass(tag, "CityMajor wordmark visible");

  const canvas = page.locator('[data-testid="city-canvas"] canvas');
  await canvas.waitFor({ state: "visible" });
  pass(tag, "WebGL canvas mounted");

  try {
    await page.waitForFunction(
      () => {
        const el = document.querySelector('[data-testid="city-canvas"] canvas');
        const engine = el?.getAttribute("data-engine");
        return typeof engine === "string" && engine.startsWith("three.js");
      },
      undefined,
      { timeout: 15_000 },
    );
  } catch {
    fail(tag, "Main play canvas missing Three.js WebGL renderer");
  }
  pass(tag, "WebGL context created");

  await assertCanvasRenderHealth(page, tag);

  const savesAvailable = await assertSaveApiHealth(tag);
  await assertHudPanels(page, tag);
  await assertEraQuestPanel(page, tag);
  await assertEconomyPanel(page, tag);
  await assertCitizenPanel(page, tag);
  await assertLawPanel(page, tag);
  await assertOptionalOverlayToolbars(page, tag);

  if (options.screenshotPath) {
    await page.screenshot({ path: options.screenshotPath, fullPage: false });
    pass(tag, `screenshot saved: ${options.screenshotPath}`);
  }

  const hudText = await page
    .locator("div")
    .filter({ hasText: "Diagnostics" })
    .first()
    .innerText();

  const dataMatch = hudText.match(/Data:\s*(WASM sim|procedural)/);
  const simSource = dataMatch?.[1] ?? "unknown";
  if (WASM_EXPECTED && simSource !== "WASM sim") {
    fail(
      tag,
      `Expected WASM sim (run pnpm build:wasm first); got Data: ${simSource}`,
    );
  }
  pass(tag, `sim source: ${simSource}`);

  if (savesAvailable) {
    await assertCmjrSaveRoundTrip(tag, page, { simIsWasm: simSource === "WASM sim" });
  }

  const buildingsMatch = hudText.match(/Buildings:\s*(\d+)\/(\d+)/);
  const visible = Number(buildingsMatch?.[1] ?? 0);
  const total = Number(buildingsMatch?.[2] ?? 0);
  if (total < 1) {
    fail(tag, `No buildings in HUD (visible=${visible}, total=${total})`);
  }
  pass(tag, `buildings rendering (${visible}/${total} visible)`);

  const fpsMatch = hudText.match(/FPS:\s*(\d+|—)/);
  const fps = fpsMatch?.[1];
  if (fps && fps !== "—") {
    pass(tag, `FPS snapshot: ${fps}`);
  } else {
    console.warn(
      `[${tag}] WARN: FPS still — (headless GPU may be slow); use pnpm perf:gate for sampled threshold check`,
    );
  }

  if (PERF_GATE) {
    await runPerfGate(page, { tag: `${tag}-perf`, skipNavigate: true });
  }

  // Anything not on the WASM-fallback allowlist should FAIL the smoke run —
  // silently downgrading to `console.warn` masked several real regressions on
  // /play (missing textures, worker crashes) that only surfaced in prod.
  const blocking = consoleErrors.filter(
    (e) => !e.includes("WASM sim unavailable"),
  );
  if (blocking.length > 0) {
    fail(
      tag,
      `Unexpected console errors on /play (${blocking.length}): ${blocking.slice(0, 5).join(" | ")}`,
    );
  }
  pass(tag, "no unexpected console errors");
}
