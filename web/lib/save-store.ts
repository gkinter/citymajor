import { copyFileSync, existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";
import {
  blobKeyForSave,
  deleteBlob,
  getBlob,
  getSignedDownloadUrl,
  isR2Configured,
  putBlob,
} from "@/lib/save-blob-store";
import { getUserIdFromRequest } from "@/lib/user-identity";

/** JSON-only stub — no CMJR blob yet (SAVE_FORMAT_WEB §6.1). */
export const FORMAT_VERSION_JSON_STUB = 0;
export const SNAPSHOT_SCHEMA_VERSION = 1;

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
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

/** List response — summary only, no inline payload (§5.1). */
export const SaveSlotListItemSchema = SaveSlotSchema.omit({ payload: true });
export type SaveSlotListItem = z.infer<typeof SaveSlotListItemSchema>;

export const CreateSaveBodySchema = z.object({
  name: z.string().min(1).max(64),
  payload: z.record(z.string(), z.unknown()).optional(),
});

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotListItemSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
  storageBackend: z.enum(["local", "r2"]),
});

const DATA_DIR = join(process.cwd(), ".data");
const SAVES_FILE = join(DATA_DIR, "saves.json");
const SAVES_TMP_FILE = `${SAVES_FILE}.tmp`;
const SAVES_BACKUP_FILE = `${SAVES_FILE}.bak`;

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
  if (!existsSync(SAVES_FILE)) return {};
  let raw: unknown;
  try {
    raw = JSON.parse(readFileSync(SAVES_FILE, "utf8"));
  } catch (err) {
    if (existsSync(SAVES_BACKUP_FILE)) {
      try {
        raw = JSON.parse(readFileSync(SAVES_BACKUP_FILE, "utf8"));
      } catch {
        throw new Error("saves.json is corrupt and backup unreadable", { cause: err });
      }
    } else {
      throw new Error("saves.json is corrupt or unreadable", { cause: err });
    }
  }

  const parsed = SavesFileSchema.safeParse(raw);
  if (!parsed.success) {
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
  memoryStore = readFromDisk();
  memoryStoreLoaded = true;
  return memoryStore;
}

function persistStore(store: Store): void {
  memoryStore = store;
  memoryStoreLoaded = true;
  if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });
  if (existsSync(SAVES_FILE)) {
    copyFileSync(SAVES_FILE, SAVES_BACKUP_FILE);
  }
  writeFileSync(SAVES_TMP_FILE, JSON.stringify(store, null, 2), "utf8");
  renameSync(SAVES_TMP_FILE, SAVES_FILE);
}

function userKey(req: Request): string {
  return getUserIdFromRequest(req) ?? "anonymous";
}

function toListItem(slot: SaveSlot): SaveSlotListItem {
  const { payload: _payload, ...rest } = slot;
  return SaveSlotListItemSchema.parse(rest);
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

export function listSaves(req: Request, tier: Tier) {
  const store = loadStore();
  const key = userKey(req);
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
):
  | { ok: true; save: SaveSlot; downloadUrl: string | null }
  | { ok: false; status: 404; error: string } {
  const store = loadStore();
  const key = userKey(req);
  const slot = (store[key] ?? []).find((s) => s.id === saveId);
  if (!slot) {
    return { ok: false, status: 404, error: "Save not found" };
  }

  const payload = resolvePayload(slot, key);
  const downloadUrl = slot.blobKey ? getSignedDownloadUrl(slot.blobKey) : null;

  return {
    ok: true,
    save: SaveSlotSchema.parse({ ...slot, payload }),
    downloadUrl,
  };
}

export function createSave(
  req: Request,
  tier: Tier,
  body: z.infer<typeof CreateSaveBodySchema>,
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
      status: 500;
      error: string;
    }
> {
  return withWriteLock(() => {
    try {
      memoryStore = readFromDisk();
      memoryStoreLoaded = true;

      const store = memoryStore;
      const key = userKey(req);
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
      const blobKey = blobKeyForSave(key, id);

      // Sidecar blob — cloud-ready separation; inline payload kept for v1 stub.
      try {
        putBlob(blobKey, JSON.stringify(payload));
      } catch {
        // Non-fatal: inline payload still works for v1.
      }

      const slot: SaveSlot = SaveSlotSchema.parse({
        id,
        name: body.name,
        createdAt: now,
        updatedAt: now,
        revision: 1,
        formatVersion: FORMAT_VERSION_JSON_STUB,
        snapshotSchemaVersion: SNAPSHOT_SCHEMA_VERSION,
        summary: extractSummaryFromPayload(payload),
        payload,
        blobKey,
        blobBytes: Buffer.byteLength(JSON.stringify(payload), "utf8"),
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
      const key = userKey(req);
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
