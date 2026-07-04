/**
 * Sim bridge — Web Worker loads published Forge.SimWasm dotnet bundle (SB-3666).
 * Falls back to procedural city-data when WASM is unavailable (see city-data.ts).
 */

export type SimCommand =
  | { type: "tick"; deltaMs: number }
  | { type: "place_building"; tileX: number; tileZ: number; typeId: number }
  | { type: "zone_paint"; tileX: number; tileZ: number; zoneType: number }
  | { type: "bulldoze"; tileX: number; tileZ: number }
  | { type: "pause" }
  | { type: "resume" };

export type ZoneSnapshot = {
  tileX: number;
  tileZ: number;
  zoneType: number;
};

export type BuildingSnapshot = {
  id: number;
  typeId: number;
  tileX: number;
  tileZ: number;
  level: number;
  state: number;
  condition: number;
};

export type SimResources = {
  tick: number;
  population: number;
  cityFunds: number;
  era: number;
};

export type SimSnapshot = SimResources & {
  buildings: BuildingSnapshot[];
  /** Sparse zoned tiles (non-zero zoneType). */
  zones?: ZoneSnapshot[];
};

export interface SimBridge {
  init(wasmUrl?: string, worldSize?: number): Promise<void>;
  send(command: SimCommand): void;
  getSnapshot(): SimSnapshot | null;
  onSnapshot(callback: (snapshot: SimSnapshot) => void): () => void;
  dispose(): void;
}

const DEFAULT_WASM_URL = "/dotnet";
const DEFAULT_WORLD_SIZE = 256;

type WorkerInbound =
  | { type: "init"; wasmBaseUrl: string; worldSize: number }
  | { type: "command"; command: SimCommand }
  | { type: "dispose" };

type WorkerOutbound =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: SimSnapshot }
  | { type: "error"; message: string };

/** Worker-backed bridge loading the published dotnet WASM bundle at runtime. */
export function createSimBridge(): SimBridge {
  let worker: Worker | null = null;
  let latestSnapshot: SimSnapshot | null = null;
  const listeners = new Set<(snapshot: SimSnapshot) => void>();

  const notify = (snapshot: SimSnapshot) => {
    latestSnapshot = snapshot;
    for (const listener of listeners) listener(snapshot);
  };

  return {
    async init(wasmUrl = DEFAULT_WASM_URL, worldSize = DEFAULT_WORLD_SIZE) {
      worker = new Worker(new URL("../workers/sim-worker.ts", import.meta.url));

      await new Promise<void>((resolve, reject) => {
        const onMessage = (event: MessageEvent<WorkerOutbound>) => {
          const msg = event.data;
          if (msg.type === "ready") {
            worker?.removeEventListener("message", onMessage);
            resolve();
          } else if (msg.type === "snapshot") {
            notify(msg.snapshot);
          } else if (msg.type === "error") {
            worker?.removeEventListener("message", onMessage);
            reject(new Error(msg.message));
          }
        };

        worker!.addEventListener("message", onMessage);
        worker!.postMessage({
          type: "init",
          wasmBaseUrl: wasmUrl,
          worldSize,
        } satisfies WorkerInbound);
      });

      worker.addEventListener("message", (event: MessageEvent<WorkerOutbound>) => {
        if (event.data.type === "snapshot") notify(event.data.snapshot);
      });
    },

    send(command) {
      worker?.postMessage({ type: "command", command } satisfies WorkerInbound);
    },

    getSnapshot() {
      return latestSnapshot;
    },

    onSnapshot(callback) {
      listeners.add(callback);
      if (latestSnapshot) callback(latestSnapshot);
      return () => listeners.delete(callback);
    },

    dispose() {
      worker?.postMessage({ type: "dispose" } satisfies WorkerInbound);
      worker?.terminate();
      worker = null;
      listeners.clear();
      latestSnapshot = null;
    },
  };
}

/** No-op bridge for tests or explicit stub mode. */
export function createSimBridgeStub(): SimBridge {
  const listeners = new Set<(snapshot: SimSnapshot) => void>();

  return {
    async init() {},
    send() {},
    getSnapshot() {
      return null;
    },
    onSnapshot(callback) {
      listeners.add(callback);
      return () => listeners.delete(callback);
    },
    dispose() {
      listeners.clear();
    },
  };
}
