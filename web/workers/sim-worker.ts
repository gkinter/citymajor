/// <reference lib="webworker" />

import type {
  ActiveEventSnapshot,
  EconomySnapshot,
  GoodImbalance,
  RoadSnapshot,
  ServiceCoverageSnapshot,
  SimCommand,
  SimSnapshot,
  ZoneSnapshot,
} from "../lib/sim-bridge";
import { parsePopulationL2 } from "../lib/population-l2";

type WorkerInbound =
  | { type: "init"; wasmBaseUrl: string; worldSize: number }
  | { type: "command"; command: SimCommand }
  | { type: "export_cmjr" }
  | { type: "dispose" };

type WorkerOutbound =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: SimSnapshot }
  | { type: "load_complete"; ok: boolean }
  | { type: "cmjr_blob"; base64: string }
  | { type: "error"; message: string };

const ctx: DedicatedWorkerGlobalScope =
  self as unknown as DedicatedWorkerGlobalScope;

type WasmStatus = {
  initialized?: boolean;
  tick?: number;
  tickCount?: number;
  population?: number;
  householdCount?: number;
  populationGrowthRate?: number;
  cityFunds?: number;
  era?: number;
  residentialDemand?: number;
  commercialDemand?: number;
  industrialDemand?: number;
  researchPoints?: number;
  researchRate?: number;
  techCount?: number;
  currentResearchId?: number;
  currentResearchProgress?: number;
  currentResearchMonthsRemaining?: number;
  unlockedTechIds?: number[];
  approval?: number;
  happiness?: number;
  monthlyIncome?: number;
  monthlyExpenses?: number;
  buildingCount?: number;
  eraProgress?: {
    nextEra?: number;
    nextEraName?: string;
    percent?: number;
    gates?: Array<{
      id?: string;
      label?: string;
      current?: number;
      required?: number;
      met?: boolean;
    }>;
  };
  activeEvents?: Array<{
    eventId?: number;
    typeId?: string;
    phase?: string;
    severity?: number;
    tileX?: number;
    tileY?: number;
  }>;
  healthcareCoverage?: number;
  policeCoverage?: number;
  fireCoverage?: number;
  economy?: {
    shortages?: Array<{ name?: string; magnitude?: number }>;
    surpluses?: Array<{ name?: string; magnitude?: number }>;
  };
  monthlyExportValue?: number;
  monthlyImportCost?: number;
  tradeBalance?: number;
  populationL2?: unknown;
  lawDefinitionCount?: number;
  activeLawCount?: number;
};

type SimExports = {
  Init: (worldSize: number) => void;
  Tick: (dt: number) => number;
  GetRenderSnapshot: () => string;
  GetStatus?: () => string;
  PaintZone?: (x: number, y: number, zoneType: number) => void;
  Bulldoze?: (x: number, y: number) => void;
  PlaceRoad?: (x: number, y: number) => void;
  EnqueueResearch?: (techId: number) => boolean;
  LoadSnapshot?: (snapshotJson: string) => boolean;
  ExportCmjr?: () => string;
  LoadFromCmjr?: (base64Cmjr: string) => boolean;
  AdjustBudget?: (deltaFunds: number) => void;
  ApplyApprovalDelta?: (deltaPercent: number) => void;
  BoostResearch?: (points: number) => void;
  ResolveHeraldEvent?: (eventId: number) => boolean;
};

let sim: SimExports | null = null;
/** 0 = paused, 1–3 = tick rate multiplier */
let speedLevel = 1;
let worldSize = 256;
/** Sim tick rate — decouple from display RAF to keep 256×256 WASM within budget. */
const SIM_TICK_HZ = 8;
const SIM_TICK_MS = 1000 / SIM_TICK_HZ;
/**
 * Max sim ticks to catch up in a single "tick" message. Guards against a
 * runaway `while (simAccumMs >= SIM_TICK_MS)` spiral after a long tab-hidden
 * pause / GC stall / dev-tools throttle where `deltaMs` can be seconds.
 * At 8 Hz this caps catch-up work at ~1.5s of sim time per real frame.
 */
