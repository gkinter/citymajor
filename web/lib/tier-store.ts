import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import { TierSchema, type Tier } from "@/lib/entitlements";
import { ensureDataDir, resolveDataDir } from "@/lib/data-dir";

const TierFileSchema = z.record(z.string(), TierSchema);
type Store = z.infer<typeof TierFileSchema>;

function tiersFilePath(): string {
  return join(resolveDataDir(), "tiers.json");
}

let memoryStore: Store = {};

function loadStore(): Store {
  if (Object.keys(memoryStore).length > 0) return memoryStore;
  const tiersFile = tiersFilePath();
  try {
    if (existsSync(tiersFile)) {
      const raw = JSON.parse(readFileSync(tiersFile, "utf8")) as unknown;
      memoryStore = TierFileSchema.parse(raw);
      return memoryStore;
    }
  } catch (err) {
    console.error("[tier-store] loadStore failed, using empty store:", tiersFile, err);
    memoryStore = {};
  }
  return memoryStore;
}

function persistStore(store: Store): void {
  memoryStore = store;
  const tiersFile = tiersFilePath();
  try {
    ensureDataDir();
    writeFileSync(tiersFile, JSON.stringify(store, null, 2), "utf8");
  } catch (err) {
    console.error("[tier-store] persistStore failed (in-memory only):", tiersFile, err);
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
