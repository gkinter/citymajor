/**
 * Sim bridge — Web Worker loads published Forge.SimWasm dotnet bundle (SB-3666).
 * Falls back to procedural city-data when WASM is unavailable (see city-data.ts).
 */

/** 0 = paused, 1 = 1x, 2 = 2x, 4 = 4x */
export type GameSpeedLevel = 0 | 1 | 2 | 4;

export type SimCommand =
  | { type: "tick"; deltaMs: number }
  | { type: "place_building"; tileX: number; tileZ: number; typeId: number }
  | {
      type: "place_road";
      tileX: number;
      tileZ: number;
      tier?: number;
      /** RoadFlags.Bridge (bit 6) — mutually exclusive with tunnel/ramp. */
      bridge?: boolean;
      /** RoadFlags.Tunnel (bit 7) — mutually exclusive with bridge/ramp. */
      tunnel?: boolean;
      /** RoadFlags.Ramp (bits 6+7 = 0xC0). */
      ramp?: boolean;
    }
  | { type: "zone_paint"; tileX: number; tileZ: number; zoneType: number; density?: number }
  | { type: "bulldoze"; tileX: number; tileZ: number }
  | { type: "enqueue_research"; techId: number }
  | { type: "set_law_active"; lawId: string; active: boolean }
  | { type: "set_speed"; level: GameSpeedLevel }
  | { type: "pause" }
  | { type: "resume" }
  | { type: "load_snapshot"; snapshot: SimSnapshot }
  | { type: "load_cmjr"; base64Cmjr: string }
  /** Herald council — one-time treasury change from a council option. */
  | { type: "budget_adjust"; deltaFunds: number }
  /** Herald council — approval swing + optional active-event resolution. */
  | {
      type: "approval_event";
      approvalDelta: number;
      optionId: string;
      eventId?: number;
    }
  /** Herald council — instant RP grant (stub until full policy research hooks). */
  | { type: "research_boost"; points: number };

