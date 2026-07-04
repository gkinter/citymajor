#!/usr/bin/env node
/**
 * Batch-generate building GLBs via Meshy text-to-3d (preview → refine → download).
 * Post-processes each GLB to pivot at ground-center using @gltf-transform/core.
 *
 * Usage:
 *   MESHY_API_KEY=msk_… node scripts/meshy/batch-generate.mjs
 *   node scripts/meshy/batch-generate.mjs --dry-run
 *
 * See docs/MESHY_ASSET_PIPELINE.md
 */
import { mkdirSync, readFileSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Document, NodeIO, getBounds } from "@gltf-transform/core";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(__dirname, "../..");
const MANIFEST_PATH = join(__dirname, "manifest.json");
const OUT_ROOT = join(REPO_ROOT, "web/public/assets/gltf");

const MESHY_BASE = "https://api.meshy.ai/openapi/v2/text-to-3d";
const DEFAULT_POLL_MS = 5_000;
const DEFAULT_TARGET_POLYCOUNT = 8_000;

/** @typedef {{ key: string; prompt: string; category: string; era: string }} MeshyJob */
/** @typedef {{ pipeline_version: string; meshy_model: string; export_format: string; jobs: MeshyJob[] }} MeshyManifest */

/**
 * @param {string[]} argv
 */
function parseArgs(argv) {
  const opts = {
    dryRun: false,
    only: null,
    skipExisting: false,
    limit: Infinity,
    pollMs: DEFAULT_POLL_MS,
    previewOnly: false,
  };

  for (const arg of argv) {
    if (arg === "--dry-run" || arg === "-n") opts.dryRun = true;
    else if (arg === "--skip-existing") opts.skipExisting = true;
    else if (arg === "--preview-only") opts.previewOnly = true;
    else if (arg.startsWith("--only=")) opts.only = arg.slice("--only=".length);
    else if (arg.startsWith("--limit=")) opts.limit = Number(arg.slice("--limit=".length));
    else if (arg.startsWith("--poll-ms=")) opts.pollMs = Number(arg.slice("--poll-ms=".length));
    else if (arg === "--help" || arg === "-h") {
      printHelp();
      process.exit(0);
    } else {
      console.error(`Unknown argument: ${arg}`);
      printHelp();
      process.exit(1);
    }
  }

  return opts;
}

function printHelp() {
  console.log(`CityMajor Meshy batch generator

Usage:
  node scripts/meshy/batch-generate.mjs [options]

Options:
  --dry-run, -n       Plan jobs without calling Meshy (default when MESHY_API_KEY unset)
  --only=<key>        Process a single manifest job by archetype key
  --skip-existing     Skip jobs whose output GLB already exists
  --limit=<n>         Process at most N jobs
  --poll-ms=<ms>      Poll interval for task status (default ${DEFAULT_POLL_MS})
  --preview-only      Stop after preview download (no refine credits spent)
  --help, -h          Show this help

Environment:
  MESHY_API_KEY       Meshy API bearer token (required unless --dry-run)
`);
}

/**
 * @returns {MeshyManifest}
 */
function loadManifest() {
  const raw = readFileSync(MANIFEST_PATH, "utf8");
  return JSON.parse(raw);
}

/**
 * @param {string} apiKey
 * @param {string} method
 * @param {string} [pathSuffix]
 * @param {Record<string, unknown>} [body]
 */
async function meshyFetch(apiKey, method, pathSuffix = "", body) {
  const url = pathSuffix ? `${MESHY_BASE}/${pathSuffix}` : MESHY_BASE;
  const res = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  const text = await res.text();
  let data;
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    throw new Error(`Meshy ${method} ${url} returned non-JSON (${res.status}): ${text.slice(0, 200)}`);
  }

  if (!res.ok) {
    const detail = data?.message ?? data?.error ?? text.slice(0, 300);
    throw new Error(`Meshy ${method} ${url} failed (${res.status}): ${detail}`);
  }

  return data;
}

/**
 * @param {string} apiKey
 * @param {string} taskId
 * @param {number} pollMs
 */
async function pollTask(apiKey, taskId, pollMs) {
  for (;;) {
    const task = await meshyFetch(apiKey, "GET", taskId);
    const status = task.status;
    const progress = task.progress ?? 0;

    if (status === "SUCCEEDED") return task;
    if (status === "FAILED") {
      const msg = task.task_error?.message ?? "unknown error";
      throw new Error(`Meshy task ${taskId} failed: ${msg}`);
    }

    process.stdout.write(`  … ${status} ${progress}%\r`);
    await sleep(pollMs);
  }
}

/**
 * @param {string} url
 * @returns {Promise<Uint8Array>}
 */
async function downloadBinary(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`Download failed (${res.status}): ${url}`);
  }
  const buf = await res.arrayBuffer();
  return new Uint8Array(buf);
}

/**
 * @param {Document} document
 * @returns {import('@gltf-transform/core').Scene}
 */
function getScene(document) {
  const root = document.getRoot();
  const scene = root.getDefaultScene() ?? root.listScenes()[0];
  if (!scene) throw new Error("GLB has no scene");
  return scene;
}

/**
 * Shift geometry so the footprint center sits on X/Z origin and the lowest
 * vertex rests on Y=0 (ground-center pivot per BUILDING_ARCHETYPE_3D).
 *
 * @param {Document} document
 */
