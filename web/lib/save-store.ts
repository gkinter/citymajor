import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";

export const SaveSlotSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(64),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  /** Opaque sim snapshot blob — stub only for v1. */
  payload: z.record(z.string(), z.unknown()).default({}),
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

export const CreateSaveBodySchema = z.object({
  name: z.string().min(1).max(64),
  payload: z.record(z.string(), z.unknown()).optional(),
});

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
});

const DATA_DIR = join(process.cwd(), ".data");
const SAVES_FILE = join(DATA_DIR, "saves.json");

const SavesFileSchema = z.record(z.string(), z.array(SaveSlotSchema));

type Store = z.infer<typeof SavesFileSchema>;

/** In-memory cache mirrored to JSON file when writable. */
let memoryStore: Store = {};

function loadStore(): Store {
  if (Object.keys(memoryStore).length > 0) return memoryStore;

  try {
    if (existsSync(SAVES_FILE)) {
      const raw = JSON.parse(readFileSync(SAVES_FILE, "utf8")) as unknown;
      memoryStore = SavesFileSchema.parse(raw);
      return memoryStore;
    }
  } catch {
    memoryStore = {};
  }
  return memoryStore;
}

function persistStore(store: Store): void {
  memoryStore = store;
  try {
    if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });
    writeFileSync(SAVES_FILE, JSON.stringify(store, null, 2), "utf8");
  } catch {
    // Dev stub — in-memory only if filesystem is read-only.
  }
}

function userKey(req: Request): string {
  return req.headers.get("x-citymajor-user") ?? "default-user";
}

export function listSaves(req: Request, tier: Tier) {
  const store = loadStore();
  const key = userKey(req);
  const saves = store[key] ?? [];
  const maxSlots = entitlementsForTier(tier).maxSaveSlots;

  return SaveListResponseSchema.parse({
    saves,
    count: saves.length,
    maxSlots,
    tier,
  });
}

export function createSave(req: Request, tier: Tier, body: z.infer<typeof CreateSaveBodySchema>) {
  const store = loadStore();
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
  const slot: SaveSlot = {
    id: crypto.randomUUID(),
    name: body.name,
    createdAt: now,
    updatedAt: now,
    payload: body.payload ?? {},
  };

  saves.push(SaveSlotSchema.parse(slot));
  persistStore({ ...store, [key]: saves });

  return {
    ok: true as const,
    status: 201 as const,
    save: slot,
    count: saves.length,
    maxSlots,
  };
}

/** Test helper — reset store between runs. */
export function _resetSaveStoreForTests(): void {
  memoryStore = {};
}
