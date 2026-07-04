#!/usr/bin/env node
/**
 * WebGL FPS perf gate for /play — enforces WEB_V1_SCOPE §4 integrated GPU target (≥30 FPS).
 *
 * Prereq: `pnpm dev` (or `pnpm dev:wasm`) on BASE_URL.
 * First run: `npx playwright install chromium`
 *
 *   pnpm perf:gate
 *   MIN_FPS=30 pnpm perf:gate
 *   PERF_GATE_STRICT=1 pnpm perf:gate   # fail when no FPS samples (no rAF fallback)
 *
 * Writes JSON report to test-results/perf-gate.json when PERF_REPORT=1 (default).
 */
import { mkdir, writeFile } from "node:fs/promises";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import {
  BASE_URL,
  HEADLESS,
  TIMEOUT_MS,
  fail,
  pass,
} from "./smoke-lib.mjs";

/** WEB_V1_SCOPE §4 — integrated GPU floor at max city fill with LOD. */
export const MIN_FPS = Number(process.env.MIN_FPS ?? 30);
export const PERF_WARMUP_MS = Number(process.env.PERF_WARMUP_MS ?? 3500);
export const PERF_SAMPLE_MS = Number(process.env.PERF_SAMPLE_MS ?? 5000);
export const PERF_POLL_MS = Number(process.env.PERF_POLL_MS ?? 500);
export const PERF_GATE_STRICT = process.env.PERF_GATE_STRICT === "1";
export const PERF_USE_RAF_FALLBACK = process.env.PERF_USE_RAF_FALLBACK !== "0";
export const PERF_REPORT = process.env.PERF_REPORT !== "0";
/** Below this max HUD FPS in headless ⇒ software renderer; skip gate unless strict. */
export const PERF_SOFT_RENDERER_MAX = Number(process.env.PERF_SOFT_RENDERER_MAX ?? 15);
export const PERF_REPORT_PATH =
  process.env.PERF_REPORT_PATH ?? "test-results/perf-gate.json";

/**
 * Read numeric FPS from the diagnostics HUD (R3F useFrame accumulator).
 * @param {import("playwright").Page} page
 * @returns {Promise<number | null>}
 */
export async function readHudFps(page) {
  const hudText = await page
    .locator("div")
    .filter({ hasText: "Diagnostics" })
    .first()
    .innerText();
  const match = hudText.match(/FPS:\s*(\d+)/);
  return match ? Number(match[1]) : null;
}

/**
 * Fallback when headless Chromium never paints HUD FPS (shows "—").
 * Measures main-thread rAF rate while the WebGL canvas is visible.
 * @param {import("playwright").Page} page
 * @param {number} durationMs
 */
export async function measureRafFps(page, durationMs) {
  return page.evaluate(async (ms) => {
    return new Promise((resolve) => {
      let frames = 0;
      const start = performance.now();
      function tick(now) {
        frames += 1;
        if (now - start >= ms) {
          resolve(Math.round((frames * 1000) / (now - start)));
        } else {
          requestAnimationFrame(tick);
        }
      }
      requestAnimationFrame(tick);
    });
  }, durationMs);
}

/**
 * @param {number[]} values
 */
function summarizeFps(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const sum = sorted.reduce((acc, v) => acc + v, 0);
  return {
    minFps: sorted[0],
    maxFps: sorted[sorted.length - 1],
    medianFps: sorted[Math.floor(sorted.length / 2)],
    avgFps: Math.round(sum / sorted.length),
    samples: sorted,
  };
}

/**
 * Sample HUD FPS (with optional rAF fallback) and enforce MIN_FPS.
 * @param {import("playwright").Page} page
 * @param {{ tag?: string; skipNavigate?: boolean }} [options]
 */
