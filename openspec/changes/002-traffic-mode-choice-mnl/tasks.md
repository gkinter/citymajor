# Tasks: 002-traffic-mode-choice-mnl

> Documents Cathedral **P4** traffic depth already landed on integration
> (`feat/unity-port-plan-2026-07-12`). See [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 P4.

## BPR foundation (supports P4 commute truth)

- [x] Add `Forge.SimCore.TrafficBpr` — α=0.15, β=4 travel time
- [x] `TrafficBpr.EdgeCapacity` from P1.2 tier table × lanes × law mult
- [x] Wire `TrafficSystem` + `WasmTrafficLite` to shared BPR
- [x] Characterization: pinned flow/capacity cases (`CathedralTrafficBprTests`)

## Multi-iteration Frank-Wolfe

- [x] Raise `WasmConfig.TrafficLiteFrankWolfeIterations` to 3
- [x] Relative-gap early stop at 1% (`TrafficLiteFrankWolfeConvergenceThreshold`)
- [x] `TrafficBpr.FrankWolfeRelativeGap` (TSTT − SPTT) / TSTT
- [x] Export `edgeVolumes[]` + `travelTimes[]` on roadGraph snapshot / WASM status
- [x] Tests: multi-iter assignment + snapshot travel-time export

## P4.5 — Mode-choice stub (MNL)

- [x] Replace flat 65% car with 3-mode MNL (car / transit / walk)
- [x] Distance-based utilities (ASC + β_time × mode travel time)
- [x] Cap walk beyond max walk minutes (share → 0 on long trips)
- [x] Expose `CarModeShare` / `TransitModeShare` / `WalkModeShare` on lite traffic
- [x] Apply mode split to O-D car flow before Frank-Wolfe assignment
- [x] Preserve P4.1 home/work O-D construction (no gravity regression)
- [x] Characterization: `CathedralModeChoiceTests` (sum-to-1, mid-distance car band, short vs long walk)

## Verification

```bash
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj \
  --filter "FullyQualifiedName~CathedralTrafficBpr|FullyQualifiedName~CathedralModeChoice"
```

## Merge gate

- [x] Characterization tests pass on integration tip
- [x] No change to P4.1 building-pair O-D builder contract
- [ ] OpenSpec delta archived to `openspec/specs/population.md` on merge
