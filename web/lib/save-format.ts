/**
 * CMJR save envelope helpers — SAVE_FORMAT_WEB.md §3–§6.
 * v1 spike: WASM exports CMJR with JSON snapshot in chunk 0x01 (interim).
 */

/** ASCII "CMJR" — CityMajor save magic. */
export const CMJR_MAGIC = 0x434d4a52;

/** JSON-only stub — no CMJR blob (§6.1). */
export const FORMAT_VERSION_JSON_STUB = 0;

/** Web v1 CMJR with zstd/brotli or uncompressed payload (§6.1). */
export const FORMAT_VERSION_CMJR_V1 = 1;

/** Layer C JSON schema generation (§6.2). */
export const SNAPSHOT_SCHEMA_VERSION = 1;

/** Reject saves larger than 5 MB compressed (§10). */
export const MAX_CMJR_BLOB_BYTES = 5 * 1024 * 1024;

export const CMJR_HEADER_BYTES = 256;

export type CmjrHeaderSummary = {
  formatVersion: number;
  snapshotSchemaVersion: number;
  worldSize: number;
  gameTick: number;
  population: number;
  cityFunds: number;
  era: number;
  compression: number;
  compressedSize: number;
};

function readU32LE(buf: Uint8Array, offset: number): number {
  return (
    buf[offset] |
    (buf[offset + 1] << 8) |
    (buf[offset + 2] << 16) |
    (buf[offset + 3] << 24)
  ) >>> 0;
}

function readU16LE(buf: Uint8Array, offset: number): number {
  return buf[offset] | (buf[offset + 1] << 8);
}

function readI32LE(buf: Uint8Array, offset: number): number {
  const v = readU32LE(buf, offset);
  return v > 0x7fffffff ? v - 0x1_0000_0000 : v;
}

/** Parse the 256-byte CMJR header; returns null when magic or size is invalid. */
export function parseCmjrHeader(bytes: Uint8Array): CmjrHeaderSummary | null {
  if (bytes.length < CMJR_HEADER_BYTES) return null;

  const magic = readU32LE(bytes, 0);
  if (magic !== CMJR_MAGIC) return null;

  const compressedSize = readU32LE(bytes, 16);
  if (compressedSize > MAX_CMJR_BLOB_BYTES) return null;
  if (bytes.length < CMJR_HEADER_BYTES + compressedSize) return null;

  return {
    formatVersion: readU32LE(bytes, 4),
    snapshotSchemaVersion: readU16LE(bytes, 21),
    worldSize: readU16LE(bytes, 23),
    compression: bytes[20] ?? 0,
    compressedSize,
    gameTick: readU32LE(bytes, 35),
    population: readU32LE(bytes, 39),
    cityFunds: readI32LE(bytes, 43),
    era: bytes[47] ?? 0,
  };
}

export type ValidateWasmBlobResult =
  | { ok: true; bytes: Buffer; header: CmjrHeaderSummary }
  | { ok: false; error: string };

/** Decode base64 WASM blob and validate CMJR header + size caps. */
export function validateWasmBlobBase64(base64: string): ValidateWasmBlobResult {
  if (!base64 || typeof base64 !== "string") {
    return { ok: false, error: "Missing wasmBlobBase64" };
  }

  let bytes: Buffer;
  try {
    bytes = Buffer.from(base64, "base64");
  } catch {
    return { ok: false, error: "Invalid base64 encoding" };
  }

  if (bytes.length > MAX_CMJR_BLOB_BYTES + CMJR_HEADER_BYTES) {
    return { ok: false, error: `Save blob exceeds ${MAX_CMJR_BLOB_BYTES} byte limit` };
  }

  const header = parseCmjrHeader(bytes);
  if (!header) {
    return { ok: false, error: "Invalid CMJR header or magic" };
  }

  if (header.formatVersion > FORMAT_VERSION_CMJR_V1) {
    return {
      ok: false,
      error: `Unsupported save format version ${header.formatVersion}`,
    };
  }

  if (header.snapshotSchemaVersion > SNAPSHOT_SCHEMA_VERSION) {
    return {
      ok: false,
      error: `Unsupported snapshot schema version ${header.snapshotSchemaVersion}`,
    };
  }

  return { ok: true, bytes, header };
}

/** Encode binary CMJR for API transport. */
export function encodeWasmBlobBase64(bytes: Buffer | Uint8Array): string {
  return Buffer.from(bytes).toString("base64");
}

/** Extract denormalized summary from CMJR header for list UI. */
export function summaryFromCmjrHeader(header: CmjrHeaderSummary) {
  return {
    tick: header.gameTick,
    population: header.population,
    cityFunds: header.cityFunds,
    era: header.era,
  };
}
