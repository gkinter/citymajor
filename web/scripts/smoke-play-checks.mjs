import { BASE_URL, fail, pass, WASM_EXPECTED } from "./smoke-lib.mjs";

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

  await page.goto(`${BASE_URL}/play`, { waitUntil: "networkidle" });

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
    pass(tag, `FPS reported: ${fps}`);
  } else {
    console.warn(
      `[${tag}] WARN: FPS still — (headless GPU may be slow); canvas + HUD OK`,
    );
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
