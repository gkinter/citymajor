/**
 * Sim bridge — Web Worker loads published Forge.SimWasm dotnet bundle (SB-3666).
 * Falls back to procedural city-data when WASM is unavailable (see city-data.ts).
 */

/** 0 = paused, 1 = 1x, 2 = 2x, 3 = 3x */
export type GameSpeedLevel = 0 | 1 | 2 | 3;

export type SimCommand =
  | { type: "tick"; deltaMs: number }
  | { type: "place_building"; tileX: number; tileZ: number; typeId: number }
  | { type: "place_road"; tileX: number; tileZ: number }
  | { type: "zone_paint"; tileX: number; tileZ: number; zoneType: number }
  | { type: "bulldoze"; tileX: number; tileZ: number }
  | { type: "enqueue_research"; techId: number }
  | { type: "set_speed"; level: GameSpeedLevel }
  | { type: "pause" }
  | { type: "resume" }
  | { type: "load_snapshot"; snapshot: SimSnapshot }
  | {
      type: "herald_choice";
      optionId: string;
      eventTypeId?: string;
      eventId?: number;
    };

export type ZoneSnapshot = {
  tileX: number;
  tileZ: number;
  zoneType: number;
};

export type RoadSnapshot = {
  tileX: number;
  tileZ: number;
  roadFlags: number;
};

/** Sparse road tiles with congestion density (0–1) from WasmTrafficLite. */
export type TrafficSnapshot = {
  tileX: number;
  tileZ: number;
  density: number;
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

/** RCI demand from EconomySystem — normalized -1 (surplus) to +1 (shortage). */
export type RciDemand = {
  residential: number;
  commercial: number;
  industrial: number;
};

export type ActiveEventSnapshot = {
  eventId: number;
  typeId: string;
  phase: string;
  severity: number;
  tileX: number;
  tileY: number;
};

export type EraProgressGate = {
  id: string;
  label: string;
  current: number;
  required: number;
  met: boolean;
};

export type EraProgress = {
  nextEra: number;
  nextEraName: string;
  percent: number;
  gates: EraProgressGate[];
};

export type SimResources = {
  tick: number;
  population: number;
  /** Present when WASM status JSON includes householdCount. */
  householdCount?: number;
  cityFunds: number;
  era: number;
  /** WASM GetStatus — progress toward next era (WasmConfig thresholds). */
  eraProgress?: EraProgress;
  /** WASM GetStatus — EconomySystem demand signals (-1..+1). */
  residentialDemand?: number;
  commercialDemand?: number;
  industrialDemand?: number;
  /** WASM GetStatus — ResearchSystem accumulated RP. */
  researchPoints?: number;
  /** WASM GetStatus — RP generated per game month. */
  researchRate?: number;
  /** WASM GetStatus — count of unlocked technologies. */
  techCount?: number;
  /** WASM GetStatus — mayor approval percent (0–100) when PoliticsSystem is exported. */
  approval?: number;
  /** City happiness (0–1). */
  happiness?: number;
  /** Current-month income / expense totals from BudgetSystem ledgers. */
  monthlyIncome?: number;
  monthlyExpenses?: number;
  /** Live EventSystem instances from WASM GetStatus / snapshot. */
  activeEvents?: ActiveEventSnapshot[];
};

/** Strip render payload from a full sim snapshot for HUD consumers. */
export function resourcesFromSnapshot(snapshot: SimSnapshot): SimResources {
  const {
    buildings: _buildings,
    zones: _zones,
    roads: _roads,
    traffic: _traffic,
    ...resources
  } = snapshot;
  return resources;
}

export type SimSnapshot = SimResources & {
  buildings: BuildingSnapshot[];
  /** Sparse zoned tiles (non-zero zoneType). */
  zones?: ZoneSnapshot[];
  /** Sparse road tiles (non-zero roadFlags). */
  roads?: RoadSnapshot[];
  /** Sparse road tiles with congestion density (≥0.01). */
  traffic?: TrafficSnapshot[];
};

export interface SimBridge {
  init(wasmUrl?: string, worldSize?: number): Promise<void>;
  send(command: SimCommand): void;
  /** Restore WASM state from a save payload; resolves when the worker acks. */
  loadSnapshot(snapshot: SimSnapshot): Promise<void>;
  getSnapshot(): SimSnapshot | null;
  onSnapshot(callback: (snapshot: SimSnapshot) => void): () => void;
  /**
   * Subscribe to worker failures — includes:
   *   • `{type:"error"}` messages posted by the worker after init
   *   • uncaught worker exceptions (`Worker#error`)
   *   • message deserialization errors (`Worker#messageerror`)
   *
   * Returns an unsubscribe function.
   */
  onError(callback: (error: Error) => void): () => void;
  dispose(): void;
}

/** Client-side snapshot access for save/load UI. */
export type SimClientApi = {
  getSnapshot: () => SimSnapshot | null;
  /** Optimistic UI apply + WASM restore; rejects on worker load failure. */
  applySnapshot: (snapshot: SimSnapshot) => Promise<void>;
  sendCommand: (command: SimCommand) => void;
};

const DEFAULT_WASM_URL = "/dotnet";
const DEFAULT_WORLD_SIZE = 256;
/**
 * Cap init() so a worker that never posts `ready` (bad WASM URL, missing
 * blazor.boot.json, module-load exception before the postMessage handler is
 * installed) rejects instead of leaving the caller hanging forever.
 */
const INIT_TIMEOUT_MS = 15_000;

type WorkerInbound =
  | { type: "init"; wasmBaseUrl: string; worldSize: number }
  | { type: "command"; command: SimCommand }
  | { type: "dispose" };

type WorkerOutbound =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: SimSnapshot }
  | { type: "load_complete"; ok: boolean }
  | { type: "error"; message: string };

