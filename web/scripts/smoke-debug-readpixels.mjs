#!/usr/bin/env node
/** One-off readPixels diagnostic — not committed to CI. */
import { chromium } from "playwright";
import { BASE_URL, PLAY_VIEWPORT } from "./smoke-lib.mjs";

const HEADLESS = process.env.HEADLESS !== "0";
const WAIT_MS = Number(process.env.WAIT_MS ?? 8000);
const URL = process.env.BASE_URL ?? "https://citymajor.apps.softblaze.net";

async function probe(page, label) {
  const diag = await page.evaluate(async (waitMs) => {
    const canvas = document.querySelector('[data-testid="city-canvas"] canvas');
    const rect = canvas?.getBoundingClientRect();
    const css = canvas
      ? { w: rect?.width, h: rect?.height, cw: canvas.clientWidth, ch: canvas.clientHeight, bw: canvas.width, bh: canvas.height }
      : null;

    const sample = () => {
      if (!canvas) return { sum: 0, buf: [0, 0, 0, 0], err: "no canvas" };
      const gl =
        canvas.getContext("webgl2", { preserveDrawingBuffer: true }) ??
        canvas.getContext("webgl", { preserveDrawingBuffer: true });
      if (!gl) return { sum: 0, buf: [0, 0, 0, 0], err: "no gl" };
      const buf = new Uint8Array(4);
      const x = Math.max(0, Math.floor(canvas.width / 2) - 1);
      const y = Math.max(0, Math.floor(canvas.height / 2) - 1);
      gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, buf);
      return { sum: buf[0] + buf[1] + buf[2], buf: [...buf], x, y, err: null };
    };

    const afterFrame = () =>
      new Promise((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve(sample())));
      });

    const timeline = [];
    for (let i = 0; i < Math.ceil(waitMs / 500); i++) {
      timeline.push({ t: i * 500, ...(await afterFrame()) });
      await new Promise((r) => setTimeout(r, 500));
    }

    const hud = document.body.innerText.match(/Buildings:\s*(\d+)\/(\d+)/);
    const fps = document.body.innerText.match(/FPS:\s*(\S+)/);
    const data = document.body.innerText.match(/Data:\s*(\S+)/);

    return {
      css,
      engine: canvas?.getAttribute("data-engine") ?? null,
      timeline,
      hud: { buildings: hud?.[0], fps: fps?.[1], data: data?.[1] },
    };
  }, WAIT_MS);

  console.log(`\n=== ${label} (headless=${HEADLESS}) ===`);
  console.log(JSON.stringify(diag, null, 2));
  return diag;
}

async function runOnce(headless) {
  const browser = await chromium.launch({ headless });
  const page = await browser.newPage();
  const failed404 = [];
  page.on("response", (r) => {
    if (r.status() === 404 && r.url().includes(".glb")) failed404.push(r.url());
  });
  const consoleErrors = [];
  page.on("console", (m) => {
    if (m.type() === "error") consoleErrors.push(m.text());
  });

  await page.addInitScript(() => {
    window.localStorage.setItem("citymajor_onboarding_done", "1");
    window.localStorage.setItem("citymajor_traffic_overlay", "off");
    window.localStorage.setItem("citymajor_graphics_quality", "low");
  });
  await page.setViewportSize(PLAY_VIEWPORT);
  await page.goto(`${URL}/play`, { waitUntil: "domcontentloaded" });
  await page.getByText("Diagnostics").waitFor({ state: "visible", timeout: 30_000 });

  const diag = await probe(page, `${URL}/play`);
  await page.screenshot({ path: `/tmp/smoke-debug-${headless ? "headless" : "headed"}.png` });
  console.log(`screenshot: /tmp/smoke-debug-${headless ? "headless" : "headed"}.png`);
  console.log("404 glbs:", failed404.slice(0, 10));
  console.log("console errors:", consoleErrors.slice(0, 8));
  await browser.close();
  return diag;
}

// Headless then headed
const h = await runOnce(HEADLESS);
if (process.env.COMPARE_HEADED === "1") {
  await runOnce(false);
}

const last = h.timeline?.[h.timeline.length - 1];
process.exit(last?.sum > 0 ? 0 : 1);