const MAX_TICKS_PER_MESSAGE = 12;
let simAccumMs = 0;
let lastSnapshotMs = 0;
let lastResourceMs = 0;
const SNAPSHOT_MIN_MS = 250;
/** Lightweight GetStatus polls for HUD between full render snapshots. */
const RESOURCE_MIN_MS = 100;
let lastPublished: SimSnapshot | null = null;
/** Tracks WASM building pool count to force render snapshots when ZoneGrowthSystem spawns. */
let lastPublishedBuildingCount: number | null = null;
/** Optimistic zone grid — merged into snapshots when WASM lacks zone data. */
let zoneGrid: Uint8Array | null = null;
/** Optimistic road flags grid — merged into snapshots for placed roads. */
let roadGrid: Uint8Array | null = null;

function post(message: WorkerOutbound) {
  ctx.postMessage(message);
}

function ensureZoneGrid(size: number) {
  if (!zoneGrid || zoneGrid.length !== size * size) {
    zoneGrid = new Uint8Array(size * size);
  }
  return zoneGrid;
}

function ensureRoadGrid(size: number) {
  if (!roadGrid || roadGrid.length !== size * size) {
    roadGrid = new Uint8Array(size * size);
  }
  return roadGrid;
}

function zoneIndex(tileX: number, tileZ: number) {
  return tileX + tileZ * worldSize;
}

function collectZonesFromGrid(grid: Uint8Array): ZoneSnapshot[] {
  const zones: ZoneSnapshot[] = [];
  for (let z = 0; z < worldSize; z++) {
    for (let x = 0; x < worldSize; x++) {
      const idx = zoneIndex(x, z);
      const zoneType = grid[idx];
      if (zoneType !== 0) zones.push({ tileX: x, tileZ: z, zoneType });
    }
  }
  return zones;
}

function collectRoadsFromGrid(grid: Uint8Array): RoadSnapshot[] {
  const roads: RoadSnapshot[] = [];
  for (let z = 0; z < worldSize; z++) {
    for (let x = 0; x < worldSize; x++) {
      const idx = zoneIndex(x, z);
      const roadFlags = grid[idx];
      if (roadFlags !== 0) roads.push({ tileX: x, tileZ: z, roadFlags });
    }
  }
  return roads;
}

function mergeZones(
  wasmZones: ZoneSnapshot[] | undefined,
  grid: Uint8Array,
): ZoneSnapshot[] {
  if (!wasmZones?.length) return collectZonesFromGrid(grid);

  const merged = new Map<string, number>();
  for (const z of wasmZones) {
    merged.set(`${z.tileX},${z.tileZ}`, z.zoneType);
  }
  for (let i = 0; i < grid.length; i++) {
    if (grid[i] === 0) continue;
    const x = i % worldSize;
    const z = Math.floor(i / worldSize);
    merged.set(`${x},${z}`, grid[i]);
  }

  const out: ZoneSnapshot[] = [];
  for (const [key, zoneType] of merged) {
    const [tileX, tileZ] = key.split(",").map(Number);
    out.push({ tileX, tileZ, zoneType });
  }
  return out;
}

function mergeRoads(
  wasmRoads: RoadSnapshot[] | undefined,
  grid: Uint8Array,
): RoadSnapshot[] {
  if (!wasmRoads?.length) return collectRoadsFromGrid(grid);

  const merged = new Map<string, number>();
  for (const r of wasmRoads) {
    merged.set(`${r.tileX},${r.tileZ}`, r.roadFlags);
  }
  for (let i = 0; i < grid.length; i++) {
    if (grid[i] === 0) continue;
    const x = i % worldSize;
    const z = Math.floor(i / worldSize);
    merged.set(`${x},${z}`, grid[i]);
  }

  const out: RoadSnapshot[] = [];
  for (const [key, roadFlags] of merged) {
    const [tileX, tileZ] = key.split(",").map(Number);
    out.push({ tileX, tileZ, roadFlags });
  }
  return out;
}

