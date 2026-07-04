import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import { TierSchema, type Tier } from "@/lib/entitlements";

const TierFileSchema = z.record(z.string(), TierSchema);
type Store = z.infer<typeof TierFileSchema>;

const DATA_DIR = join(process.cwd(), ".data");
const TIERS_FILE = join(DATA_DIR, "tiers.json");

let memoryStore: Store = {};

function loadStore(): Store {
  if (Object.keys(memoryStore).length > 0) return memoryStore;
  try {
    if (existsSync(TIERS_FILE)) {
      const raw = JSON.parse(readFileSync(TIERS_FILE, "utf8")) as unknown;
      memoryStore = TierFileSchema.parse(raw);
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
    writeFileSync(TIERS_FILE, JSON.stringify(store, null, 2), "utf8");
  } catch {
    // Dev stub — in-memory only if filesystem is read-only.
  }
}

export function getStoredTier(userKey: string): Tier | null {
  const store = loadStore();
  return store[userKey] ?? null;
}

export function setStoredTier(userKey: string, tier: Tier): void {
  const store = loadStore();
  persistStore({ ...store, [userKey]: tier });
}

export function _resetTierStoreForTests(): void {
  memoryStore = {};
}
