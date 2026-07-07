import { copyFileSync, existsSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";
import {
  FORMAT_VERSION_CMJR_V1,
  FORMAT_VERSION_JSON_STUB,
  SNAPSHOT_SCHEMA_VERSION,
  encodeWasmBlobBase64,
  summaryFromCmjrHeader,
  validateWasmBlobBase64,
} from "@/lib/save-format";
import {
  blobKeyForSave,
  deleteBlob,
  getBlob,
  getSignedDownloadUrl,
  isR2Configured,
  putBlob,
} from "@/lib/save-blob-store";
import { getUserIdFromRequest } from "@/lib/user-identity";
import { ensureDataDir, resolveDataDir } from "@/lib/data-dir";

/** JSON-only stub — no CMJR blob yet (SAVE_FORMAT_WEB §6.1). */
export { FORMAT_VERSION_JSON_STUB, SNAPSHOT_SCHEMA_VERSION } from "@/lib/save-format";
export const FORMAT_VERSION_CMJR = FORMAT_VERSION_CMJR_V1;

export const SaveSummarySchema = z.object({
  tick: z.number().int().nonnegative(),
  population: z.number().int().nonnegative(),
  cityFunds: z.number(),
  era: z.number().int().min(0).max(4),
});
export type SaveSummary = z.infer<typeof SaveSummarySchema>;

export const SaveSlotSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(64),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  revision: z.number().int().positive().default(1),
  formatVersion: z.number().int().nonnegative().default(FORMAT_VERSION_JSON_STUB),
  snapshotSchemaVersion: z.number().int().positive().default(SNAPSHOT_SCHEMA_VERSION),
  summary: SaveSummarySchema,
  /** v1 stub: inline SimSnapshot JSON when formatVersion === 0. */
  payload: z.record(z.string(), z.unknown()).optional(),
  blobKey: z.string().optional(),
  blobBytes: z.number().int().nonnegative().optional(),
  thumbnailKey: z.string().optional(),
  /** Base64 CMJR blob for client load when formatVersion >= 1 (GET :id only). */
  wasmBlobBase64: z.string().optional(),
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

/** List response — summary only, no inline payload (§5.1). */
export const SaveSlotListItemSchema = SaveSlotSchema.omit({ payload: true });
export type SaveSlotListItem = z.infer<typeof SaveSlotListItemSchema>;

export const CreateSaveBodySchema = z.object({
  name: z.string().min(1).max(64),
  payload: z.record(z.string(), z.unknown()).optional(),
  /** Layer B CMJR bytes from WASM ExportCmjr (base64). */
  wasmBlobBase64: z.string().min(1).optional(),
});

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotListItemSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
  storageBackend: z.enum(["local", "r2"]),
});

function savesFilePath(): string {
  return join(resolveDataDir(), "saves.json");
}

function savesTmpFilePath(): string {
  return `${savesFilePath()}.tmp`;
}

function savesBackupFilePath(): string {
  return `${savesFilePath()}.bak`;
}

const SavesFileSchema = z.record(z.string(), z.array(z.unknown()));

type Store = Record<string, SaveSlot[]>;

let memoryStore: Store = {};
let memoryStoreLoaded = false;

export function extractSummaryFromPayload(
  payload: Record<string, unknown> | undefined,
): SaveSummary {
  const tick = typeof payload?.tick === "number" ? payload.tick : 0;
  const population = typeof payload?.population === "number" ? payload.population : 0;
  const cityFunds = typeof payload?.cityFunds === "number" ? payload.cityFunds : 0;
  const era = typeof payload?.era === "number" ? payload.era : 0;
  return SaveSummarySchema.parse({ tick, population, cityFunds, era });
}

/** Upgrade legacy slots missing cloud envelope fields. */
function normalizeSlot(raw: unknown): SaveSlot {
  if (!raw || typeof raw !== "object") {
    throw new Error("Invalid save slot entry");
  }
  const record = raw as Record<string, unknown>;
  const payload =
    record.payload && typeof record.payload === "object" && !Array.isArray(record.payload)
      ? (record.payload as Record<string, unknown>)
      : undefined;

  const formatVersion =
    typeof record.formatVersion === "number" ? record.formatVersion : FORMAT_VERSION_JSON_STUB;

  return SaveSlotSchema.parse({
    id: record.id,
    name: record.name,
    createdAt: record.createdAt,
    updatedAt: record.updatedAt,
    revision: typeof record.revision === "number" ? record.revision : 1,
    formatVersion,
    snapshotSchemaVersion:
      typeof record.snapshotSchemaVersion === "number"
        ? record.snapshotSchemaVersion
        : SNAPSHOT_SCHEMA_VERSION,
    summary:
      record.summary && typeof record.summary === "object"
        ? record.summary
        : extractSummaryFromPayload(payload),
    payload: formatVersion === FORMAT_VERSION_JSON_STUB ? (payload ?? {}) : payload,
    blobKey: typeof record.blobKey === "string" ? record.blobKey : undefined,
    blobBytes: typeof record.blobBytes === "number" ? record.blobBytes : undefined,
    thumbnailKey: typeof record.thumbnailKey === "string" ? record.thumbnailKey : undefined,
  });
}