async function loadWasm(baseUrl: string): Promise<SimExports> {
  const framework = `${baseUrl}/_framework`;
  const { dotnet } = await import(/* webpackIgnore: true */ `${framework}/dotnet.js`);
  const api = await dotnet
    .withConfigSrc(`${framework}/blazor.boot.json`)
    .create();
  const config = api.getConfig();
  const assemblyName = config.mainAssemblyName ?? "Forge.SimWasm.dll";
  const raw = await api.getAssemblyExports(assemblyName);
  return resolveExports(raw as Record<string, unknown>);
}

function resolveExports(raw: Record<string, unknown>): SimExports {
  const forge = raw.Forge as Record<string, unknown> | undefined;
  const simWasm = forge?.SimWasm as Record<string, unknown> | undefined;
  const program = simWasm?.Program as Record<string, unknown> | undefined;
  const source =
    program ?? (raw.Program as Record<string, unknown> | undefined) ?? raw;
  const init = source.Init ?? source.init;
  const tick = source.Tick ?? source.tick;
  const getRenderSnapshot =
    source.GetRenderSnapshot ?? source.getRenderSnapshot;
  const getStatus = source.GetStatus ?? source.getStatus;
  const paintZone = source.PaintZone ?? source.paintZone;
  const bulldoze = source.Bulldoze ?? source.bulldoze;
  const placeRoad = source.PlaceRoad ?? source.placeRoad;
  const enqueueResearch = source.EnqueueResearch ?? source.enqueueResearch;
  const loadSnapshot = source.LoadSnapshot ?? source.loadSnapshot;
  const exportCmjr = source.ExportCmjr ?? source.exportCmjr;
  const loadFromCmjr = source.LoadFromCmjr ?? source.loadFromCmjr;
  const adjustBudget = source.AdjustBudget ?? source.adjustBudget;
  const applyApprovalDelta =
    source.ApplyApprovalDelta ?? source.applyApprovalDelta;
  const boostResearch = source.BoostResearch ?? source.boostResearch;
  const resolveHeraldEvent =
    source.ResolveHeraldEvent ?? source.resolveHeraldEvent;
  if (
    typeof init !== "function" ||
    typeof tick !== "function" ||
    typeof getRenderSnapshot !== "function"
  ) {
    throw new TypeError(
      `Missing sim exports. Keys: ${JSON.stringify(Object.keys(raw))}`,
    );
  }
  return {
    Init: init as (worldSize: number) => void,
    Tick: tick as (dt: number) => number,
    GetRenderSnapshot: getRenderSnapshot as () => string,
    GetStatus:
      typeof getStatus === "function"
        ? (getStatus as () => string)
        : undefined,
    PaintZone:
      typeof paintZone === "function"
        ? (paintZone as (x: number, y: number, zoneType: number) => void)
        : undefined,
    Bulldoze:
      typeof bulldoze === "function"
        ? (bulldoze as (x: number, y: number) => void)
        : undefined,
    PlaceRoad:
      typeof placeRoad === "function"
        ? (placeRoad as (x: number, y: number) => void)
        : undefined,
    EnqueueResearch:
      typeof enqueueResearch === "function"
        ? (enqueueResearch as (techId: number) => boolean)
        : undefined,
    LoadSnapshot:
      typeof loadSnapshot === "function"
        ? (loadSnapshot as (snapshotJson: string) => boolean)
        : undefined,
    ExportCmjr:
      typeof exportCmjr === "function"
        ? (exportCmjr as () => string)
        : undefined,
    LoadFromCmjr:
      typeof loadFromCmjr === "function"
        ? (loadFromCmjr as (base64Cmjr: string) => boolean)
        : undefined,
    AdjustBudget:
      typeof adjustBudget === "function"
        ? (adjustBudget as (deltaFunds: number) => void)
        : undefined,
    ApplyApprovalDelta:
      typeof applyApprovalDelta === "function"
        ? (applyApprovalDelta as (deltaPercent: number) => void)
        : undefined,
    BoostResearch:
      typeof boostResearch === "function"
        ? (boostResearch as (points: number) => void)
        : undefined,
    ResolveHeraldEvent:
      typeof resolveHeraldEvent === "function"
        ? (resolveHeraldEvent as (eventId: number) => boolean)
        : undefined,
  };
}