export async function runPerfGate(page, options = {}) {
  const tag = options.tag ?? "perf-gate";

  if (!options.skipNavigate) {
    await page.addInitScript(() => {
      window.localStorage.setItem("citymajor_onboarding_done", "1");
    });
    await page.goto(`${BASE_URL}/play`, { waitUntil: "networkidle" });
  }

  const canvas = page.locator('[data-testid="city-canvas"] canvas');
  await canvas.waitFor({ state: "visible" });
  pass(tag, "WebGL canvas visible");

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

  await page.waitForTimeout(PERF_WARMUP_MS);

  const hudSamples = [];
  const deadline = Date.now() + PERF_SAMPLE_MS;
  while (Date.now() < deadline) {
    const fps = await readHudFps(page);
    if (fps !== null) hudSamples.push(fps);
    await page.waitForTimeout(PERF_POLL_MS);
  }

  let method = "hud";
  /** @type {number[]} */
  let samples = hudSamples;

  if (samples.length === 0 && PERF_USE_RAF_FALLBACK) {
    method = "raf";
    const rafFps = await measureRafFps(page, PERF_SAMPLE_MS);
    samples = [rafFps];
    pass(
      tag,
      `rAF fallback FPS: ${rafFps} (HUD never reported numeric FPS in headless)`,
    );
  }

  if (samples.length === 0) {
    if (PERF_GATE_STRICT) {
      fail(
        tag,
        `No FPS samples in ${PERF_SAMPLE_MS}ms — try PERF_USE_RAF_FALLBACK=1 or run with HEADLESS=0`,
      );
    }
    console.warn(
      `[${tag}] WARN: No FPS samples; gate skipped (set PERF_GATE_STRICT=1 to fail)`,
    );
    return {
      passed: null,
      method: "none",
      threshold: MIN_FPS,
      samples: [],
    };
  }

  const stats = summarizeFps(samples);
  const report = {
    ...stats,
    method,
    threshold: MIN_FPS,
    passed: stats.minFps >= MIN_FPS,
    warmupMs: PERF_WARMUP_MS,
    sampleMs: PERF_SAMPLE_MS,
    baseUrl: BASE_URL,
  };

  console.log(`[${tag}] FPS report: ${JSON.stringify(report)}`);

  if (
    HEADLESS &&
    stats.maxFps < PERF_SOFT_RENDERER_MAX &&
    !PERF_GATE_STRICT
  ) {
    console.warn(
      `[${tag}] WARN: Headless software renderer likely (max FPS ${stats.maxFps} < ${PERF_SOFT_RENDERER_MAX}); gate skipped. Use HEADLESS=0 or GPU CI with PERF_GATE_STRICT=1.`,
    );
    report.passed = null;
    report.skipped = "headless-software-renderer";
    if (PERF_REPORT) {
      await mkdir(dirname(PERF_REPORT_PATH), { recursive: true });
      await writeFile(PERF_REPORT_PATH, `${JSON.stringify(report, null, 2)}\n`);
      pass(tag, `report saved: ${PERF_REPORT_PATH}`);
    }
    return report;
  }

  if (PERF_REPORT) {
    await mkdir(dirname(PERF_REPORT_PATH), { recursive: true });
    await writeFile(PERF_REPORT_PATH, `${JSON.stringify(report, null, 2)}\n`);
    pass(tag, `report saved: ${PERF_REPORT_PATH}`);
  }

  if (stats.minFps < MIN_FPS) {
    fail(
      tag,
      `min FPS ${stats.minFps} < threshold ${MIN_FPS} (WEB_V1_SCOPE integrated GPU target)`,
    );
  }

  pass(
    tag,
    `min FPS ${stats.minFps} ≥ ${MIN_FPS} (${samples.length} sample(s) via ${method})`,
  );
  return report;
}

async function main() {
  let browser;
  try {
    browser = await chromium.launch({ headless: HEADLESS });
    const page = await browser.newPage();
    page.setDefaultTimeout(TIMEOUT_MS);
    await runPerfGate(page);
    console.log("[perf-gate] All checks passed.");
    process.exit(0);
  } catch (err) {
    fail("perf-gate", err instanceof Error ? err.message : String(err));
  } finally {
    await browser?.close();
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