function pivotToGroundCenter(document) {
  const scene = getScene(document);

  const { min, max } = getBounds(scene);
  if (!Number.isFinite(min[0])) {
    throw new Error("GLB scene has no mesh positions");
  }

  const tx = -((min[0] + max[0]) / 2);
  const ty = -min[1];
  const tz = -((min[2] + max[2]) / 2);

  if (tx === 0 && ty === 0 && tz === 0) return;

  const offsetNode = document.createNode().setTranslation([tx, ty, tz]);
  for (const child of [...scene.listChildren()]) {
    offsetNode.addChild(child);
  }
  scene.addChild(offsetNode);
}

/**
 * @param {Uint8Array} glbBytes
 * @returns {Promise<Document>}
 */
async function fixPivot(glbBytes) {
  const io = new NodeIO();
  const document = await io.readBinary(glbBytes);
  pivotToGroundCenter(document);
  return document;
}

/**
 * @param {string} apiKey
 * @param {MeshyJob} job
 * @param {MeshyManifest} manifest
 * @param {ReturnType<typeof parseArgs>} opts
 */
async function runJob(apiKey, job, manifest, opts) {
  const outDir = join(OUT_ROOT, job.era);
  const outPath = join(outDir, `${job.key}.glb`);

  if (opts.skipExisting && existsSync(outPath)) {
    console.log(`skip ${job.key} (exists)`);
    return;
  }

  console.log(`\n▶ ${job.key}`);
  console.log(`  prompt: ${job.prompt.slice(0, 80)}${job.prompt.length > 80 ? "…" : ""}`);

  const previewBody = {
    mode: "preview",
    prompt: job.prompt,
    ai_model: manifest.meshy_model,
    should_remesh: true,
    topology: "triangle",
    target_polycount: DEFAULT_TARGET_POLYCOUNT,
    target_formats: ["glb"],
    moderation: false,
  };

  const previewCreate = await meshyFetch(apiKey, "POST", "", previewBody);
  const previewId = previewCreate.result;
  console.log(`  preview task: ${previewId}`);

  const previewTask = await pollTask(apiKey, previewId, opts.pollMs);
  const previewUrl = previewTask.model_urls?.glb;
  if (!previewUrl) throw new Error(`Preview ${previewId} missing model_urls.glb`);

  if (opts.previewOnly) {
    console.log(`  preview-only: ${previewUrl}`);
    return;
  }

  const refineBody = {
    mode: "refine",
    preview_task_id: previewId,
    enable_pbr: true,
    ai_model: manifest.meshy_model,
    target_formats: ["glb"],
    remove_lighting: true,
  };

  const refineCreate = await meshyFetch(apiKey, "POST", "", refineBody);
  const refineId = refineCreate.result;
  console.log(`  refine task: ${refineId}`);

  const refineTask = await pollTask(apiKey, refineId, opts.pollMs);
  const refineUrl = refineTask.model_urls?.glb;
  if (!refineUrl) throw new Error(`Refine ${refineId} missing model_urls.glb`);

  console.log(`  download → ${outPath}`);
  const glbBytes = await downloadBinary(refineUrl);
  const document = await fixPivot(glbBytes);

  mkdirSync(outDir, { recursive: true });
  const io = new NodeIO();
  await io.write(outPath, document);
  console.log(`  ✓ wrote ${outPath}`);
}

/**
 * @param {MeshyJob} job
 * @param {MeshyManifest} manifest
 */
function dryRunJob(job, manifest) {
  const outPath = join(OUT_ROOT, job.era, `${job.key}.glb`);
  console.log(`[dry-run] ${job.key}`);
  console.log(`  era/category: ${job.era} / ${job.category}`);
  console.log(`  model: ${manifest.meshy_model} (preview 20cr + refine 10cr)`);
  console.log(`  prompt: ${job.prompt}`);
  console.log(`  output: ${outPath}`);
  console.log(`  post: pivotToGroundCenter via @gltf-transform/core`);
}

/**
 * @param {number} ms
 */
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const manifest = loadManifest();
  const apiKey = process.env.MESHY_API_KEY?.trim() ?? "";
  const dryRun = opts.dryRun || !apiKey;

  if (!dryRun && !apiKey) {
    console.error("MESHY_API_KEY is required (or pass --dry-run).");
    process.exit(1);
  }

  let jobs = manifest.jobs;
  if (opts.only) {
    jobs = jobs.filter((j) => j.key === opts.only);
    if (jobs.length === 0) {
      console.error(`No manifest job with key "${opts.only}"`);
      process.exit(1);
    }
  }
  if (Number.isFinite(opts.limit)) {
    jobs = jobs.slice(0, opts.limit);
  }

  console.log(
    dryRun
      ? `Dry-run: ${jobs.length} job(s) from ${MANIFEST_PATH}`
      : `Generating ${jobs.length} asset(s) via Meshy (${manifest.meshy_model})`,
  );

  let failures = 0;

  for (const job of jobs) {
    try {
      if (dryRun) {
        dryRunJob(job, manifest);
      } else {
        await runJob(apiKey, job, manifest, opts);
      }
    } catch (err) {
      failures += 1;
      console.error(`  ✗ ${job.key}:`, err instanceof Error ? err.message : err);
    }
  }

  if (failures > 0) {
    console.error(`\n${failures} job(s) failed.`);
    process.exit(1);
  }

  if (dryRun) {
    console.log(`\nDry-run complete. Set MESHY_API_KEY to generate for real.`);
  } else {
    console.log(`\nBatch complete.`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