function readFromDisk(): Store {
  const savesFile = savesFilePath();
  const savesBackup = savesBackupFilePath();
  if (!existsSync(savesFile)) return {};
  let raw: unknown;
  try {
    raw = JSON.parse(readFileSync(savesFile, "utf8"));
  } catch (err) {
    console.error("[save-store] saves.json unreadable:", savesFile, err);
    if (existsSync(savesBackup)) {
      try {
        raw = JSON.parse(readFileSync(savesBackup, "utf8"));
        console.warn("[save-store] Recovered from backup:", savesBackup);
      } catch (backupErr) {
        console.error("[save-store] Backup also unreadable:", savesBackup, backupErr);
        throw new Error("saves.json is corrupt and backup unreadable", { cause: err });
      }
    } else {
      throw new Error("saves.json is corrupt or unreadable", { cause: err });
    }
  }

  const parsed = SavesFileSchema.safeParse(raw);
  if (!parsed.success) {
    console.error("[save-store] saves.json failed schema validation:", parsed.error.message);
    throw new Error("saves.json failed schema validation", { cause: parsed.error });
  }

  const store: Store = {};
  for (const [userKey, slots] of Object.entries(parsed.data)) {
    store[userKey] = slots.map((slot) => normalizeSlot(slot));
  }
  return store;
}

function loadStore(): Store {
  if (memoryStoreLoaded) return memoryStore;
  try {
    memoryStore = readFromDisk();
  } catch (err) {
    console.error("[save-store] loadStore failed, using empty store:", err);
    memoryStore = {};
  }
  memoryStoreLoaded = true;
  return memoryStore;
}

function persistStore(store: Store): void {
  memoryStore = store;
  memoryStoreLoaded = true;
  const savesFile = savesFilePath();
  const savesTmp = savesTmpFilePath();
  const savesBackup = savesBackupFilePath();
  try {
    ensureDataDir();
    if (existsSync(savesFile)) {
      copyFileSync(savesFile, savesBackup);
    }
    writeFileSync(savesTmp, JSON.stringify(store, null, 2), "utf8");
    renameSync(savesTmp, savesFile);
  } catch (err) {
    console.error("[save-store] persistStore failed:", savesFile, err);
    throw err;
  }
}

function userKey(req: Request, userId?: string): string {
  return userId ?? getUserIdFromRequest(req) ?? "anonymous";
}

function toListItem(slot: SaveSlot): SaveSlotListItem {
  const { payload: _payload, ...rest } = slot;
  return SaveSlotListItemSchema.parse(rest);
}

function resolveWasmBlobBase64(slot: SaveSlot, userId: string): string | undefined {
  const key = slot.blobKey ?? blobKeyForSave(userId, slot.id, "cmjr");
  if (slot.formatVersion < FORMAT_VERSION_CMJR_V1) return undefined;
  const blob = getBlob(key);
  if (!blob || blob.length === 0) return undefined;
  return encodeWasmBlobBase64(blob);
}

function resolvePayload(slot: SaveSlot, userId: string): Record<string, unknown> {
  if (slot.formatVersion === FORMAT_VERSION_JSON_STUB && slot.payload) {
    return slot.payload;
  }
  if (slot.blobKey) {
    const blob = getBlob(slot.blobKey);
    if (blob) {
      try {
        return JSON.parse(blob.toString("utf8")) as Record<string, unknown>;
      } catch {
        return {};
      }
    }
  }
  const fallbackKey = blobKeyForSave(userId, slot.id);
  const blob = getBlob(fallbackKey);
  if (blob) {
    try {
      return JSON.parse(blob.toString("utf8")) as Record<string, unknown>;
    } catch {
      return {};
    }
  }
  return slot.payload ?? {};
}

export function listSaves(req: Request, tier: Tier, userId?: string) {
  const store = loadStore();
  const key = userKey(req, userId);
  const saves = (store[key] ?? []).map(toListItem);
  const maxSlots = entitlementsForTier(tier).maxSaveSlots;

  return SaveListResponseSchema.parse({
    saves,
    count: saves.length,
    maxSlots,
    tier,
    storageBackend: isR2Configured() ? "r2" : "local",
  });
}

let writeLock: Promise<unknown> = Promise.resolve();

function withWriteLock<T>(fn: () => T): Promise<T> {
  const prev = writeLock;
  const next = prev.catch(() => undefined).then(() => fn());
  writeLock = next.catch(() => undefined);
  return next;
}

export function getSave(
  req: Request,
  saveId: string,
  userId?: string,
):
  | { ok: true; save: SaveSlot; downloadUrl: string | null }
  | { ok: false; status: 404; error: string } {
  const store = loadStore();
  const key = userKey(req, userId);
  const slot = (store[key] ?? []).find((s) => s.id === saveId);
  if (!slot) {
    return { ok: false, status: 404, error: "Save not found" };
  }

  const payload = resolvePayload(slot, key);
  const downloadUrl = slot.blobKey ? getSignedDownloadUrl(slot.blobKey) : null;

  return {
    ok: true,
    save: SaveSlotSchema.parse({
      ...slot,
      payload,
      wasmBlobBase64: resolveWasmBlobBase64(slot, key),
    }),
    downloadUrl,
  };
}

