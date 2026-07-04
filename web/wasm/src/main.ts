import type { SimSnapshot } from './sim-worker';

const logEl = document.getElementById('log')!;
const runBtn = document.getElementById('run') as HTMLButtonElement;
const stopBtn = document.getElementById('stop') as HTMLButtonElement;

// Published WASM output — run `dotnet publish` first (see web/wasm/README.md).
const WASM_BASE = '/dotnet';

const worker = new Worker(new URL('./sim-worker.ts', import.meta.url), { type: 'module' });

function append(line: string, cls = '') {
  const span = cls ? `<span class="${cls}">` : '';
  const end = cls ? '</span>' : '';
  logEl.innerHTML += `${span}${line}${end}\n`;
}

worker.onmessage = (event) => {
  const msg = event.data;
  switch (msg.type) {
    case 'log':
      append(msg.message);
      break;
    case 'progress': {
      const s = msg.snapshot as SimSnapshot;
      append(
        `tick ${msg.tick}/${msg.total} — simTick=${s.tick} buildings=${s.buildings.length}`,
      );
      break;
    }
    case 'ready':
      append('Worker ready.', 'ok');
      runBtn.disabled = false;
      break;
    case 'done': {
      const s = msg.snapshot as SimSnapshot;
      append(`Done ${msg.ticks} ticks in ${msg.elapsedMs.toFixed(0)}ms`, 'ok');
      append(`Final: ${JSON.stringify({ ...s, buildings: `[${s.buildings.length} items]` })}`);
      runBtn.disabled = false;
      stopBtn.disabled = true;
      break;
    }
    case 'error':
      append(`ERROR: ${msg.message}`, 'warn');
      runBtn.disabled = false;
      stopBtn.disabled = true;
      break;
  }
  logEl.scrollTop = logEl.scrollHeight;
};

runBtn.addEventListener('click', () => {
  runBtn.disabled = true;
  stopBtn.disabled = false;
  append('--- run 1000 ticks @ 12 Hz ---');
  worker.postMessage({ type: 'run', ticks: 1000, tickHz: 12 });
});

stopBtn.addEventListener('click', () => {
  worker.postMessage({ type: 'stop' });
  stopBtn.disabled = true;
});

append('Starting worker…');
worker.postMessage({ type: 'init', wasmBaseUrl: WASM_BASE, worldSize: 64 });