function parseActiveEvents(
  raw: unknown,
): ActiveEventSnapshot[] | undefined {
  if (!Array.isArray(raw)) return undefined;
  const events: ActiveEventSnapshot[] = [];
  for (const item of raw) {
    if (typeof item !== "object" || item === null) continue;
    const e = item as Record<string, unknown>;
    if (typeof e.typeId !== "string") continue;
    events.push({
      eventId: typeof e.eventId === "number" ? e.eventId : 0,
      typeId: e.typeId,
      phase: typeof e.phase === "string" ? e.phase : "active",
      severity: typeof e.severity === "number" ? e.severity : 0,
      tileX: typeof e.tileX === "number" ? e.tileX : -1,
      tileY: typeof e.tileY === "number" ? e.tileY : -1,
    });
  }
  return events.length > 0 ? events : undefined;
}

function parseServiceCoverage(
  raw: unknown,
): ServiceCoverageSnapshot[] | undefined {
  if (!Array.isArray(raw)) return undefined;
  const tiles: ServiceCoverageSnapshot[] = [];
  for (const item of raw) {
    if (typeof item !== "object" || item === null) continue;
    const t = item as Record<string, unknown>;
    if (typeof t.tileX !== "number" || typeof t.tileZ !== "number") continue;
    tiles.push({
      tileX: t.tileX,
      tileZ: t.tileZ,
      health: typeof t.health === "number" ? t.health : 0,
      police: typeof t.police === "number" ? t.police : 0,
      fire: typeof t.fire === "number" ? t.fire : 0,
    });
  }
  return tiles.length > 0 ? tiles : undefined;
}

function parseGoodImbalances(raw: unknown): GoodImbalance[] {
  if (!Array.isArray(raw)) return [];
  const rows: GoodImbalance[] = [];
  for (const item of raw) {
    if (typeof item !== "object" || item === null) continue;
    const row = item as Record<string, unknown>;
    if (typeof row.name !== "string") continue;
    rows.push({
      name: row.name,
      magnitude: typeof row.magnitude === "number" ? row.magnitude : 0,
    });
  }
  return rows;
}

function parseEconomy(raw: unknown): EconomySnapshot | undefined {
  if (raw === undefined) return undefined;
  if (typeof raw !== "object" || raw === null) return undefined;
  const economy = raw as Record<string, unknown>;
  return {
    shortages: parseGoodImbalances(economy.shortages),
    surpluses: parseGoodImbalances(economy.surpluses),
  };
}

function readStatus(): Pick<
  SimSnapshot,
  | "tick"
  | "population"
  | "householdCount"
  | "populationGrowthRate"
  | "cityFunds"
  | "era"
  | "residentialDemand"
  | "commercialDemand"
  | "industrialDemand"
  | "researchPoints"
  | "researchRate"
  | "techCount"
  | "currentResearchId"
  | "currentResearchProgress"
  | "currentResearchMonthsRemaining"
  | "unlockedTechIds"
  | "approval"
  | "happiness"
  | "monthlyIncome"
  | "monthlyExpenses"
  | "buildingCount"
  | "eraProgress"
  | "activeEvents"
  | "healthcareCoverage"
  | "policeCoverage"
  | "fireCoverage"
  | "economy"
  | "monthlyExportValue"
  | "monthlyImportCost"
  | "tradeBalance"
  | "populationL2"
  | "lawDefinitionCount"
  | "activeLawCount"
