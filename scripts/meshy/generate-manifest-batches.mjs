#!/usr/bin/env node
/**
 * Generate Meshy manifest batch JSON files from BUILDING_ARCHETYPE_3D taxonomy.
 * Usage: node scripts/meshy/generate-manifest-batches.mjs
 */
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

/** @type {Record<string, Record<string, string>>} */
const PROMPTS = {
  res_low: {
    frontier:
      "A single-story frontier log cabin or wooden house, colonial era, peaked roof, low poly, stylized game asset, isometric view",
    industrial:
      "A single-story 1800s brick rowhouse, industrial era, pitched roof, low poly, stylized game asset, matte colors",
    postwar:
      "A single-story 1950s suburban house, postwar architecture, simple peaked roof, clean stylized, low poly",
    modern:
      "A single-story contemporary house, modern architecture, peaked roof, wood and stucco, stylized game asset, low poly",
    future:
      "A single-story sci-fi eco house, futuristic architecture, solar panels, sleek design, peaked roof, low poly, stylized",
  },
  res_high: {
    frontier:
      "A two-story frontier boarding house, wooden clapboard, flat roof with lip, low poly, stylized game asset",
    industrial:
      "A dense 19th-century tenement building block, brick texture, flat roof with lip, low poly, stylized game asset",
    postwar:
      "A 1960s concrete apartment building block, postwar, flat roof with lip, low poly, stylized game asset",
    modern:
      "A modern mid-rise apartment block, balconies, flat roof with lip, concrete and glass, low poly, stylized game asset",
    future:
      "A futuristic residential tower block, sleek panels, flat roof with lip, low poly, stylized game asset",
  },
  com: {
    frontier:
      "A frontier general store or shop, wooden facade, flat roof with lip, wild west style, low poly, stylized game asset",
    industrial:
      "A 19th-century brick storefront, industrial era commercial building, flat roof with lip, low poly, stylized",
    postwar:
      "A postwar strip mall storefront, concrete and glass, flat roof with lip, low poly, stylized game asset",
    modern:
      "A modern glass storefront retail building, flat roof with lip and HVAC units, low poly, stylized game asset",
    future:
      "A futuristic commercial pod, neon signs, curved glass, flat roof with lip, low poly, stylized game asset",
  },
  ind: {
    frontier:
      "A frontier workshop or mill, wooden and stone, sawtooth roof, low poly, stylized game asset",
    industrial:
      "An industrial era factory or plant, brick walls, sawtooth roof, chimney, low poly, stylized game asset",
    postwar:
      "A postwar factory building, sawtooth roof, brick and metal, loading docks, low poly, stylized game asset",
    modern:
      "A modern logistics warehouse, metal panels, sawtooth roof, loading bays, low poly, stylized game asset",
    future:
      "A futuristic automated manufacturing plant, neon accents, sawtooth roof, metal panels, low poly, stylized",
  },
  svc: {
    frontier:
      "A frontier civic service building, wooden structure, flat roof with accent stripe, low poly, stylized game asset",
    industrial:
      "A Victorian era civic service building, brick, flat roof with accent stripe, low poly, stylized game asset",
    postwar:
      "A postwar institutional service building, concrete, flat roof with accent stripe, low poly, stylized game asset",
    modern:
      "A modern city service building, police or fire station style, flat roof with accent stripe, concrete, low poly, stylized game asset",
    future:
      "A futuristic civic service hub, clean tech lines, flat roof with accent stripe, low poly, stylized game asset",
  },
};

/**
 * @param {string[]} categories
 * @param {string[]} eras
 * @param {number[]} variantRange
 */
function makeJobs(categories, eras, variantRange) {
  /** @type {{ key: string; prompt: string; category: string; era: string }[]} */
  const jobs = [];
  for (const category of categories) {
    for (const era of eras) {
      const base =
        PROMPTS[category]?.[era] ??
        "Stylized city builder building block, low poly, isometric view";
      for (const v of variantRange) {
        const variant = String(v).padStart(2, "0");
        const key = `${category}_${era}_${variant}`;
        jobs.push({
          key,
          prompt: `${base}, silhouette variant ${variant}`,
          category,
          era,
        });
      }
    }
  }
  return jobs;
}

/**
 * @param {string} filename
 * @param {object} batch
 */
function writeBatch(filename, batch) {
  writeFileSync(join(__dirname, filename), `${JSON.stringify(batch, null, 2)}\n`);
}

const core = JSON.parse(readFileSync(join(__dirname, "manifest.json"), "utf8"));
writeBatch("manifest-batch-v1-core.json", {
  ...core,
  batch_id: "v1-core",
  linear_issue: "SB-3739",
  jobs: core.jobs,
});

const fi80 = makeJobs(
  ["res_low", "res_high", "com", "ind"],
  ["frontier", "industrial"],
  Array.from({ length: 10 }, (_, i) => i + 1),
);
writeBatch("manifest-batch-v1-frontier-industrial.json", {
  pipeline_version: "1.0",
  batch_id: "v1-frontier-industrial-80",
  meshy_model: "meshy-6",
  export_format: "glb",
  linear_issue: "SB-3740",
  jobs: fi80,
});

const fi150 = makeJobs(
  ["res_low", "res_high", "com", "ind", "svc"],
  ["frontier", "industrial"],
  Array.from({ length: 15 }, (_, i) => i + 5),
);
writeBatch("manifest-batch-v1-frontier-industrial-full.json", {
  pipeline_version: "1.0",
  batch_id: "v1-frontier-industrial-full",
  meshy_model: "meshy-6",
  export_format: "glb",
  linear_issue: "SB-3742",
  jobs: fi150,
});

for (const [era, issue] of [
  ["postwar", "SB-3743"],
  ["modern", "SB-3744"],
  ["future", "SB-3746"],
]) {
  writeBatch(`manifest-batch-${era}.json`, {
    pipeline_version: "1.0",
    batch_id: era,
    meshy_model: "meshy-6",
    export_format: "glb",
    linear_issue: issue,
    jobs: makeJobs(
      ["res_low", "res_high", "com", "ind", "svc"],
      [era],
      Array.from({ length: 20 }, (_, i) => i),
    ),
  });
}

writeBatch("manifest-batch-svc-modern.json", {
  pipeline_version: "1.0",
  batch_id: "svc-modern",
  meshy_model: "meshy-6",
  export_format: "glb",
  linear_issue: "SB-3745",
  jobs: makeJobs(["svc"], ["modern"], Array.from({ length: 20 }, (_, i) => i)),
});

console.log(
  `Wrote batches: core=${core.jobs.length}, fi80=${fi80.length}, fi150=${fi150.length}`,
);
