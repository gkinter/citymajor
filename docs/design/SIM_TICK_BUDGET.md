# SimHost tick budget (WP-D)

Characterization of native `Forge.SimCore.SimHost` wall-clock cost per 8 Hz frame (`Tick(0.125)`).

## FPS vs tick-ms (read this first)

| Gate | What it measures | Source of truth |
|------|------------------|-----------------|
| **WEB_V1_SCOPE ≥30 FPS** (integrated) / ≥60 (discrete) | **Render wall-clock** with LOD on the GPU path | [`WEB_V1_SCOPE.md`](./WEB_V1_SCOPE.md) §4 · `pnpm perf:gate` |
| **Sim tick-ms** (this doc) | Native/WASM `SimHost.Tick` median on CPU | `TickBudgetTests` |

These are **independent**. A Mac that spends ~90 ms in a v1-scale sim tick can still hit ≥30 FPS if the worker keeps pace at 8 Hz and the renderer LOD is healthy. Do not equate tick-ms with frame FPS.

## Targets

| Context | Median frame tick | Notes |
|---------|-------------------|-------|
| **Local dev** (SB-3685) | ≤ **10 ms** | Discrete GPU / M-series Mac; 256×256 starter, traffic lite 64 — aspirational |
| **Starter-city CI gate** | < **25 ms** | Shared `ubuntu-latest`; `TickBudgetTests` starter facts |
| **V1-scale keep-pace** (always) | < **125 ms** | One 8 Hz sim frame — sim must not fall behind the worker period |
| **V1-scale strict** (`CI_STRICT=1`) | < **25 ms** | Historical ubuntu gate; opt-in. Darwin without `CI_STRICT` uses keep-pace only |
| **Scale target** (v1 charter) | ≤ 10 ms @ 8 Hz | 10K households, 5K buildings — characterization logs local target; not hard-gated on Darwin |

Measurement: `Stopwatch` around `SimHost.Tick(0.125)` after warmup; median of 100 samples.

## Results

| Machine | World | HH | Buildings | Median tick (ms) | Traffic mode | Date |
|---------|-------|-----|-----------|------------------|--------------|------|
| CI `ubuntu-latest` | 256 | starter | starter | *(from CI log)* | lite 64 | 2026-07-16 |
| CI `ubuntu-latest` | 128 | starter | starter | *(from CI log)* | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 256 | 128 | 218 | **0.03** | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 128 | 64 | 54 | **0.01** | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 256 after 200 warm ticks | 150 | 218 | **0.02** | lite 64 | 2026-07-16 |
| CI `ubuntu-latest` | 256 | 8,200 | 3,200 | *(from CI log)* | lite 64 | 2026-07-18 |
| Mac arm64 (local dev) | 256 | 8,200 | 3,200 | **3.53** | lite 64 | 2026-07-18 |
| Mac arm64 (cathedral tip) | 256 | 8,200 | 3,200 | **~94** | lite 64 | 2026-08-10 |
| CI `ubuntu-latest` | 128 | starter | starter | *(from CI log)* | full FW | 2026-07-18 |
| Mac arm64 (local dev) | 128 | starter | starter | *(see FullTrafficTests)* | full FW | 2026-07-18 |

> **Starter city:** `SimHost.Init` seeds ~220 buildings and starting population via `SeedStarterCity` / `SeedStartingPopulation`.
>
> **V1-scale city:** `SimHost.SeedV1ScaleCity()` (test/characterization only) fills ≥8K households and ≥3K buildings using `RestoreHouseholds` and the same placement patterns as starter seed. Invoked by `TickBudgetTests.V1Scale_256x256_NearFullPools_MedianFrameTick_UnderCiBudget`. After cathedral systems landed, Mac median rose to ~90+ ms — still under the 125 ms keep-pace soft gate; set `CI_STRICT=1` to re-enable the historical &lt;25 ms assert.

## Traffic modes

| Mode | Config | When |
|------|--------|------|
| **Lite 64** | Default `WasmConfig.TrafficLiteZoneCount` | Browser WASM 256×256 |
| **Lite 128** | `SimHostInitOptions.TrafficLiteZoneCount = 128` | Unity modern profile |
| **Full** | `SimHostInitOptions.UseFullTraffic = true` or Unity env `CITYMAJOR_FULL_TRAFFIC=1` | Desktop opt-in; `FullTrafficTests` in CI |

## Running locally

```bash
# Soft keep-pace gate (default; Darwin-safe)
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj \
  --filter "FullyQualifiedName~TickBudgetTests" \
  -c Release --logger "console;verbosity=detailed"

# Historical hard gate (opt-in)
CI_STRICT=1 dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj \
  --filter "FullyQualifiedName~V1Scale_256x256" \
  -c Release --logger "console;verbosity=detailed"
```

Median ms is printed to test output (`ITestOutputHelper`).

## CI

`Forge.SimCore.Tests` (including `TickBudgetTests`) runs in [`.github/workflows/unity-simcore.yml`](../../.github/workflows/unity-simcore.yml) on every PR and `feat/**` push. Default workflow does **not** set `CI_STRICT`; v1-scale asserts keep-pace (&lt;125 ms) plus pool sizes. Enable `CI_STRICT=1` on a dedicated job when re-baselining tick cost after a perf pass.

## Related

- [SIM_FOUNDATION_CHARTER.md](./SIM_FOUNDATION_CHARTER.md) — WP-D
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — ≥30 FPS render wall-clock; 8 Hz sim; 10K HH / 5K buildings
- Linear [SB-3685](https://linear.app/softblaze/issue/SB-3685) — WASM tick profiling
