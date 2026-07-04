import { existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const DEFAULT_PROD_DATA_DIR = "/tmp/citymajor-data";

/**
 * Writable directory for JSON stores and local blob sidecars.
 *
 * - `CITYMAJOR_DATA_DIR` when set (Coolify volume mount or explicit path)
 * - `/tmp/citymajor-data` in production (container cwd is often read-only)
 * - `.data` under cwd in development
 */
export function resolveDataDir(): string {
  const fromEnv = process.env.CITYMAJOR_DATA_DIR?.trim();
  if (fromEnv) return fromEnv;
  if (process.env.NODE_ENV === "production") {
    return DEFAULT_PROD_DATA_DIR;
  }
  return join(process.cwd(), ".data");
}

/** Create the data directory if missing; logs and rethrows on failure. */
export function ensureDataDir(): string {
  const dir = resolveDataDir();
  try {
    if (!existsSync(dir)) {
      mkdirSync(dir, { recursive: true });
    }
  } catch (err) {
    console.error("[data-dir] Failed to create data directory:", dir, err);
    throw err;
  }
  return dir;
}
