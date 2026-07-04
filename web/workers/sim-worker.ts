/// <reference lib="webworker" />

import type { SimCommand, SimSnapshot } from "../lib/sim-bridge";

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
};

let sim: SimExports | null = null;
let paused = false;

function post(message: WorkerOutbound) {
  ctx.postMessage(message);
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
  };
}

function readSnapshot(): SimSnapshot {
  if (!sim) return { tick: 0, buildings: [] };
  return JSON.parse(sim.GetRenderSnapshot()) as SimSnapshot;
}

function publishSnapshot() {
  post({ type: "snapshot", snapshot: readSnapshot() });
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
  }
}

ctx.onmessage = async (event: MessageEvent<WorkerInbound>) => {
  const msg = event.data;

  if (msg.type === "dispose") {
    sim = null;
    return;
  }

  if (msg.type === "init") {
    try {
      sim = await loadWasm(msg.wasmBaseUrl);
      sim.Init(msg.worldSize || 256);
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
