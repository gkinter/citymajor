/// <reference lib="webworker" />

import type { SimCommand, SimSnapshot, ZoneSnapshot } from "../lib/sim-bridge";

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

type SimExports = {
  Init: (worldSize: number) => void;
  Tick: (dt: number) => number;
  GetRenderSnapshot: () => string;
  PaintZone?: (x: number, y: number, zoneType: number) => void;
  Bulldoze?: (x: number, y: number) => void;
};

let sim: SimExports | null = null;
let paused = false;
let worldSize = 256;
/** Optimistic zone grid — merged into snapshots when WASM lacks zone data. */
let zoneGrid: Uint8Array | null = null;

function post(message: WorkerOutbound) {
  ctx.postMessage(message);
}

function ensureZoneGrid(size: number) {
  if (!zoneGrid || zoneGrid.length !== size * size) {
    zoneGrid = new Uint8Array(size * size);
  }
  return zoneGrid;
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
  const paintZone = source.PaintZone ?? source.paintZone;
  const bulldoze = source.Bulldoze ?? source.bulldoze;
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
    PaintZone:
      typeof paintZone === "function"
        ? (paintZone as (x: number, y: number, zoneType: number) => void)
        : undefined,
    Bulldoze:
      typeof bulldoze === "function"
        ? (bulldoze as (x: number, y: number) => void)
        : undefined,
  };
}

function readSnapshot(): SimSnapshot {
  if (!sim) return { tick: 0, buildings: [], zones: [] };

  const parsed = JSON.parse(sim.GetRenderSnapshot()) as SimSnapshot;
  const grid = ensureZoneGrid(worldSize);
  return {
    ...parsed,
    zones: mergeZones(parsed.zones, grid),
  };
}

function publishSnapshot() {
  post({ type: "snapshot", snapshot: readSnapshot() });
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

function handleCommand(command: SimCommand) {
  if (!sim) return;

  switch (command.type) {
    case "pause":
      paused = true;
      break;
    case "resume":
      paused = false;
      break;
    case "tick":
      if (!paused) {
        sim.Tick(command.deltaMs / 1000);
        publishSnapshot();
      }
      break;
    case "place_building":
      // WASM placement API lands in a follow-up; tick-driven growth still runs.
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
    return;
  }

  if (msg.type === "init") {
    try {
      worldSize = msg.worldSize || 256;
      ensureZoneGrid(worldSize);
      sim = await loadWasm(msg.wasmBaseUrl);
      sim.Init(worldSize);
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
