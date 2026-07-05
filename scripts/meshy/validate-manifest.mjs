#!/usr/bin/env node
/**
 * Validate scripts/meshy/manifest.json against on-disk GLBs and gltf-catalog.ts.
 *
 * Usage:
 *   node scripts/meshy/validate-manifest.mjs
 *   node scripts/meshy/validate-manifest.mjs --no-disk   # schema + catalog only
 */
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(__dirname, "../..");
const MANIFEST_PATH = join(__dirname, "manifest.json");
const OUT_ROOT = join(REPO_ROOT, "web/public/assets/gltf");
const CATALOG_PATH = join(REPO_ROOT, "web/lib/gltf-catalog.ts");

const VALID_CATEGORIES = new Set(["res_low", "res_high", "com", "ind", "svc"]);
const VALID_ERAS = new Set(["frontier", "industrial", "postwar", "modern", "future"]);

/**
 * @param {boolean} requireOnDisk
 * @returns {{ ok: boolean; errors: string[]; jobCount: number }}
 */
export function validateManifest({ requireOnDisk = true } = {}) {
  const errors = [];
  /** @type {{ jobs: { key: string; prompt: string; category: string; era: string }[] }} */
  const manifest = JSON.parse(readFileSync(MANIFEST_PATH, "utf8"));

  if (!Array.isArray(manifest.jobs) || manifest.jobs.length === 0) {
    errors.push("manifest.jobs must be a non-empty array");
    return { ok: false, errors, jobCount: 0 };
  }

  const manifestKeys = new Set();
  const manifestRels = new Set();

  for (const job of manifest.jobs) {
    if (!job?.key) {
      errors.push("job missing key");
      continue;
    }
    if (manifestKeys.has(job.key)) {
      errors.push(`duplicate manifest key: ${job.key}`);
    }
    manifestKeys.add(job.key);

    if (!job.prompt?.trim()) {
      errors.push(`${job.key}: missing prompt`);
    }
    if (!VALID_CATEGORIES.has(job.category)) {
      errors.push(`${job.key}: invalid category "${job.category}"`);
    }
    if (!VALID_ERAS.has(job.era)) {
      errors.push(`${job.key}: invalid era "${job.era}"`);
    }
    const prefix = `${job.category}_${job.era}_`;
    if (!job.key.startsWith(prefix)) {
      errors.push(`${job.key}: key must start with "${prefix}"`);
    }

    const rel = `${job.era}/${job.key}.glb`;
    manifestRels.add(rel);
    if (requireOnDisk) {
      const abs = join(OUT_ROOT, rel);
      if (!existsSync(abs)) {
        errors.push(`missing GLB: web/public/assets/gltf/${rel}`);
      }
    }
  }

  if (requireOnDisk && existsSync(OUT_ROOT)) {
    /** @param {string} dir @param {string} [rel] */
    function walk(dir, rel = "") {
      for (const ent of readdirSync(dir, { withFileTypes: true })) {
        const childRel = rel ? `${rel}/${ent.name}` : ent.name;
        if (ent.isDirectory()) {
          if (ent.name === "heroes") continue;
          walk(join(dir, ent.name), childRel);
        } else if (ent.name.endsWith(".glb") && !manifestRels.has(childRel)) {
          errors.push(`orphan GLB (not in manifest): web/public/assets/gltf/${childRel}`);
        }
      }
    }
    walk(OUT_ROOT);
  }

  if (existsSync(CATALOG_PATH)) {
    const catalogSrc = readFileSync(CATALOG_PATH, "utf8");
    const shippedBlock = catalogSrc.split("SHIPPED_GLTF_KEYS")[1]?.split("]")[0] ?? "";
    const catalogKeys = [...shippedBlock.matchAll(/"([a-z_0-9]+)"/g)].map((m) => m[1]);

    for (const key of catalogKeys) {
      if (!manifestKeys.has(key)) {
        errors.push(`gltf-catalog key missing from manifest: ${key}`);
      }
    }
    for (const key of manifestKeys) {
      if (!catalogKeys.includes(key)) {
        errors.push(`manifest key missing from gltf-catalog SHIPPED_GLTF_KEYS: ${key}`);
      }
    }
  } else {
    errors.push(`missing catalog: web/lib/gltf-catalog.ts`);
  }

  return { ok: errors.length === 0, errors, jobCount: manifest.jobs.length };
}

function parseArgs(argv) {
  return { requireOnDisk: !argv.includes("--no-disk") };
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  const result = validateManifest(opts);

  if (result.ok) {
    console.log(
      `OK: ${result.jobCount} manifest job(s) aligned with web/public/assets/gltf and gltf-catalog.ts`,
    );
    return;
  }

  console.error(`Manifest validation failed (${result.errors.length} issue(s)):`);
  for (const err of result.errors) {
    console.error(`  - ${err}`);
  }
  process.exit(1);
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  main();
}