> | null {
  if (!sim?.GetStatus) return null;
  try {
    const parsed = JSON.parse(sim.GetStatus()) as WasmStatus;
    if (parsed.initialized === false) return null;

    const rawProgress = parsed.eraProgress;
    const eraProgress =
      rawProgress && typeof rawProgress.nextEra === "number"
        ? {
            nextEra: rawProgress.nextEra,
            nextEraName: rawProgress.nextEraName ?? "",
            percent: rawProgress.percent ?? 0,
            gates: (rawProgress.gates ?? []).map((gate) => ({
              id: gate.id ?? "",
              label: gate.label ?? gate.id ?? "",
              current: gate.current ?? 0,
              required: gate.required ?? 0,
              met: gate.met ?? false,
            })),
          }
        : undefined;

    return {
      tick: parsed.tick ?? parsed.tickCount ?? 0,
      population: parsed.population ?? 0,
      householdCount: parsed.householdCount,
      populationGrowthRate: parsed.populationGrowthRate,
      cityFunds: parsed.cityFunds ?? 0,
      era: parsed.era ?? 0,
      residentialDemand: parsed.residentialDemand,
      commercialDemand: parsed.commercialDemand,
      industrialDemand: parsed.industrialDemand,
      researchPoints: parsed.researchPoints,
      researchRate: parsed.researchRate,
      techCount: parsed.techCount,
      currentResearchId: parsed.currentResearchId,
      currentResearchProgress: parsed.currentResearchProgress,
      currentResearchMonthsRemaining: parsed.currentResearchMonthsRemaining,
      unlockedTechIds: parsed.unlockedTechIds,
      approval: parsed.approval,
      happiness: parsed.happiness,
      monthlyIncome: parsed.monthlyIncome,
      monthlyExpenses: parsed.monthlyExpenses,
      buildingCount: parsed.buildingCount,
      eraProgress,
      activeEvents: parseActiveEvents(parsed.activeEvents),
      healthcareCoverage: parsed.healthcareCoverage,
      policeCoverage: parsed.policeCoverage,
      fireCoverage: parsed.fireCoverage,
      economy: parseEconomy(parsed.economy),
      monthlyExportValue: parsed.monthlyExportValue,
      monthlyImportCost: parsed.monthlyImportCost,
      tradeBalance: parsed.tradeBalance,
      populationL2: parsePopulationL2(parsed.populationL2),
      lawDefinitionCount: parsed.lawDefinitionCount,
      activeLawCount: parsed.activeLawCount,
    };
  } catch {
    return null;
  }
}