const LOAD_SNAPSHOT_TIMEOUT_MS = 15_000;

/** Worker-backed bridge loading the published dotnet WASM bundle at runtime. */
export function createSimBridge(): SimBridge {
  let worker: Worker | null = null;
  let latestSnapshot: SimSnapshot | null = null;
  const listeners = new Set<(snapshot: SimSnapshot) => void>();
  const errorListeners = new Set<(error: Error) => void>();
  let pendingLoad:
    | { resolve: () => void; reject: (error: Error) => void }
    | null = null;

  const notifyError = (error: Error) => {
    for (const listener of errorListeners) listener(error);
  };

  const settlePendingLoad = (ok: boolean, error?: Error) => {
    const pending = pendingLoad;
    if (!pending) return;
    pendingLoad = null;
    if (ok) pending.resolve();
    else pending.reject(error ?? new Error("Failed to restore WASM snapshot"));
  };

  const handleWorkerOutbound = (msg: WorkerOutbound) => {
    if (msg.type === "snapshot") {
      notify(msg.snapshot);
      return;
    }
    if (msg.type === "load_complete") {
      settlePendingLoad(msg.ok);
      return;
    }
    if (msg.type === "error") {
      const err = new Error(msg.message);
      notifyError(err);
      settlePendingLoad(false, err);
    }
  };

  const notify = (snapshot: SimSnapshot) => {
    latestSnapshot = snapshot;
    for (const listener of listeners) listener(snapshot);
  };

  return {
    async init(wasmUrl = DEFAULT_WASM_URL, worldSize = DEFAULT_WORLD_SIZE) {
      worker = new Worker(new URL("../workers/sim-worker.ts", import.meta.url));
      const localWorker = worker;

      await new Promise<void>((resolve, reject) => {
        let settled = false;
        const settle = (fn: () => void) => {
          if (settled) return;
          settled = true;
          clearTimeout(timeoutId);
          localWorker.removeEventListener("message", onMessage);
          localWorker.removeEventListener("error", onWorkerError);
          localWorker.removeEventListener("messageerror", onWorkerMessageError);
          fn();
        };

        const onMessage = (event: MessageEvent<WorkerOutbound>) => {
          const msg = event.data;
          if (msg.type === "ready") {
            settle(resolve);
          } else if (msg.type === "snapshot" || msg.type === "load_complete") {
            handleWorkerOutbound(msg);
          } else if (msg.type === "error") {
            const err = new Error(msg.message);
            handleWorkerOutbound(msg);
            settle(() => reject(err));
          }
        };

        const onWorkerError = (event: ErrorEvent) => {
          const err = new Error(event.message || "Worker error before ready");
          notifyError(err);
          settle(() => reject(err));
        };

        const onWorkerMessageError = () => {
          const err = new Error(
            "Worker message deserialization error before ready",
          );
          notifyError(err);
          settle(() => reject(err));
        };

        const timeoutId = setTimeout(() => {
          const err = new Error(
            `Sim worker init timed out after ${INIT_TIMEOUT_MS}ms`,
          );
          notifyError(err);
          settle(() => reject(err));
        }, INIT_TIMEOUT_MS);

        localWorker.addEventListener("message", onMessage);
        localWorker.addEventListener("error", onWorkerError);
        localWorker.addEventListener("messageerror", onWorkerMessageError);
        localWorker.postMessage({
          type: "init",
          wasmBaseUrl: wasmUrl,
          worldSize,
        } satisfies WorkerInbound);
      });

      // Post-ready runtime listeners — errors after init flow through
      // onError callbacks instead of rejecting the (already-resolved) init.
      localWorker.addEventListener(
        "message",
        (event: MessageEvent<WorkerOutbound>) => {
          handleWorkerOutbound(event.data);
        },
      );
      localWorker.addEventListener("error", (event) => {
        notifyError(new Error(event.message || "Worker error"));
      });
      localWorker.addEventListener("messageerror", () => {
        notifyError(new Error("Worker message deserialization error"));
      });
    },

    send(command) {
      worker?.postMessage({ type: "command", command } satisfies WorkerInbound);
    },

    loadSnapshot(snapshot) {
      const localWorker = worker;
      if (!localWorker) {
        return Promise.reject(new Error("Sim worker not initialized"));
      }
      if (pendingLoad) {
        return Promise.reject(new Error("Another load is already in progress"));
      }
      return new Promise<void>((resolve, reject) => {
        const timeoutId = setTimeout(() => {
          if (!pendingLoad) return;
          pendingLoad = null;
          reject(
            new Error(
              `Load snapshot timed out after ${LOAD_SNAPSHOT_TIMEOUT_MS}ms`,
            ),
          );
        }, LOAD_SNAPSHOT_TIMEOUT_MS);

        pendingLoad = {
          resolve: () => {
            clearTimeout(timeoutId);
            resolve();
          },
          reject: (error) => {
            clearTimeout(timeoutId);
            reject(error);
          },
        };

        localWorker.postMessage({
          type: "command",
          command: { type: "load_snapshot", snapshot } satisfies SimCommand,
        } satisfies WorkerInbound);
      });
    },

    getSnapshot() {
      return latestSnapshot;
    },

    onSnapshot(callback) {
      listeners.add(callback);
      if (latestSnapshot) callback(latestSnapshot);
      return () => listeners.delete(callback);
    },

    onError(callback) {
      errorListeners.add(callback);
      return () => errorListeners.delete(callback);
    },

    dispose() {
      pendingLoad?.reject(new Error("Sim bridge disposed"));
      pendingLoad = null;
      worker?.postMessage({ type: "dispose" } satisfies WorkerInbound);
      worker?.terminate();
      worker = null;
      listeners.clear();
      errorListeners.clear();
      latestSnapshot = null;
    },
  };
}

/** No-op bridge for tests or explicit stub mode. */
export function createSimBridgeStub(): SimBridge {
  const listeners = new Set<(snapshot: SimSnapshot) => void>();
  const errorListeners = new Set<(error: Error) => void>();

  return {
    async init() {},
    send() {},
    async loadSnapshot() {
      throw new Error("Sim bridge stub cannot load snapshots");
    },
    getSnapshot() {
      return null;
    },
    onSnapshot(callback) {
      listeners.add(callback);
      return () => listeners.delete(callback);
    },
    onError(callback) {
      errorListeners.add(callback);
      return () => errorListeners.delete(callback);
    },
    dispose() {
      listeners.clear();
      errorListeners.clear();
    },
  };
}
