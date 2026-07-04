#!/usr/bin/env node
/**
 * Headless smoke test for /play — page boot, WebGL canvas, HUD, COOP/COEP.
 *
 * Prereq (terminal 1):
 *   unset NODE_OPTIONS          # Beast: emcc rejects --max-semi-space-size
 *   pnpm build:wasm && pnpm dev # or: pnpm dev:wasm
 *
 * Run (terminal 2, from web/ or repo root):
 *   pnpm smoke:play
 *   BASE_URL=http://127.0.0.1:3000 node web/scripts/smoke-play.mjs
 *
 * First run: npx playwright install chromium
 */
import { chromium } from "playwright";

const BASE_URL = (process.env.BASE_URL ?? "http://localhost:3000").replace(
  /\/$/,
  "",
);
const TIMEOUT_MS = Number(process.env.TIMEOUT_MS ?? 60_000);
const HEADLESS = process.env.HEADLESS !== "0";
const WASM_EXPECTED = process.env.WASM_EXPECTED === "1";

function fail(message) {
  console.error(`[smoke-play] FAIL: ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`[smoke-play] OK: ${message}`);
}

async function main() {
  let browser;
  try {
    const res = await fetch(`${BASE_URL}/play`, { redirect: "follow" });
    if (!res.ok) {
      fail(`/play returned HTTP ${res.status} — is \`pnpm dev\` running?`);
    }
    const coop = res.headers.get("cross-origin-opener-policy");
    const coep = res.headers.get("cross-origin-embedder-policy");
    if (coop !== "same-origin" || coep !== "require-corp") {
      fail(
        `COOP/COEP missing or wrong (got COOP=${coop ?? "null"}, COEP=${coep ?? "null"})`,
      );
    }
    pass("COOP/COEP headers on /play");

    browser = await chromium.launch({ headless: HEADLESS });
    const page = await browser.newPage();
    page.setDefaultTimeout(TIMEOUT_MS);

    const consoleErrors = [];
    page.on("console", (msg) => {
      if (msg.type() === "error") consoleErrors.push(msg.text());
    });
    page.on("pageerror", (err) => {
      consoleErrors.push(String(err));
    });

    await page.goto(`${BASE_URL}/play`, { waitUntil: "networkidle" });

    const hud = page.getByText("CityMajor Web M0");
    await hud.waitFor({ state: "visible" });
    pass("HUD visible");

    const canvas = page.locator("canvas").first();
    await canvas.waitFor({ state: "visible" });
    pass("WebGL canvas mounted");

    const webgl = await page.evaluate(() => {
      const el = document.querySelector("canvas");
      if (!el) return null;
      return !!(
        el.getContext("webgl2") ??
        el.getContext("webgl") ??
        el.getContext("experimental-webgl")
      );
    });
    if (!webgl) fail("No WebGL context on canvas");
    pass("WebGL context created");

    // Allow R3F + sim worker to tick; FPS HUD updates every ~500ms.
    await page.waitForTimeout(3500);

    const hudText = await page
      .locator("div")
      .filter({ hasText: "CityMajor Web M0" })
      .first()
      .innerText();

    const dataMatch = hudText.match(/Data:\s*(WASM sim|procedural)/);
    const simSource = dataMatch?.[1] ?? "unknown";
    if (WASM_EXPECTED && simSource !== "WASM sim") {
      fail(
        `Expected WASM sim (run pnpm build:wasm first); got Data: ${simSource}`,
      );
    }
    pass(`sim source: ${simSource}`);

    const buildingsMatch = hudText.match(/Buildings:\s*(\d+)\/(\d+)/);
    const visible = Number(buildingsMatch?.[1] ?? 0);
    const total = Number(buildingsMatch?.[2] ?? 0);
    if (total < 1) {
      fail(`No buildings in HUD (visible=${visible}, total=${total})`);
    }
    pass(`buildings rendering (${visible}/${total} visible)`);

    const fpsMatch = hudText.match(/FPS:\s*(\d+|—)/);
    const fps = fpsMatch?.[1];
    if (fps && fps !== "—") {
      pass(`FPS reported: ${fps}`);
    } else {
      console.warn(
        "[smoke-play] WARN: FPS still — (headless GPU may be slow); canvas + HUD OK",
      );
    }

    const blocking = consoleErrors.filter(
      (e) => !e.includes("WASM sim unavailable"),
    );
    if (blocking.length > 0) {
      console.warn("[smoke-play] console errors:", blocking.slice(0, 5));
    }

    console.log("[smoke-play] All checks passed.");
    process.exit(0);
  } catch (err) {
    fail(err instanceof Error ? err.message : String(err));
  } finally {
    await browser?.close();
  }
}

main();