export function createSave(
  req: Request,
  tier: Tier,
  body: z.infer<typeof CreateSaveBodySchema>,
  userId?: string,
): Promise<
  | {
      ok: true;
      status: 201;
      save: SaveSlot;
      count: number;
      maxSlots: number;
    }
  | {
      ok: false;
      status: 403;
      error: string;
      maxSlots: number;
      count: number;
    }
  | {
      ok: false;
      status: 400;
      error: string;
    }
  | {
      ok: false;
      status: 500;
      error: string;
    }
> {
  return withWriteLock(() => {
    try {
      memoryStore = readFromDisk();
      memoryStoreLoaded = true;

      const store = memoryStore;
      const key = userKey(req, userId);
      const saves = [...(store[key] ?? [])];
      const maxSlots = entitlementsForTier(tier).maxSaveSlots;

      if (saves.length >= maxSlots) {
        return {
          ok: false as const,
          status: 403 as const,
          error: `Save slot limit reached (${maxSlots} for ${tier}). Delete a save or upgrade to Founder Pass.`,
          maxSlots,
          count: saves.length,
        };
      }

      const now = new Date().toISOString();
      const payload = body.payload ?? {};
      const id = crypto.randomUUID();

      let formatVersion = FORMAT_VERSION_JSON_STUB;
      let snapshotSchemaVersion = SNAPSHOT_SCHEMA_VERSION;
      let summary = extractSummaryFromPayload(payload);
      let blobKey = blobKeyForSave(key, id, "json");
      let blobBytes = Buffer.byteLength(JSON.stringify(payload), "utf8");
      let inlinePayload: Record<string, unknown> | undefined = payload;

      if (body.wasmBlobBase64) {
        const validated = validateWasmBlobBase64(body.wasmBlobBase64);
        if (!validated.ok) {
          return {
            ok: false as const,
            status: 400 as const,
            error: validated.error,
          };
        }

        formatVersion = FORMAT_VERSION_CMJR_V1;
        snapshotSchemaVersion = validated.header.snapshotSchemaVersion;
        summary = summaryFromCmjrHeader(validated.header);
        blobKey = blobKeyForSave(key, id, "cmjr");
        blobBytes = validated.bytes.length;

        try {
          putBlob(blobKey, validated.bytes);
        } catch (err) {
          const message =
            err instanceof Error ? err.message : "Failed to persist save blob";
          return {
            ok: false as const,
            status: 500 as const,
            error: message,
          };
        }

        // Layer C JSON kept as optional cache for list/detail UI.
        inlinePayload = Object.keys(payload).length > 0 ? payload : undefined;
      } else {
        // Sidecar JSON blob — cloud-ready separation; inline payload kept for v1 stub.
        try {
          putBlob(blobKey, JSON.stringify(payload));
        } catch {
          // Non-fatal: inline payload still works for v1.
        }
      }

      const slot: SaveSlot = SaveSlotSchema.parse({
        id,
        name: body.name,
        createdAt: now,
        updatedAt: now,
        revision: 1,
        formatVersion,
        snapshotSchemaVersion,
        summary,
        payload: inlinePayload,
        blobKey,
        blobBytes,
      });

      saves.push(slot);
      persistStore({ ...store, [key]: saves });

      return {
        ok: true as const,
        status: 201 as const,
        save: slot,
        count: saves.length,
        maxSlots,
      };
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Save store operation failed";
      return {
        ok: false as const,
        status: 500 as const,
        error: message,
      };
    }
  });
}

export function deleteSave(
  req: Request,
  saveId: string,
  userId?: string,
): Promise<
  | { ok: true; status: 204 }
  | { ok: false; status: 404; error: string }
  | { ok: false; status: 500; error: string }
> {
  return withWriteLock(() => {
    try {
      memoryStore = readFromDisk();
      memoryStoreLoaded = true;

      const store = memoryStore;
      const key = userKey(req, userId);
      const saves = store[key] ?? [];
      const index = saves.findIndex((s) => s.id === saveId);
      if (index === -1) {
        return { ok: false as const, status: 404 as const, error: "Save not found" };
      }

      const [removed] = saves.splice(index, 1);
      if (removed.blobKey) {
        try {
          deleteBlob(removed.blobKey);
        } catch {
          // Best-effort blob cleanup.
        }
      }

      persistStore({ ...store, [key]: saves });
      return { ok: true as const, status: 204 as const };
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Save store operation failed";
      return {
        ok: false as const,
        status: 500 as const,
        error: message,
      };
    }
  });
}

/** Test helper — reset store between runs. */
export function _resetSaveStoreForTests(): void {
  memoryStore = {};
  memoryStoreLoaded = false;
  writeLock = Promise.resolve();
}