/** Worker ack for commands that return a WASM bool (e.g. place_road rejection). */
export type SimCommandResult =
  | {
      command: "place_road";
      ok: boolean;
      tileX: number;
      tileZ: number;
      reason?: string;
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

/** Segment-graph node topology (matches Forge.Engine.Data.RoadNodeType). */
export const RoadNodeType = {
  Intersection: 0,
  DeadEnd: 1,
  Corner: 2,
  Ramp: 3,
  HighwayOn: 4,
  HighwayOff: 5,
} as const;

export type RoadNodeTypeValue =
  (typeof RoadNodeType)[keyof typeof RoadNodeType];

/** Parallel arrays indexed by graph node id (Cathedral P1.6). */
export type RoadGraphSnapshot = {
  nodeCount: number;
  nodeTypes: number[];
  nodeTileX: number[];
  nodeTileZ: number[];
  /** Graph edge count — parallel to edgeFrom/To / edgeVolumes / travelTimes (P1.6 / P4.2). */
  edgeCount?: number;
  /** Source node id per edge (CSR neighbor order). */
  edgeFrom?: number[];
  /** Target node id per edge. */
  edgeTo?: number[];
  edgeVolumes?: number[];
  travelTimes?: number[];
};

/** Debug helper — lookup node type at a grid tile from snapshot export. */
export function roadNodeTypeAt(
  graph: RoadGraphSnapshot | undefined,
  tileX: number,
  tileZ: number,
): RoadNodeTypeValue | undefined {
  if (!graph || graph.nodeCount <= 0) return undefined;
  for (let i = 0; i < graph.nodeCount; i++) {
    if (graph.nodeTileX[i] === tileX && graph.nodeTileZ[i] === tileZ) {
      return graph.nodeTypes[i] as RoadNodeTypeValue;
    }
  }
  return undefined;
}

/** Sparse road tiles with congestion density (0–1) from WasmTrafficLite. */
export type TrafficSnapshot = {
  tileX: number;
  tileZ: number;
  density: number;
};

/** Sparse market-zone boundary friction heat (0–1) — Cathedral P3.4. */
export type FrictionCorridorSnapshot = {
  tileX: number;
  tileZ: number;
  friction: number;
};

/** Per-tile service coverage on zoned land (0–1 each). */
export type ServiceCoverageSnapshot = {
  tileX: number;
  tileZ: number;
  health: number;
  police: number;
  fire: number;
  education: number;
};

export type ServiceViewMode = "off" | "health" | "police" | "fire" | "education";

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

/** Per-good Leontief market imbalance from WASM EconomySystem. */
export type GoodImbalance = {
  /** Good enum byte (0–44) when exported from WASM. */
  goodId?: number;
  name: string;
  magnitude: number;
  /** City-wide mean price across active market zones. */
  price?: number;
};

/** City-wide production vs demand for a top-activity good (P3.2 stretch). */
export type GoodFlow = {
  goodId?: number;
  name: string;
  /** City-wide supply (production) units this day. */
  production: number;
  /** City-wide demand units this day. */
  demand: number;
  /** (production − demand) / activity, clamped −1…+1. */
  inventoryRate: number;
};

/** Top shortages/surpluses exported from EconomySystem market zones. */
export type MarketZonePriceSpread = {
  goodId: number;
  name: string;
  minPrice: number;
  maxPrice: number;
  cityAvgPrice: number;
};

export type EconomySnapshot = {
  shortages: GoodImbalance[];
  surpluses: GoodImbalance[];
  /** Top goods by activity with production vs demand. */
  flows?: GoodFlow[];
  /** Active Leontief market partitions (1–16). */
  marketZoneCount?: number;
  /** Per-zone min/max for staple goods when marketZoneCount > 1. */
  marketZonePrices?: MarketZonePriceSpread[];
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

import type { CommuteOdSample } from "@/lib/commute-od";
import type { PopulationL2Snapshot } from "@/lib/population-l2";

export type SimResources = {
  tick: number;
  population: number;
  /** Present when WASM status JSON includes householdCount. */
  householdCount?: number;
  /** WASM GetStatus — net population change per game month when exported. */
  populationGrowthRate?: number;
  cityFunds: number;
  era: number;
  /** WASM GetStatus — progress toward next era (population + tech-count gates). */
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
  /** WASM GetStatus — active research queue head (tech index). */
  currentResearchId?: number;
  /** WASM GetStatus — progress toward current tech (0–1). */
  currentResearchProgress?: number;
  /** WASM GetStatus — estimated months remaining on current tech. */
  currentResearchMonthsRemaining?: number;
  /** WASM GetStatus — unlocked technology indices. */
  unlockedTechIds?: number[];
  /** WASM GetStatus — mayor approval percent (0–100) when PoliticsSystem is exported. */
  approval?: number;
  /** WASM GetStatus — income tax rate (0–100 percent) when PoliticsSystem exports it. */
  taxRate?: number;
  /** City happiness (0–1). */
  happiness?: number;
  /** WASM GetStatus — building pool count; used to detect growth without full render diff. */
  buildingCount?: number;
  /** Current-month income / expense totals from BudgetSystem ledgers. */
  monthlyIncome?: number;
  monthlyExpenses?: number;
  /** Live EventSystem instances from WASM GetStatus / snapshot. */
  activeEvents?: ActiveEventSnapshot[];
  /** WASM GetStatus — count of live events (Herald HUD badge; mirrors activeEvents length). */
  activeEventCount?: number;
  /** WASM GetStatus — mean health coverage over zoned tiles (0–1). */
  healthcareCoverage?: number;
  /** WASM GetStatus — mean police coverage over zoned tiles (0–1). */
  policeCoverage?: number;
  /** WASM GetStatus — mean fire coverage over zoned tiles (0–1). */
  fireCoverage?: number;
  /** WASM GetStatus — mean education coverage over zoned tiles (0–1). */
  educationCoverage?: number;
  /** WASM — top Leontief goods shortages and surpluses. */
  economy?: EconomySnapshot;
  /** WASM GetStatus — global-market export revenue from last trade month. */
  monthlyExportValue?: number;
  /** WASM GetStatus — global-market import cost from last trade month. */
  monthlyImportCost?: number;
  /** WASM GetStatus — net trade balance (exports − imports). */
  tradeBalance?: number;
  /** WASM — named household sample for citizen drill-down (SB-3689). */
  populationL2?: PopulationL2Snapshot;
  /** WASM GetStatus — law definitions loaded from laws.json. */
  lawDefinitionCount?: number;
  /** WASM GetStatus — ordinances currently in effect. */
  activeLawCount?: number;
  /** WASM GetStatus — first catalog entry for sample HUD toggle (v1.5 stub). */
  sampleLaw?: LawPreview;
  /** WASM — share of working-age households with a workplace (0–1). */
  employmentRate?: number;
  /** WASM — mean road-tile traffic density (0–1+). */
  meanTrafficDensity?: number;
  /** WASM — buildings currently in constructing state. */
  constructingBuildingCount?: number;
  /** WASM — fraction of partitions with power supply ≥ demand (0–1). */
  powerCoverageFraction?: number;
  /** WASM — fraction of partitions with water supply ≥ demand (0–1). */
  waterCoverageFraction?: number;
  /** WASM — composite utility stress (0 = healthy, 1 = severe shortage). */
  utilityStressIndex?: number;
  /** WASM — city-wide goods shortage pressure (0–1). */
  goodsShortageIndex?: number;
  /** WASM — city-wide goods surplus pressure (0–1). */
  goodsSurplusIndex?: number;
  /** WASM — daily inter-zone goods volume. */
  interZoneTradeVolume?: number;
  /** WASM — weighted mean trade friction for inter-zone transfers (≥1.0). */
  meanInterZoneFriction?: number;
  /** WASM — composite 0–1 goods transport cost (friction + congestion). */
  goodsTransportCostIndex?: number;
  /** WASM — active Leontief market partitions (1–16). */
  marketZoneCount?: number;
  /** WASM — mean household rent burden (rent / income, 0–1+). */
  meanRentBurden?: number;
  /** WASM — city-wide residential vacancy proxy (0–1). */
  residentialVacancy?: number;
  /** WASM — share of working commuters with valid home + work building IDs (0–1). */
  commuterCoverage?: number;
  /** WASM — top home→work tile pairs aggregated from household assignments. */
  commuteOdSample?: CommuteOdSample[];
  /** WASM — city-wide car mode share from WasmTrafficLite (0–1). */
  carModeShare?: number;
  /** WASM — city-wide transit mode share from WasmTrafficLite (0–1). */
  transitModeShare?: number;
  /** WASM — city-wide walk mode share from WasmTrafficLite (0–1). */
  walkModeShare?: number;
};

export type LawPreview = {
  id: string;
  name: string;
  active: boolean;
};

/** Strip render payload from a full sim snapshot for HUD consumers. */
export function resourcesFromSnapshot(snapshot: SimSnapshot): SimResources {
  const {
    buildings: _buildings,
    zones: _zones,
    roads: _roads,
    traffic: _traffic,
    serviceCoverage: _serviceCoverage,
    frictionCorridors: _frictionCorridors,
    roadGraph: _roadGraph,
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
  /** Segment-graph nodes — parallel nodeTypes[] (debug / ramp viz). */
  roadGraph?: RoadGraphSnapshot;
  /** Sparse road tiles with congestion density (≥0.01). */
  traffic?: TrafficSnapshot[];
  /** Sparse zoned tiles with health/police/fire/education coverage (0–1). */
  serviceCoverage?: ServiceCoverageSnapshot[];
  /** Sparse market-zone boundary friction samples (0–1). */
  frictionCorridors?: FrictionCorridorSnapshot[];
};

export interface SimBridge {
  init(
    wasmUrl?: string,
    worldSize?: number,
    skipStarterCity?: boolean,
  ): Promise<void>;
  send(command: SimCommand): void;
  /** Restore WASM state from a save payload; resolves when the worker acks. */
  loadSnapshot(snapshot: SimSnapshot): Promise<void>;
  /** Restore WASM from CMJR base64 blob (Layer B). */
  loadCmjr(base64Cmjr: string): Promise<void>;
  /** Export CMJR base64 from WASM for cloud save. */
  exportCmjr(): Promise<string | null>;
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
  /** WASM command bool results (place_road rejection, etc.). */
  onCommandResult(callback: (result: SimCommandResult) => void): () => void;
  dispose(): void;
}

/** Client-side snapshot access for save/load UI. */
export type SimClientApi = {
  getSnapshot: () => SimSnapshot | null;
  /** Optimistic UI apply + WASM restore; rejects on worker load failure. */
  applySnapshot: (snapshot: SimSnapshot) => Promise<void>;
  /** Restore from CMJR blob when formatVersion >= 1. */
  applyWasmSave: (base64Cmjr: string) => Promise<void>;
  /** Export CMJR blob for POST /api/saves. */
  exportWasmSave: () => Promise<string | null>;
  sendCommand: (command: SimCommand) => void;
};

declare global {
  interface Window {
    /** Set by PlayClient when localStorage citymajor_smoke=1 (Playwright only). */
    __citymajorSimApi?: SimClientApi | null;
  }
}

const DEFAULT_WASM_URL = "/dotnet";
const DEFAULT_WORLD_SIZE = 256;
/**
 * Cap init() so a worker that never posts `ready` (bad WASM URL, missing
 * blazor.boot.json, module-load exception before the postMessage handler is
 * installed) rejects instead of leaving the caller hanging forever.
 */
/** Cold WASM bundle download + dotnet boot can exceed 15s on slow links. */
const INIT_TIMEOUT_MS = 45_000;

type WorkerInbound =
  | {
      type: "init";
      wasmBaseUrl: string;
      worldSize: number;
      skipStarterCity?: boolean;
    }
  | { type: "command"; command: SimCommand }
  | { type: "export_cmjr" }
  | { type: "dispose" };

type WorkerOutbound =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: SimSnapshot }
  | { type: "load_complete"; ok: boolean }
  | { type: "cmjr_blob"; base64: string }
  | {
      type: "command_result";
      command: "place_road";
      ok: boolean;
      tileX: number;
      tileZ: number;
      reason?: string;
    }
  | { type: "error"; message: string };

const LOAD_SNAPSHOT_TIMEOUT_MS = 15_000;
/** CMJR export serializes full sim JSON — allow extra headroom on preview/CI CPUs. */
const EXPORT_CMJR_TIMEOUT_MS = 45_000;

/** Worker-backed bridge loading the published dotnet WASM bundle at runtime. */
export function createSimBridge(): SimBridge {
  let worker: Worker | null = null;
  let latestSnapshot: SimSnapshot | null = null;
  const listeners = new Set<(snapshot: SimSnapshot) => void>();
  const errorListeners = new Set<(error: Error) => void>();
  const commandResultListeners = new Set<(result: SimCommandResult) => void>();
  let pendingLoad:
    | { resolve: () => void; reject: (error: Error) => void }
    | null = null;
  let pendingExport:
    | { resolve: (base64: string | null) => void; reject: (error: Error) => void }
    | null = null;

  const notifyError = (error: Error) => {
    for (const listener of errorListeners) listener(error);
  };

  const notifyCommandResult = (result: SimCommandResult) => {
    for (const listener of commandResultListeners) listener(result);
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
    if (msg.type === "command_result") {
      notifyCommandResult({
        command: msg.command,
        ok: msg.ok,
        tileX: msg.tileX,
        tileZ: msg.tileZ,
        reason: msg.reason,
      });
      return;
    }
    if (msg.type === "load_complete") {
      settlePendingLoad(msg.ok);
      return;
    }
    if (msg.type === "cmjr_blob") {
      const pending = pendingExport;
      if (pending) {
        pendingExport = null;
        pending.resolve(msg.base64 || null);
      }
      return;
    }
    if (msg.type === "error") {
      const err = new Error(msg.message);
      notifyError(err);
      settlePendingLoad(false, err);
      if (pendingExport) {
        const pending = pendingExport;
        pendingExport = null;
        pending.reject(err);
      }
    }
  };

  const notify = (snapshot: SimSnapshot) => {
    latestSnapshot = snapshot;
    for (const listener of listeners) listener(snapshot);
  };

  return {
    async init(
      wasmUrl = DEFAULT_WASM_URL,
      worldSize = DEFAULT_WORLD_SIZE,
      skipStarterCity = false,
    ) {
      worker = new Worker(
        new URL("../workers/sim-worker.ts", import.meta.url),
        { type: "module" },
      );
      const localWorker = worker;

      try {
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
          skipStarterCity,
        } satisfies WorkerInbound);
        });
      } catch (err) {
        localWorker.terminate();
        if (worker === localWorker) worker = null;
        throw err;
      }

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

    loadCmjr(base64Cmjr) {
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
              `Load CMJR timed out after ${LOAD_SNAPSHOT_TIMEOUT_MS}ms`,
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
          command: { type: "load_cmjr", base64Cmjr } satisfies SimCommand,
        } satisfies WorkerInbound);
      });
    },

    exportCmjr() {
      const localWorker = worker;
      if (!localWorker) {
        return Promise.reject(new Error("Sim worker not initialized"));
      }
      if (pendingExport) {
        return Promise.reject(new Error("Export already in progress"));
      }
      return new Promise<string | null>((resolve, reject) => {
        const timeoutId = setTimeout(() => {
          if (!pendingExport) return;
          pendingExport = null;
          reject(new Error("Export CMJR timed out"));
        }, EXPORT_CMJR_TIMEOUT_MS);

        pendingExport = {
          resolve: (base64) => {
            clearTimeout(timeoutId);
            resolve(base64);
          },
          reject: (error) => {
            clearTimeout(timeoutId);
            reject(error);
          },
        };

        localWorker.postMessage({
          type: "command",
          command: { type: "pause" } satisfies SimCommand,
        } satisfies WorkerInbound);
        localWorker.postMessage({ type: "export_cmjr" } satisfies WorkerInbound);
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

    onCommandResult(callback) {
      commandResultListeners.add(callback);
      return () => commandResultListeners.delete(callback);
    },

    dispose() {
      pendingLoad?.reject(new Error("Sim bridge disposed"));
      pendingLoad = null;
      pendingExport?.reject(new Error("Sim bridge disposed"));
      pendingExport = null;
      worker?.postMessage({ type: "dispose" } satisfies WorkerInbound);
      worker?.terminate();
      worker = null;
      listeners.clear();
      errorListeners.clear();
      commandResultListeners.clear();
      latestSnapshot = null;
    },
  };
}

/** No-op bridge for tests or explicit stub mode. */
export function createSimBridgeStub(): SimBridge {
  const listeners = new Set<(snapshot: SimSnapshot) => void>();
  const errorListeners = new Set<(error: Error) => void>();
  const commandResultListeners = new Set<(result: SimCommandResult) => void>();

  return {
    async init() {},
    send() {},
    async loadSnapshot() {
      throw new Error("Sim bridge stub cannot load snapshots");
    },
    async loadCmjr() {
      throw new Error("Sim bridge stub cannot load CMJR saves");
    },
    async exportCmjr() {
      return null;
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
    onCommandResult(callback) {
      commandResultListeners.add(callback);
      return () => commandResultListeners.delete(callback);
    },
    dispose() {
      listeners.clear();
      errorListeners.clear();
      commandResultListeners.clear();
    },
  };
}
