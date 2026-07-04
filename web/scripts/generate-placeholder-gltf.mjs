#!/usr/bin/env node
/**
 * Procedural placeholder GLB building modules for CityMajor R3F spike.
 * Replace outputs via Meshy pipeline — see docs/MESHY_ASSET_PIPELINE.md
 */
import { readFileSync } from "node:fs";
import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Document, NodeIO } from "@gltf-transform/core";

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_ROOT = join(__dirname, "../public/assets/gltf");
const MANIFEST_PATH = join(__dirname, "../../scripts/meshy/manifest.json");

/** @typedef {[number, number, number]} Vec3 */
/** @typedef {[number, number, number, number]} Rgba */

/**
 * @param {import('@gltf-transform/core').Document} document
 * @param {import('@gltf-transform/core').Scene} scene
 * @param {Vec3} size
 * @param {Vec3} center
 * @param {Rgba} color
 */
function addBox(document, scene, size, center, color) {
  const [w, h, d] = size;
  const [cx, cy, cz] = center;
  const hx = w / 2;
  const hy = h / 2;
  const hz = d / 2;

  const positions = new Float32Array([
    cx - hx, cy - hy, cz + hz,
    cx + hx, cy - hy, cz + hz,
    cx + hx, cy + hy, cz + hz,
    cx - hx, cy + hy, cz + hz,
    cx - hx, cy - hy, cz - hz,
    cx + hx, cy - hy, cz - hz,
    cx + hx, cy + hy, cz - hz,
    cx - hx, cy + hy, cz - hz,
  ]);

  const indices = new Uint16Array([
    0, 1, 2, 0, 2, 3,
    1, 5, 6, 1, 6, 2,
    5, 4, 7, 5, 7, 6,
    4, 0, 3, 4, 3, 7,
    3, 2, 6, 3, 6, 7,
    4, 5, 1, 4, 1, 0,
  ]);

  const buffer = document.getRoot().listBuffers()[0] ?? document.createBuffer();
  const position = document
    .createAccessor()
    .setType("VEC3")
    .setArray(positions)
    .setBuffer(buffer);
  const index = document
    .createAccessor()
    .setType("SCALAR")
    .setArray(indices)
    .setBuffer(buffer);

  const material = document
    .createMaterial()
    .setBaseColorFactor(color)
    .setMetallicFactor(0.1)
    .setRoughnessFactor(0.65);

  const primitive = document
    .createPrimitive()
    .setAttribute("POSITION", position)
    .setIndices(index)
    .setMaterial(material);

  const mesh = document.createMesh().addPrimitive(primitive);
  scene.addChild(document.createNode().setMesh(mesh));
}

/**
 * @param {import('@gltf-transform/core').Document} document
 * @param {import('@gltf-transform/core').Scene} scene
 * @param {number} radius
 * @param {number} height
 * @param {Vec3} center
 * @param {Rgba} color
 */
function addPyramid(document, scene, radius, height, center, color) {
  const [cx, cy, cz] = center;
  const y0 = cy - height / 2;
  const y1 = cy + height / 2;

  const positions = new Float32Array([
    cx - radius, y0, cz + radius,
    cx + radius, y0, cz + radius,
    cx + radius, y0, cz - radius,
    cx - radius, y0, cz - radius,
    cx, y1, cz,
  ]);

  const indices = new Uint16Array([
    0, 1, 4,
    1, 2, 4,
    2, 3, 4,
    3, 0, 4,
    0, 2, 1,
    0, 3, 2,
  ]);

  const buffer = document.getRoot().listBuffers()[0] ?? document.createBuffer();
  const position = document
    .createAccessor()
    .setType("VEC3")
    .setArray(positions)
    .setBuffer(buffer);
  const index = document
    .createAccessor()
    .setType("SCALAR")
    .setArray(indices)
    .setBuffer(buffer);

  const material = document
    .createMaterial()
    .setBaseColorFactor(color)
    .setMetallicFactor(0.05)
    .setRoughnessFactor(0.7);

  const primitive = document
    .createPrimitive()
    .setAttribute("POSITION", position)
    .setIndices(index)
    .setMaterial(material);

  const mesh = document.createMesh().addPrimitive(primitive);
  scene.addChild(document.createNode().setMesh(mesh));
}

/** @param {() => void} build */
function makeDocument(build) {
  const document = new Document();
  const scene = document.createScene();
  build(document, scene);
  return document;
}

function hexToRgba(hex) {
  const n = parseInt(hex.replace("#", ""), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255, 1];
}

/** @param {string} category */
function buildForCategory(category) {
  switch (category) {
    case "res_low":
      return (doc, scene) => {
        addBox(doc, scene, [0.72, 0.42, 0.72], [0, 0.21, 0], hexToRgba("#8D6E63"));
        addPyramid(doc, scene, 0.52, 0.28, [0, 0.56, 0], hexToRgba("#6D4C41"));
      };
    case "res_high":
      return (doc, scene) => {
        addBox(doc, scene, [0.68, 1.05, 0.68], [0, 0.525, 0], hexToRgba("#C62828"));
        addBox(doc, scene, [0.72, 0.05, 0.72], [0, 1.075, 0], hexToRgba("#B71C1C"));
      };
    case "com":
      return (doc, scene) => {
        addBox(doc, scene, [0.78, 0.55, 0.78], [0, 0.275, 0], hexToRgba("#78909C"));
        addBox(doc, scene, [0.82, 0.06, 0.82], [0, 0.58, 0], hexToRgba("#607D8B"));
        addBox(doc, scene, [0.5, 0.04, 0.12], [0, 0.52, 0.38], hexToRgba("#546E7A"));
      };
    case "ind":
      return (doc, scene) => {
        addBox(doc, scene, [0.85, 0.38, 0.85], [0, 0.19, 0], hexToRgba("#37474F"));
        addBox(doc, scene, [0.22, 0.18, 0.85], [-0.22, 0.47, 0], hexToRgba("#455A64"));
        addBox(doc, scene, [0.22, 0.18, 0.85], [0, 0.55, 0], hexToRgba("#455A64"));
        addBox(doc, scene, [0.22, 0.18, 0.85], [0.22, 0.47, 0], hexToRgba("#455A64"));
        addBox(doc, scene, [0.1, 0.22, 0.1], [0.28, 0.49, 0.28], hexToRgba("#263238"));
      };
    case "svc":
      return (doc, scene) => {
        addBox(doc, scene, [0.75, 0.5, 0.75], [0, 0.25, 0], hexToRgba("#D9D9CC"));
        addBox(doc, scene, [0.76, 0.12, 0.76], [0, 0.44, 0], hexToRgba("#5980CC"));
      };
    default:
      throw new Error(`Unknown category: ${category}`);
  }
}

/** @type {{ key: string; era: string; category: string }[]} */
const MANIFEST_JOBS = JSON.parse(readFileSync(MANIFEST_PATH, "utf8")).jobs;

async function main() {
  const io = new NodeIO();

  for (const job of MANIFEST_JOBS) {
    const build = buildForCategory(job.category);
    const document = makeDocument((doc, scene) => build(doc, scene));
    const dir = join(OUT_ROOT, job.era);
    mkdirSync(dir, { recursive: true });
    const outPath = join(dir, `${job.key}.glb`);
    await io.write(outPath, document);
    console.log(`wrote ${outPath}`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
