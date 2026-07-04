import {
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";

const DATA_DIR = join(process.cwd(), ".data");
const BLOBS_DIR = join(DATA_DIR, "blobs");

/** CMJR / snapshot blobs — `saves/{userId}/{saveId}.cmjr` or `.json` stub. */
export function blobKeyForSave(userId: string, saveId: string, ext = "json"): string {
  return `saves/${userId}/${saveId}.${ext}`;
}

export function isR2Configured(): boolean {
  return Boolean(
    process.env.R2_ACCOUNT_ID &&
      process.env.R2_ACCESS_KEY_ID &&
      process.env.R2_SECRET_ACCESS_KEY &&
      process.env.R2_BUCKET_NAME,
  );
}

export type BlobBackend = "local" | "r2";

export function getBlobBackend(): BlobBackend {
  return isR2Configured() ? "r2" : "local";
}

function localPathForKey(key: string): string {
  return join(BLOBS_DIR, key);
}

function ensureParentDir(filePath: string): void {
  const dir = dirname(filePath);
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
}

/** Write blob to local disk (always). R2 upload is v1.5 — skipped when creds absent. */
export function putBlob(key: string, body: string | Buffer): void {
  const localPath = localPathForKey(key);
  ensureParentDir(localPath);
  const tmp = `${localPath}.tmp`;
  writeFileSync(tmp, body);
  renameSync(tmp, localPath);

  if (isR2Configured()) {
    // v1.5: S3-compatible PUT to R2. v1 stub keeps metadata + inline payload only.
    void key;
  }
}

export function getBlob(key: string): Buffer | null {
  const localPath = localPathForKey(key);
  if (existsSync(localPath)) {
    return readFileSync(localPath);
  }

  if (isR2Configured()) {
    // v1.5: signed GET from R2
    return null;
  }

  return null;
}

export function deleteBlob(key: string): void {
  const localPath = localPathForKey(key);
  if (existsSync(localPath)) {
    rmSync(localPath, { force: true });
  }

  if (isR2Configured()) {
    // v1.5: DELETE object in R2
    void key;
  }
}

/**
 * v1.5 signed URL minting. Returns null when R2 is not configured — callers
 * fall back to inline payload or local blob reads.
 */
export function getSignedDownloadUrl(_key: string): string | null {
  if (!isR2Configured()) return null;
  return null;
}

export function getSignedUploadUrl(_key: string): string | null {
  if (!isR2Configured()) return null;
  return null;
}
