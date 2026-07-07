#!/usr/bin/env node
/**
 * Smoke preflight: meshy manifest only on a clean tree (local dirty skips),
 * always runs verify:tech-unlocks.
 */
import { spawnSync } from "node:child_process";

function run(cmd, args) {
  const result = spawnSync(cmd, args, { stdio: "inherit" });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function isDirtyWorkingTree() {
  const result = spawnSync("git", ["status", "--porcelain"], { encoding: "utf8" });
  return Boolean(result.stdout?.trim());
}

if (isDirtyWorkingTree()) {
  console.log("[smoke:precheck] skip meshy:validate (dirty working tree)");
} else {
  run("pnpm", ["meshy:validate"]);
}

run("pnpm", ["verify:tech-unlocks"]);