function readSnapshot(): SimSnapshot {
  if (!sim) {
    return {
      tick: 0,
      population: 0,
      cityFunds: 0,
      era: 0,
      buildings: [],
      zones: [],
      roads: [],
    };
  }

  const parsed = JSON.parse(sim.GetRenderSnapshot()) as Partial<SimSnapshot>;
  const status = readStatus();
  const grid = ensureZoneGrid(worldSize);
  const roadsGrid = ensureRoadGrid(worldSize);
  return {
    tick: parsed.tick ?? status?.tick ?? 0,
    population: parsed.population ?? status?.population ?? 0,
    householdCount: parsed.householdCount ?? status?.householdCount,
    populationGrowthRate:
      parsed.populationGrowthRate ?? status?.populationGrowthRate,
    cityFunds: parsed.cityFunds ?? status?.cityFunds ?? 0,
    era: parsed.era ?? status?.era ?? 0,
    residentialDemand:
      parsed.residentialDemand ?? status?.residentialDemand,
    commercialDemand: parsed.commercialDemand ?? status?.commercialDemand,
    industrialDemand: parsed.industrialDemand ?? status?.industrialDemand,
    researchPoints: parsed.researchPoints ?? status?.researchPoints,
    researchRate: parsed.researchRate ?? status?.researchRate,
    techCount: parsed.techCount ?? status?.techCount,
    currentResearchId: parsed.currentResearchId ?? status?.currentResearchId,
    currentResearchProgress:
      parsed.currentResearchProgress ?? status?.currentResearchProgress,
    currentResearchMonthsRemaining:
      parsed.currentResearchMonthsRemaining ??
      status?.currentResearchMonthsRemaining,
    unlockedTechIds: parsed.unlockedTechIds ?? status?.unlockedTechIds,
    approval: parsed.approval ?? status?.approval,
    happiness: parsed.happiness ?? status?.happiness,
    monthlyIncome: parsed.monthlyIncome ?? status?.monthlyIncome,
    monthlyExpenses: parsed.monthlyExpenses ?? status?.monthlyExpenses,
    eraProgress: parsed.eraProgress ?? status?.eraProgress,
    activeEvents:
      parseActiveEvents(parsed.activeEvents) ?? status?.activeEvents,
    healthcareCoverage:
      parsed.healthcareCoverage ?? status?.healthcareCoverage,
    policeCoverage: parsed.policeCoverage ?? status?.policeCoverage,
    fireCoverage: parsed.fireCoverage ?? status?.fireCoverage,
    economy: parseEconomy(parsed.economy) ?? status?.economy,
    monthlyExportValue:
      parsed.monthlyExportValue ?? status?.monthlyExportValue,
    monthlyImportCost: parsed.monthlyImportCost ?? status?.monthlyImportCost,
    tradeBalance: parsed.tradeBalance ?? status?.tradeBalance,
    populationL2: parsePopulationL2(parsed.populationL2) ?? status?.populationL2,
    lawDefinitionCount:
      parsed.lawDefinitionCount ?? status?.lawDefinitionCount,
    activeLawCount: parsed.activeLawCount ?? status?.activeLawCount,
    buildings: parsed.buildings ?? [],
    zones: mergeZones(parsed.zones, grid),
    roads: mergeRoads(parsed.roads, roadsGrid),
    traffic: parsed.traffic ?? [],
    serviceCoverage: parseServiceCoverage(parsed.serviceCoverage),
  };
}

function publishSnapshot() {
  const snapshot = readSnapshot();
  lastPublished = snapshot;
  lastPublishedBuildingCount =
    readStatus()?.buildingCount ?? snapshot.buildings.length;
  post({ type: "snapshot", snapshot });
}

function publishResourceUpdate() {
  const status = readStatus();
  if (!status) return;

  const base = lastPublished ?? readSnapshot();
  const snapshot: SimSnapshot = { ...base, ...status };
  lastPublished = snapshot;
  post({ type: "snapshot", snapshot });
}

function paintZone(tileX: number, tileZ: number, zoneType: number) {
  const grid = ensureZoneGrid(worldSize);
  if (tileX < 0 || tileZ < 0 || tileX >= worldSize || tileZ >= worldSize) return;
  grid[zoneIndex(tileX, tileZ)] = zoneType;
  sim?.PaintZone?.(tileX, tileZ, zoneType);
  publishSnapshot();
}

function bulldozeTile(tileX: number, tileZ: number) {
  const grid = ensureZoneGrid(worldSize);
  if (tileX < 0 || tileZ < 0 || tileX >= worldSize || tileZ >= worldSize) return;
  grid[zoneIndex(tileX, tileZ)] = 0;
  sim?.Bulldoze?.(tileX, tileZ);
  publishSnapshot();
}

function placeRoadTile(tileX: number, tileZ: number) {
  const grid = ensureRoadGrid(worldSize);
  if (tileX < 0 || tileZ < 0 || tileX >= worldSize || tileZ >= worldSize) return;
  grid[zoneIndex(tileX, tileZ)] = 1;
  sim?.PlaceRoad?.(tileX, tileZ);
  publishSnapshot();
}

function enqueueResearchTech(techId: number) {
  if (techId < 0) return;
  const ok = sim?.EnqueueResearch?.(techId) ?? false;
  if (ok) publishResourceUpdate();
}

function adjustBudgetFunds(deltaFunds: number) {
  if (!deltaFunds) return;
  sim?.AdjustBudget?.(deltaFunds);
  publishResourceUpdate();
}

