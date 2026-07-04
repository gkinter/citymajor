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
import {
  BASE_URL,
  HEADLESS,
  SCREENSHOT,
  TIMEOUT_MS,
  assertHttpOk,
  fail,
} from "./smoke-lib.mjs";
import { runPlayChecks } from "./smoke-play-checks.mjs";

async function main() {
  let browser;
  try {
    await assertHttpOk("smoke-play", "/play");

    browser = await chromium.launch({ headless: HEADLESS });
    const page = await browser.newPage();
    page.setDefaultTimeout(TIMEOUT_MS);

    await runPlayChecks(page, {
      screenshotPath: SCREENSHOT ? "test-results/smoke-play.png" : undefined,
    });

    console.log("[smoke-play] All checks passed.");
    process.exit(0);
  } catch (err) {
    fail("smoke-play", err instanceof Error ? err.message : String(err));
  } finally {
    await browser?.close();
  }
}

main();
