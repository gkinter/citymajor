/// <reference lib="webworker" />

import type {
  RoadSnapshot,
  SimCommand,
  SimSnapshot,
  ZoneSnapshot,
} from "../lib/sim-bridge";

type WorkerInbound =
  | { type: "init"; wasmBaseUrl: string; worldSize: number }
  | { type: "command"; command: SimCommand }
  | { type: "dispose" };

type WorkerOutbound =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: SimSnapshot }
  | { type: "error"; message: string };

const ctx: DedicatedWorkerGlobalScope =
  self as unknown as DedicatedWorkerGlobalScope;

type WasmStatus = {
  initialized?: boolean;
  tick?: number;
  tickCount?: number;
  population?: number;
  householdCount?: number;
  cityFunds?: number;
  era?: number;
};

type SimExports = {
  Init: (worldSize: number) => void;
  Tick: (dt: number) => number;
  GetRenderSnapshot: () => string;
  GetStatus?: () => string;
  PaintZone?: (x: number, y: number, zoneType: number) => void;
  Bulldoze?: (x: number, y: number) => void;
  PlaceRoad?: (x: number, y: number) => void;
};

let sim: SimExports | null = null;
/** 0 = paused, 1–3 = tick rate multiplier */
let speedLevel = 1;
let worldSize = 256;
/** Sim tick rate — decouple from display RAF to keep 256×256 WASM within budget. */
const SIM_TICK_HZ = 8;
const SIM_TICK_MS = 1000 / SIM_TICK_HZ;
let simAccumMs = 0;
let lastSnapshotMs = 0;
let lastResourceMs = 0;
const SNAPSHOT_MIN_MS = 250;
/** Lightweight GetStatus polls for HUD between full render snapshots. */
const RESOURCE_MIN_MS = 100;
let lastPublished: SimSnapshot | null = null;
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
  };
}

function readStatus(): Pick<
  SimSnapshot,
  "tick" | "population" | "householdCount" | "cityFunds" | "era"
> | null {
  if (!sim?.GetStatus) return null;
  try {
    const parsed = JSON.parse(sim.GetStatus()) as WasmStatus;
    if (parsed.initialized === false) return null;
    return {
      tick: parsed.tick ?? parsed.tickCount ?? 0,
      population: parsed.population ?? 0,
      householdCount: parsed.householdCount,
      cityFunds: parsed.cityFunds ?? 0,
      era: parsed.era ?? 0,
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
    cityFunds: parsed.cityFunds ?? status?.cityFunds ?? 0,
    era: parsed.era ?? status?.era ?? 0,
    buildings: parsed.buildings ?? [],
    zones: mergeZones(parsed.zones, grid),
    roads: mergeRoads(parsed.roads, roadsGrid),
  };
}

function publishSnapshot() {
  const snapshot = readSnapshot();
  lastPublished = snapshot;
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
        while (simAccumMs >= SIM_TICK_MS) {
          simAccumMs -= SIM_TICK_MS;
          sim.Tick(SIM_TICK_MS / 1000);
          simTicked = true;
        }
        if (simTicked) {
          if (now - lastSnapshotMs >= SNAPSHOT_MIN_MS) {
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
  }
};
