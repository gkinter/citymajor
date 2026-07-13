# Unity Workshop & Blueprint Scope (v2.5)

**Linear:** [SB-4183](https://linear.app/softblaze/issue/SB-4183)–[SB-4186](https://linear.app/softblaze/issue/SB-4186)  
**Status:** Scaffold only — no live Steam UGC in v1 or v1.5.

## Goal

Let players export a **district blueprint slice** from the Unity client and (in v2.5) publish/subscribe via **Steam Workshop**. Web v1 remains browser-only; this is the native Steam EA path.

## CMJR chunk 0x02 — District slice

| Field | Type | Notes |
|-------|------|-------|
| Chunk ID | `byte` | `0x02` (`BlueprintChunkIds.DistrictSlice`) |
| Origin X/Z | `int32` LE | Tile origin on 256×256 map |
| Width / Height | `int32` LE | Slice bounds (stub default 32×32) |
| Payload | TBD | Buildings, zones, roads — writer lands v2.5 |

`BlueprintSliceWriter.WriteHeaderOnly` returns a **32-byte header stub** for editor/UI hooks until the full chunk writer ships.

## v2.5 deliverables (not in this stub)

1. Full CMJR chunk 0x02 writer + reader (district payload)
2. `SteamWorkshopStub` → real Steam UGC API (`PublishBlueprint`, `SubscribeToItem`)
3. In-game browse/subscribe UI; cloud sync with Steam Remote Storage where applicable
4. Moderation + metadata (title, tags, preview thumbnail)

## Current scaffold (2026-07-13)

| File | Role |
|------|------|
| `Save/BlueprintSlice.cs` | Chunk ID, header type, header-only writer |
| `Platform/SteamWorkshopStub.cs` | Log-only publish/subscribe |
| `UI/BlueprintPanelController.cs` | `P` panel — dimensions stub + export button |
| `UI/BlueprintPanel.uxml` | Panel layout |
| `Net/CityShareStub.cs` | Spectator URL (v1.5; separate from Workshop) |

Bootstrap wires `CityMajor_BlueprintUi` in `CityMajorBootstrap.cs`. Help panel documents **P — Blueprint**.

## Out of scope (v2.5 gate)

- Cross-platform Workshop (Epic/GOG)
- In-sim blueprint placement from subscribed items (needs sim contract)
- Paid UGC or sim-affecting mods

## References

- `docs/design/CITY_ECOSYSTEM_VISION.md` §10–11 — phased social/blueprint roadmap
- `docs/design/UNITY_V1_SCOPE.md` — v1 does not include Workshop
