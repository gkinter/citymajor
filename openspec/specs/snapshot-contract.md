# Snapshot Contract (OpenSpec domain spec)

> **Status:** Active — Tier 0 contract documented.  
> **Cathedral pillar:** P7 Client Truth Layer  
> **Canonical field matrix:** [`SIM_SNAPSHOT_V2.md`](../../docs/design/SIM_SNAPSHOT_V2.md)  
> **Worker protocol:** [`WASM_SIM_BRIDGE.md`](../../docs/design/WASM_SIM_BRIDGE.md)

## Scope

`SimSnapshot` / `SimSnapshotDto` / `CitySimState` field ownership, cadence, Unity vs web parity, characterization test hooks.

## Implemented requirements

- **P7.1** — `SIM_SNAPSHOT_V2.md` documents tip exports (`8f57614`): Event*Mult, AbandonedBuildingCount, fire/EMS, delivery delay, HH L2 sample, friction corridors, mode share, council seats, and known surface gaps.
- **P7.2** — Cathedral characterization 0-skip gate.
- **P7.4** — Gap matrix CI: `scripts/verify-sim-snapshot-v2.py` (doc→code + code→doc) on `unity-simcore.yml`; local `pnpm verify:sim-snapshot-v2`.
- **P7.5** — Unity Cathedral HUD Event*Mult + WASM DTO export.
- **Law\*Mult WASM save/load** — `SimSnapshotDto` exports Law traffic/construction/spawn Mults; `ApplySnapshotDto` restores them (parity with Event\*Mult).

## Target milestones

P7.3 per [`CATHEDRAL_PROGRAM.md`](../../docs/design/CATHEDRAL_PROGRAM.md) §4 (Play verify human gate).
