#!/usr/bin/env node
/**
 * Full web smoke suite: /, /shop, /play (HTTP 200 + key copy + WebGL on /play).
 *
 * Prereq: pnpm dev (or pnpm dev:wasm) on BASE_URL (default http://localhost:3000)
 *
 *   pnpm smoke:all
 *   SCREENSHOT=1 pnpm smoke:all   # saves test-results/smoke-play.png
 *
 * First run: npx playwright install chromium
 */
import { mkdir } from "node:fs/promises";
import { chromium } from "playwright";
import {
  BASE_URL,
  HEADLESS,
  SCREENSHOT,
  TIMEOUT_MS,
  assertHttpOk,
  fail,
  pass,
} from "./smoke-lib.mjs";
import { runPlayChecks } from "./smoke-play-checks.mjs";

const TAG = "smoke-all";

/** @type {{ path: string; text: string; label: string }[]} */
const ROUTE_COPY = [
  { path: "/", text: "Build your city across 200 years", label: "home hero" },
  { path: "/shop", text: "CityMajor Shop", label: "shop title" },
  { path: "/shop", text: "Founder Pass", label: "founder pass product" },
];

async function assertRouteCopy(page, path, text, label) {
  await page.goto(`${BASE_URL}${path}`, { waitUntil: "domcontentloaded" });
  const locator = page.getByText(text, { exact: false }).first();
  await locator.waitFor({ state: "visible" });
  pass(TAG, `${path} shows ${label}`);
}

async function main() {
  let browser;
  try {
    for (const route of ["/", "/shop", "/play"]) {
      await assertHttpOk(TAG, route);
    }

    browser = await chromium.launch({ headless: HEADLESS });
    const page = await browser.newPage();
    page.setDefaultTimeout(TIMEOUT_MS);

    for (const { path, text, label } of ROUTE_COPY) {
      await assertRouteCopy(page, path, text, label);
    }

    if (SCREENSHOT) {
      await mkdir("test-results", { recursive: true });
    }

    await runPlayChecks(page, {
      tag: TAG,
      screenshotPath: SCREENSHOT ? "test-results/smoke-play.png" : undefined,
    });

    console.log(`[${TAG}] All checks passed.`);
    process.exit(0);
  } catch (err) {
    fail(TAG, err instanceof Error ? err.message : String(err));
  } finally {
    await browser?.close();
  }
}

main();
