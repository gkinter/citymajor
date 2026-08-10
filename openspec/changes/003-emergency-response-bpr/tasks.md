# Tasks: 003-emergency-response-bpr

> Documents Cathedral **P5.2** emergency response × BPR already landed on
> `feat/cathedral-p5-emergency-response-2026-08-10`. See
> [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 P5
> and [`CATHEDRAL_P5_SERVICES.md`](../../../docs/design/CATHEDRAL_P5_SERVICES.md).

## EmergencyResponseTime helper (SimCore)

- [x] Add `Forge.SimCore.EmergencyResponseTime` — path cost / speed + crew readiness
- [x] Nearest-station search by `ServiceFlags` (fire / health)
- [x] Dijkstra on `RoadGraph` preferring BPR `edgeTravelTimes[]`
- [x] Free-flow edge costs when travel-time array absent
- [x] Euclidean tile-distance fallback when graph path unavailable
- [x] Defaults: 2 tiles/min vehicle speed, 1 min crew readiness, 30 min if no station

## ServiceSystem + traffic coupling

- [x] `CalculateFireResponseTime` forwards to `EmergencyResponseTime`
- [x] Pass `state.RoadEdgeTravelTimes` so congestion raises minutes
- [x] `Forge.Game` references `Forge.SimCore` for the shared helper

## Docs + characterization

- [x] `CATHEDRAL_P5_SERVICES.md` §4.3 response formula + P5.2 live row
- [x] `CATHEDRAL_PROGRAM.md` mark **P5.2** ✅
- [x] `EmergencyResponseTime_UsesDistancePlusTraffic` — near &lt; far; congested &gt; free; facade parity

## Verification

```bash
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj \
  --filter "FullyQualifiedName~EmergencyResponseTime_UsesDistancePlusTraffic"
```

## Merge gate

- [x] Characterization test passes on integration tip with P5.2 commit
- [x] No change to P5.1 coverage fraction contract
- [ ] OpenSpec delta archived to `openspec/specs/services.md` on merge
