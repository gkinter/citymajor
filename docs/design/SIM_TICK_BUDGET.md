# SimHost tick budget (WP-D)

Characterization of native `Forge.SimCore.SimHost` wall-clock cost per 8 Hz frame (`Tick(0.125)`).

## Targets

| Context | Median frame tick | Notes |
|---------|-------------------|-------|
| **Local dev** (SB-3685) | ≤ **10 ms** | Discrete GPU / M-series Mac; 256×256, traffic lite 64 zones |
| **CI gate** (`TickBudgetTests`) | < **25 ms** | Shared `ubuntu-latest`; generous headroom for runner variance |
| **Scale target** (v1 charter) | ≤ 10 ms @ 8 Hz | 10K households, 5K buildings — not yet characterized in CI |

Measurement: `Stopwatch` around `SimHost.Tick(0.125)` after warmup; median of 100 samples.

## Results

| Machine | World | HH | Buildings | Median tick (ms) | Traffic mode | Date |
|---------|-------|-----|-----------|------------------|--------------|------|
| CI `ubuntu-latest` | 256 | starter | starter | *(from CI log)* | lite 64 | 2026-07-16 |
| CI `ubuntu-latest` | 128 | starter | starter | *(from CI log)* | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 256 | 128 | 218 | **0.03** | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 128 | 64 | 54 | **0.01** | lite 64 | 2026-07-16 |
| Mac arm64 (local dev) | 256 after 200 warm ticks | 150 | 218 | **0.02** | lite 64 | 2026-07-16 |

> **Starter city:** `SimHost.Init` seeds ~220 buildings and starting population via `SeedStarterCity` / `SeedStartingPopulation`. Tests do not yet force 10K HH — see `TickBudgetTests.StarterCity_AfterWarmSim_ReportsPopulationAndTickBudget` for post-warm counts.

## Traffic modes

| Mode | Config | When |
|------|--------|------|
| **Lite 64** | Default `WasmConfig.TrafficLiteZoneCount` | Browser WASM 256×256 |
| **Lite 128** | `SimHostInitOptions.TrafficLiteZoneCount = 128` | Unity modern profile |
| **Full** | `SimHostInitOptions.UseFullTraffic = true` | Opt-in MNL + Frank-Wolfe; not in default CI budget tests |

## Running locally

```bash
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj \
  --filter "FullyQualifiedName~TickBudgetTests" \
  -c Release --logger "console;verbosity=detailed"
```

Median ms is printed to test output (`ITestOutputHelper`).

## CI

`Forge.SimCore.Tests` (including `TickBudgetTests`) runs in [`.github/workflows/unity-simcore.yml`](../../.github/workflows/unity-simcore.yml) on every PR and `feat/**` push.

## Related

- [SIM_FOUNDATION_CHARTER.md](./SIM_FOUNDATION_CHARTER.md) — WP-D
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — 8 Hz, 10K HH, 5K buildings
- Linear [SB-3685](https://linear.app/softblaze/issue/SB-3685) — WASM tick profiling
