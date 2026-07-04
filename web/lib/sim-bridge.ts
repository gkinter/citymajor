/**
 * Stub sim bridge for WASM worker integration (SB-3666).
 * Procedural city-data remains the data source until the worker lands.
 */

export type SimCommand =
  | { type: "tick"; deltaMs: number }
  | { type: "place_building"; tileX: number; tileZ: number; typeId: number }
  | { type: "pause" }
  | { type: "resume" };

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
  buildings: BuildingSnapshot[];
};

export interface SimBridge {
  init(wasmUrl?: string): Promise<void>;
  send(command: SimCommand): void;
  getSnapshot(): SimSnapshot | null;
  onSnapshot(callback: (snapshot: SimSnapshot) => void): () => void;
  dispose(): void;
}

/** No-op bridge until WASM sim worker is wired (SB-3666). */
export function createSimBridgeStub(): SimBridge {
  const listeners = new Set<(snapshot: SimSnapshot) => void>();

  return {
    async init() {
      // WASM worker bootstrap will load wasmUrl and postMessage commands.
    },
    send(_command: SimCommand) {
      // Commands forwarded to worker once connected.
    },
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
