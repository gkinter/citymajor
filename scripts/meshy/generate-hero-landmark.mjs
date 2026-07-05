#!/usr/bin/env node
/**
 * Generate a single hero landmark GLB via Meshy MCP → web/public/assets/gltf/heroes/
 * Usage: MESHY_USE_MCP=1 node scripts/meshy/generate-hero-landmark.mjs [basenameWithoutExt]
 */
import { mkdirSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Document, NodeIO, getBounds } from "@gltf-transform/core";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(__dirname, "../..");
const HERO_ROOT = join(REPO_ROOT, "web/public/assets/gltf/heroes");
const DEFAULT_MCP_URL = "https://meshy.preview.softblaze.net/mcp";
const POLL_MS = 5_000;
const TIMEOUT_MS = 30 * 60 * 1000;

const HERO_BRIEFS = {
  hero_frontier_city_hall: {
    prompt:
      "Mid-fidelity game asset, 1850s American frontier town hall, two-story wooden clapboard with covered porch, bell tower cupola, whitewashed timber walls with brown trim, peaked shingle roof, flagpole, gas lamp by entrance, simple symmetrical facade, clean readable silhouette, no surrounding terrain, isolated building on flat ground, PBR game-ready, orthographic-friendly three-quarter view",
    target_polycount: 3500,
    ai_model: "meshy-6",
  },
};

async function mcpToolCall(toolName, toolArgs) {
  const url = process.env.MESHY_MCP_URL?.trim() || DEFAULT_MCP_URL;
  const res = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json, text/event-stream",
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: Date.now(),
      method: "tools/call",
      params: { name: toolName, arguments: toolArgs },
    }),
  });
  const raw = await res.text();
  if (!res.ok) throw new Error(`Meshy MCP ${toolName} HTTP ${res.status}: ${raw.slice(0, 200)}`);
  const dataLine = raw.split("\n").find((line) => line.startsWith("data: "));
  if (!dataLine) throw new Error(`Meshy MCP ${toolName} missing SSE data`);
  const envelope = JSON.parse(dataLine.slice("data: ".length));
  if (envelope.error) throw new Error(envelope.error.message ?? "MCP error");
  if (envelope.result?.isError) {
    throw new Error(envelope.result.content?.[0]?.text ?? "tool error");
  }
  const structured = envelope.result?.structuredContent;
  if (structured && typeof structured === "object") return structured;
  const textPayload = envelope.result?.content?.[0]?.text;
  if (textPayload) {
    try {
      return JSON.parse(textPayload);
    } catch {
      return { raw: textPayload };
    }
  }
  throw new Error(`Meshy MCP ${toolName}: empty response`);
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function pollTaskMcp(taskId) {
  const deadline = Date.now() + TIMEOUT_MS;
  while (Date.now() < deadline) {
    const task = await mcpToolCall("meshy_get_task", {
      task_id: taskId,
      task_type: "text-to-3d",
    });
    if (task.status === "SUCCEEDED") return task;
    if (task.status === "FAILED") {
      throw new Error(task.task_error?.message ?? `task ${taskId} failed`);
    }
    process.stdout.write(`  … ${task.status} ${task.progress ?? 0}%\r`);
    await sleep(POLL_MS);
  }
  throw new Error(`timeout waiting for ${taskId}`);
}

async function downloadBinary(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`download ${res.status}: ${url}`);
  return new Uint8Array(await res.arrayBuffer());
}

function pivotToGroundCenter(document) {
  const root = document.getRoot();
  const scene = root.getDefaultScene() ?? root.listScenes()[0];
  if (!scene) throw new Error("no scene");
  const { min, max } = getBounds(scene);
  const tx = -((min[0] + max[0]) / 2);
  const ty = -min[1];
  const tz = -((min[2] + max[2]) / 2);
  if (tx === 0 && ty === 0 && tz === 0) return;
  const offsetNode = document.createNode().setTranslation([tx, ty, tz]);
  for (const child of [...scene.listChildren()]) offsetNode.addChild(child);
  scene.addChild(offsetNode);
}

async function fixPivot(glbBytes) {
  const io = new NodeIO();
  const document = await io.readBinary(glbBytes);
  pivotToGroundCenter(document);
  return document;
}

async function main() {
  const key = process.argv[2] ?? "hero_frontier_city_hall";
  const brief = HERO_BRIEFS[key];
  if (!brief) {
    console.error(`Unknown hero key: ${key}`);
    process.exit(1);
  }
  if (process.env.MESHY_USE_MCP !== "1") {
    console.error("Set MESHY_USE_MCP=1");
    process.exit(1);
  }

  const outPath = join(HERO_ROOT, `${key}.glb`);
  if (existsSync(outPath) && process.argv.includes("--skip-existing")) {
    console.log(`skip ${key} (exists)`);
    return;
  }

  console.log(`▶ ${key} via Meshy MCP (${brief.ai_model})`);
  const previewCreate = await mcpToolCall("meshy_text_to_3d", {
    prompt: brief.prompt,
    mode: "preview",
    ai_model: brief.ai_model,
    target_polycount: brief.target_polycount,
    should_remesh: true,
    topology: "triangle",
  });
  const previewId = String(previewCreate.result ?? "");
  console.log(`  preview: ${previewId}`);
  await pollTaskMcp(previewId);

  const refineCreate = await mcpToolCall("meshy_refine_3d", {
    preview_task_id: previewId,
    enable_pbr: true,
  });
  const refineId = String(refineCreate.result ?? refineCreate.id ?? "");
  console.log(`  refine: ${refineId}`);
  const refineTask = await pollTaskMcp(refineId);
  const refineUrl = refineTask.model_urls?.glb;
  if (!refineUrl) throw new Error("missing glb url");

  console.log(`  download → ${outPath}`);
  const glbBytes = await downloadBinary(refineUrl);
  const document = await fixPivot(glbBytes);
  mkdirSync(HERO_ROOT, { recursive: true });
  const io = new NodeIO();
  await io.write(outPath, document);
  console.log(`  ✓ wrote ${outPath}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