function applyApprovalEvent(
  approvalDelta: number,
  optionId: string,
  eventId?: number,
) {
  if (approvalDelta !== 0) {
    sim?.ApplyApprovalDelta?.(approvalDelta);
  }
  if (eventId !== undefined && eventId >= 0) {
    const resolved = sim?.ResolveHeraldEvent?.(eventId) ?? false;
    if (!resolved) {
      console.info(
        "[CityMajor] Herald event resolve missed",
        { eventId, optionId },
      );
    }
  }
  publishResourceUpdate();
}

function boostResearchPoints(points: number) {
  if (points <= 0) return;
  sim?.BoostResearch?.(points);
  publishResourceUpdate();
}

function syncGridsFromSnapshot(snapshot: SimSnapshot) {
  const zg = ensureZoneGrid(worldSize);
  zg.fill(0);
  for (const zone of snapshot.zones ?? []) {
    if (
      zone.tileX < 0 ||
      zone.tileZ < 0 ||
      zone.tileX >= worldSize ||
      zone.tileZ >= worldSize
    ) {
      continue;
    }
    zg[zoneIndex(zone.tileX, zone.tileZ)] = zone.zoneType;
  }

  const rg = ensureRoadGrid(worldSize);
  rg.fill(0);
  for (const road of snapshot.roads ?? []) {
    if (
      road.tileX < 0 ||
      road.tileZ < 0 ||
      road.tileX >= worldSize ||
      road.tileZ >= worldSize
    ) {
      continue;
    }
    rg[zoneIndex(road.tileX, road.tileZ)] = road.roadFlags || 1;
  }
}

function loadCmjrBlob(base64Cmjr: string) {
  if (!sim) {
    post({ type: "load_complete", ok: false });
    post({ type: "error", message: "Sim worker not initialized" });
    return;
  }

  if (!sim.LoadFromCmjr) {
    post({ type: "load_complete", ok: false });
    post({ type: "error", message: "LoadFromCmjr export missing from WASM" });
    return;
  }

  const restored = sim.LoadFromCmjr(base64Cmjr);
  if (!restored) {
    post({ type: "load_complete", ok: false });
    post({ type: "error", message: "Failed to restore WASM CMJR save" });
    return;
  }

  const snapshot = readSnapshot();
  syncGridsFromSnapshot(snapshot);
  simAccumMs = 0;
  lastSnapshotMs = 0;
  lastResourceMs = 0;
  lastPublishedBuildingCount = null;
  publishSnapshot();
  post({ type: "load_complete", ok: true });
}

function loadSnapshot(snapshot: SimSnapshot) {
  if (!sim) {
    post({ type: "load_complete", ok: false });
    post({ type: "error", message: "Sim worker not initialized" });
    return;
  }

  let restored = true;
  if (sim.LoadSnapshot) {
    restored = sim.LoadSnapshot(JSON.stringify(snapshot));
    if (!restored) {
      post({ type: "load_complete", ok: false });
      post({ type: "error", message: "Failed to restore WASM snapshot" });
      return;
    }
  } else {
    console.warn(
      "[sim-worker] LoadSnapshot export missing — syncing grids only",
    );
  }

  syncGridsFromSnapshot(snapshot);
  simAccumMs = 0;
  lastSnapshotMs = 0;
  lastResourceMs = 0;
  lastPublishedBuildingCount = null;
  publishSnapshot();
  post({ type: "load_complete", ok: true });
}

function clampSpeedLevel(level: number): 0 | 1 | 2 | 3 {
  if (level <= 0) return 0;
  if (level >= 3) return 3;
  return level as 1 | 2 | 3;
}

