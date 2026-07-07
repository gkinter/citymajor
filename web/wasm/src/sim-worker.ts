/// <reference lib="webworker" />

/** Matches web/lib/sim-bridge.ts (citymajor-web-r3f-spike). */
export type BuildingSnapshot = {
  id: number;
  typeId: number;
  tileX: number;
  tileZ: number;
  level: number;
  state: number;
  condition: number;
};

export type SimSnapshot = {
  tick: number;
  population: number;
  cityFunds: number;
  era: number;
  buildings: BuildingSnapshot[];
};

type WorkerInbound =
  | { type: 'init'; wasmBaseUrl: string; worldSize: number }
  | { type: 'run'; ticks: number; tickHz: number }
  | { type: 'stop' };

type WorkerOutbound =
  | { type: 'ready' }
  | { type: 'log'; message: string }
  | { type: 'progress'; tick: number; total: number; snapshot: SimSnapshot }
  | { type: 'done'; ticks: number; elapsedMs: number; snapshot: SimSnapshot }
  | { type: 'error'; message: string };

const ctx: DedicatedWorkerGlobalScope = self as unknown as DedicatedWorkerGlobalScope;

let cancelled = false;

function post(message: WorkerOutbound) {
  ctx.postMessage(message);
}

async function loadWasm(baseUrl: string) {
  const framework = `${baseUrl}/_framework`;
  const { dotnet } = await import(/* @vite-ignore */ `${framework}/dotnet.js`);
  const api = await dotnet
    .withConfigSrc(`${framework}/blazor.boot.json`)
    .create();
  const config = api.getConfig();
  const assemblyName = (config.mainAssemblyName ?? 'Forge.SimWasm.dll').replace(/\.dll$/i, '');
  const raw = await api.getAssemblyExports(assemblyName);
  return resolveExports(raw as Record<string, unknown>);
}

type WasmSimExports = {
  Init: (worldSize: number) => void;
  Tick: (dt: number) => number;
  GetRenderSnapshot: () => string;
  GetStatus: () => string;
};

function resolveExports(raw: Record<string, unknown>): WasmSimExports {
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
  if (
    typeof init !== 'function' ||
    typeof tick !== 'function' ||
    typeof getRenderSnapshot !== 'function' ||
    typeof getStatus !== 'function'
  ) {
    throw new TypeError(
      `Missing sim exports. Keys: ${JSON.stringify(Object.keys(raw))}`,
    );
  }
  return {
    Init: init as (worldSize: number) => void,
    Tick: tick as (dt: number) => number,
    GetRenderSnapshot: getRenderSnapshot as () => string,
    GetStatus: getStatus as () => string,
  };
}

ctx.onmessage = async (event: MessageEvent<WorkerInbound>) => {
  const msg = event.data;
  if (msg.type === 'stop') {
    cancelled = true;
    return;
  }

  if (msg.type === 'init') {
    try {
      post({ type: 'log', message: `Loading WASM from ${msg.wasmBaseUrl}…` });
      const exports = await loadWasm(msg.wasmBaseUrl);
      (ctx as unknown as { sim: typeof exports }).sim = exports;
      exports.Init(msg.worldSize || 256);
      const status = exports.GetStatus();
      post({ type: 'log', message: status });
      post({ type: 'ready' });
    } catch (err) {
      post({ type: 'error', message: String(err) });
    }
    return;
  }

  if (msg.type === 'run') {
    cancelled = false;
    const sim = (ctx as unknown as { sim: {
      Tick: (dt: number) => number;
      GetRenderSnapshot: () => string;
    } }).sim;

    if (!sim) {
      post({ type: 'error', message: 'Worker not initialized — call init first.' });
      return;
    }

    const total = msg.ticks;
    const hz = msg.tickHz || 12;
    const dt = 1 / hz;
    const intervalMs = 1000 / hz;
    const started = performance.now();
    let lastSnapshot: SimSnapshot = { tick: 0, population: 0, cityFunds: 0, era: 0, buildings: [] };

    for (let i = 1; i <= total && !cancelled; i++) {
      sim.Tick(dt);
      if (i % 50 === 0 || i === total) {
        lastSnapshot = JSON.parse(sim.GetRenderSnapshot()) as SimSnapshot;
        post({ type: 'progress', tick: i, total, snapshot: lastSnapshot });
      }
      if (i < total) await new Promise((r) => setTimeout(r, intervalMs));
    }

    post({
      type: 'done',
      ticks: cancelled ? -1 : total,
      elapsedMs: performance.now() - started,
      snapshot: lastSnapshot,
    });
  }
};
