import { BASE_URL, fail, pass, WASM_EXPECTED } from "./smoke-lib.mjs";
import { runPerfGate } from "./perf-gate.mjs";

const PERF_GATE = process.env.PERF_GATE === "1";

/**
 * GET /api/saves — list endpoint must return a well-formed envelope.
 * @param {string} tag
 */
export async function assertSaveApiHealth(tag) {
  const res = await fetch(`${BASE_URL}/api/saves`, { redirect: "follow" });
  if (!res.ok) {
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
 * Optional Services toolbar + Traffic toggle — skipped when UI not mounted.
 * Resolves via data-testid first, then toolbar aria-label / button text.
 * @param {import('playwright').Page} page
 * @param {string} tag
 */
async function assertOptionalOverlayToolbars(page, tag) {
  const servicesToolbar = page
    .locator('[data-testid="services-toolbar"]')
    .or(page.getByRole("toolbar", { name: "Services coverage overlay" }));

  if ((await servicesToolbar.count()) > 0) {
    await servicesToolbar.first().waitFor({ state: "visible" });
    const healthBtn = servicesToolbar.getByRole("button", { name: "Health", exact: true });
    const offBtn = servicesToolbar.getByRole("button", { name: "Off", exact: true });
    await healthBtn.click();
    if ((await healthBtn.getAttribute("aria-pressed")) !== "true") {
      fail(tag, "Services Health button did not activate (aria-pressed)");
    }
    if ((await offBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Services Health and Off both pressed (aria-pressed)");
    }
    pass(tag, "services toolbar present and Health mode toggles");
    await offBtn.click();
  } else {
    console.log(`[${tag}] SKIP: services toolbar not on page`);
  }

  const trafficToolbar = page
    .locator('[data-testid="traffic-overlay-toggle"]')
    .or(page.getByRole("toolbar", { name: "Traffic overlay" }));

  if ((await trafficToolbar.count()) > 0) {
    await trafficToolbar.first().waitFor({ state: "visible" });
    const onBtn = trafficToolbar.getByRole("button", { name: "On", exact: true });
    const offBtn = trafficToolbar.getByRole("button", { name: "Off", exact: true });
    await onBtn.click();
    if ((await onBtn.getAttribute("aria-pressed")) !== "true") {
      fail(tag, "Traffic On button did not activate (aria-pressed)");
    }
    if ((await offBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Traffic On and Off both pressed (aria-pressed)");
    }
    await offBtn.click();
    if ((await offBtn.getAttribute("aria-pressed")) !== "true") {
      fail(tag, "Traffic Off button did not activate (aria-pressed)");
    }
    if ((await onBtn.getAttribute("aria-pressed")) === "true") {
      fail(tag, "Traffic On and Off both pressed after Off click");
    }
    pass(tag, "traffic toggle On/Off mutually exclusive");
  } else {
    console.log(`[${tag}] SKIP: traffic overlay toggle not on page`);
  }
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
  });

  await page.goto(`${BASE_URL}/play`, { waitUntil: "domcontentloaded" });

  const hud = page.getByText("Diagnostics");
  await hud.waitFor({ state: "visible" });
  pass(tag, "diagnostics HUD visible");

  const wordmark = page.locator(".hud-wordmark__title");
  await wordmark.waitFor({ state: "visible" });
  pass(tag, "CityMajor wordmark visible");

  const canvas = page.locator("canvas").first();
  await canvas.waitFor({ state: "visible" });
  pass(tag, "WebGL canvas mounted");

  const webgl = await page.evaluate(() => {
    const el = document.querySelector("canvas");
    if (!el) return null;
    return !!(
      el.getContext("webgl2") ??
      el.getContext("webgl") ??
      el.getContext("experimental-webgl")
    );
  });
  if (!webgl) fail(tag, "No WebGL context on canvas");
  pass(tag, "WebGL context created");

  await page.waitForTimeout(3500);

  await assertSaveApiHealth(tag);
  await assertHudPanels(page, tag);
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