function handleCommand(command: SimCommand) {
  if (!sim) return;

  switch (command.type) {
    case "set_speed":
      speedLevel = clampSpeedLevel(command.level);
      break;
    case "pause":
      speedLevel = 0;
      break;
    case "resume":
      if (speedLevel === 0) speedLevel = 1;
      break;
    case "tick":
      if (speedLevel > 0) {
        simAccumMs += command.deltaMs * speedLevel;
        const now = performance.now();
        let simTicked = false;
        let ticksThisMessage = 0;
        while (
          simAccumMs >= SIM_TICK_MS &&
          ticksThisMessage < MAX_TICKS_PER_MESSAGE
        ) {
          simAccumMs -= SIM_TICK_MS;
          sim.Tick(SIM_TICK_MS / 1000);
          simTicked = true;
          ticksThisMessage += 1;
        }
        // Drop backlog beyond the catch-up cap so the sim recovers cleanly
        // after long pauses instead of trying to run seconds of ticks now.
        if (simAccumMs > SIM_TICK_MS * MAX_TICKS_PER_MESSAGE) {
          simAccumMs = SIM_TICK_MS * MAX_TICKS_PER_MESSAGE;
        }
        if (simTicked) {
          const status = readStatus();
          const wasmBuildingCount = status?.buildingCount;
          const buildingCountChanged =
            wasmBuildingCount !== undefined &&
            wasmBuildingCount !== lastPublishedBuildingCount;

          if (buildingCountChanged || now - lastSnapshotMs >= SNAPSHOT_MIN_MS) {
            lastSnapshotMs = now;
            lastResourceMs = now;
            publishSnapshot();
          } else if (now - lastResourceMs >= RESOURCE_MIN_MS) {
            lastResourceMs = now;
            publishResourceUpdate();
          }
        }
      }
      break;
    case "place_building":
      // WASM placement API lands in a follow-up; tick-driven growth still runs.
      break;
    case "place_road":
      placeRoadTile(command.tileX, command.tileZ);
      break;
    case "zone_paint":
      paintZone(command.tileX, command.tileZ, command.zoneType);
      break;
    case "bulldoze":
      bulldozeTile(command.tileX, command.tileZ);
      break;
    case "enqueue_research":
      enqueueResearchTech(command.techId);
      break;
    case "load_snapshot":
      loadSnapshot(command.snapshot);
      break;
    case "load_cmjr":
      loadCmjrBlob(command.base64Cmjr);
      break;
    case "budget_adjust":
      adjustBudgetFunds(command.deltaFunds);
      break;
    case "approval_event":
      applyApprovalEvent(
        command.approvalDelta,
        command.optionId,
        command.eventId,
      );
      break;
    case "research_boost":
      boostResearchPoints(command.points);
      break;
  }
}

ctx.onmessage = async (event: MessageEvent<WorkerInbound>) => {
  const msg = event.data;

  if (msg.type === "dispose") {
    sim = null;
    zoneGrid = null;
    roadGrid = null;
    lastPublished = null;
    return;
  }

  if (msg.type === "init") {
    try {
      worldSize = msg.worldSize || 256;
      ensureZoneGrid(worldSize);
      ensureRoadGrid(worldSize);
      sim = await loadWasm(msg.wasmBaseUrl);
      sim.Init(worldSize);
      simAccumMs = 0;
      lastSnapshotMs = 0;
      lastResourceMs = 0;
      lastPublished = null;
      lastPublishedBuildingCount = null;
      publishSnapshot();
      post({ type: "ready" });
    } catch (err) {
      post({ type: "error", message: String(err) });
    }
    return;
  }

  if (msg.type === "command") {
    try {
      handleCommand(msg.command);
    } catch (err) {
      post({ type: "error", message: String(err) });
    }
    return;
  }

  if (msg.type === "export_cmjr") {
    try {
      if (!sim?.ExportCmjr) {
        post({ type: "error", message: "ExportCmjr not available" });
        return;
      }
      const base64 = sim.ExportCmjr();
      if (!base64) {
        post({ type: "error", message: "ExportCmjr returned empty blob" });
        return;
      }
      post({ type: "cmjr_blob", base64 });
    } catch (err) {
      post({ type: "error", message: String(err) });
    }
  }
};
