# CityMajor Web Save Format

> **Status:** Active spec (GAP_AUDIT #6, 2026-07-04)  
> **Scope:** Browser v1 / v1.5 cloud persistence — unifies `SIMULATION_ARCHITECTURE` §17 CMJR binary, `/api/saves` JSON envelope, tier limits, versioning, migration, and conflict resolution.  
> **Supersedes:** Fragmented references in `AGENT_01_CORE_ENGINE.md` (FlatBuffers + Zstd, Godot-era) and `docs/research/14-ux-systems.md` (`IOAK` magic header). Those remain historical; **CMJR** is canonical.

---

## 1. Design goals

| Goal | Constraint |
|------|------------|
| Fast save/load in browser | Target &lt; 500 ms round-trip for 256×256 / ~5k buildings at v1 scale |
| Tier-gated slot limits | Free: **3** slots; Founder Pass: **20** slots |
| Forward-compatible evolution | Version header + step migrations; never silently corrupt |
| Cloud-ready | Metadata in API/DB; blob in object storage (R2) at v1.5 |
| WASM parity | Canonical state is sim binary; JSON snapshot is a v1 transport shortcut |

---

## 2. Architecture — three layers

```
┌─────────────────────────────────────────────────────────────────┐
│  Layer A — Cloud envelope (JSON, API + DB)                      │
│  id, name, tier, revision, blobUrl, header summary, timestamps  │
├─────────────────────────────────────────────────────────────────┤
│  Layer B — CMJR binary blob (canonical sim state)               │
│  256-byte header + tagged chunks + compression                  │
├─────────────────────────────────────────────────────────────────┤
│  Layer C — Render snapshot (JSON, optional cache)               │
│  SimSnapshot for 3D instancing; subset of full sim state        │
└─────────────────────────────────────────────────────────────────┘
```

**v1 spike (current):** Layer A is inlined in `.data/saves.json`; Layer B is **not** written yet — `payload` holds Layer C (`SimSnapshot` JSON) only.

**v1.5 cloud:** Layer A in Postgres (Supabase); Layer B in R2 via signed PUT/GET; Layer C generated on load from WASM or cached alongside blob.

---

## 3. CMJR binary format (canonical)

Source of truth: [`SIMULATION_ARCHITECTURE.md`](./SIMULATION_ARCHITECTURE.md) §17. Web v1 uses the same header and chunk tagging; scale targets are reduced for 256×256 maps.

### 3.1 File layout

```
[SaveFileHeader 256 bytes]
[Compressed payload: tagged chunks]
```

Compression: **Brotli quality 4** (desktop spec). Web v1 may use **zstd level 3** if WASM Brotli is unavailable — `compression` field in header distinguishes (`0` = none, `1` = brotli, `2` = zstd).

### 3.2 Header (256 bytes, little-endian)

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| 0 | `magic` | `uint32` | `0x434D4A52` — ASCII **"CMJR"** (CityMajor) |
| 4 | `formatVersion` | `uint32` | Save **format** version (see §6) |
| 8 | `checksum` | `uint32` | CRC32 of compressed payload (after header) |
| 12 | `uncompressedSize` | `uint32` | Decompressed chunk stream size |
| 16 | `compressedSize` | `uint32` | Byte length of compressed payload |
| 20 | `compression` | `uint8` | `0` none / `1` brotli / `2` zstd |
| 21 | `snapshotSchemaVersion` | `uint16` | Layer C JSON schema generation (see §6.2) |
| 23 | `worldSize` | `uint16` | Grid edge length (v1: **256**) |
| 25 | `flags` | `uint16` | Bit flags (autosave, multiplayer, …) |
| 27 | `saveTimestamp` | `uint64` | Unix epoch seconds (real-world save time) |
| 35 | `gameTick` | `uint32` | Sim tick at save |
| 39 | `population` | `uint32` | For save UI |
| 43 | `cityFunds` | `int32` | For save UI |
| 47 | `era` | `uint8` | Era index (0 = Frontier) |
| 48 | `cityName` | `byte[64]` | UTF-8, null-padded |
| 112 | `contentHash` | `byte[32]` | SHA-256 of uncompressed chunk stream (conflict detection) |
| 144 | `buildId` | `byte[16]` | App build / sim DLL hash (optional) |
| 160 | `_reserved` | `byte[96]` | Zero-filled |

**Note:** Legacy engine `SaveManager.cs` uses magic `FORG` and LZ4 — not compatible. Importers must reject unknown magic.

### 3.3 Chunk stream (after decompression)

Tagged chunks, order-independent on load (same as desktop):

| `ChunkId` | Content | v1 web subset |
|-----------|---------|---------------|
| `0x01` Tiles | SoA tile arrays | Required |
| `0x02` Buildings | Building pool | Required |
| `0x03` Households | Household pool | Required when pop &gt; 0 |
| `0x04` Roads | Road graph CSR | Required |
| `0x05` Economy | Budget, demand, taxes | Required |
| `0x06` Laws | Active ordinances | Optional v1 |
| `0x07` Tech | Research progress | Optional v1 |
| `0x08` Events | Active event queue | Optional v1 |
| `0x09` RNG | Deterministic replay state | Optional v1 |
| `0xFF` End | Marker | Required |

Each chunk: `uint8 id` + `uint32 len` + `len` bytes payload.

### 3.4 Size budget (web v1)

| Component | Raw (est.) | Compressed (est.) |
|-----------|------------|-------------------|
| Tiles 256×256 | ~1.2 MB | ~200 KB |
| Buildings ~5k | ~300 KB | ~150 KB |
| Households ~10k | ~600 KB | ~400 KB |
| Roads + economy | ~200 KB | ~100 KB |
| **Total** | **~2.3 MB** | **~0.8–1.2 MB** |

Targets: save &lt; 500 ms, load &lt; 1 s on mid-tier laptop (async worker + streaming decompress).

---

## 4. Layer C — render snapshot JSON (`SimSnapshot`)

Used by `SaveLoadControls`, WASM `GetRenderSnapshot()`, and `sim-bridge.ts`. This is a **lossy view** for rendering and HUD — not a substitute for CMJR on cloud v1.5.

### 4.1 Schema (`snapshotSchemaVersion: 1`)

```typescript
type SimSnapshot = {
  tick: number;
  population: number;
  householdCount?: number;
  cityFunds: number;
  era: number; // 0–4

  buildings: {
    id: number;
    typeId: number;
    tileX: number;
    tileZ: number;
    level: number;
    state: number;
    condition: number;
  }[];

  zones?: { tileX: number; tileZ: number; zoneType: number }[];
  roads?: { tileX: number; tileZ: number; roadFlags: number }[];

  // Planned v2 fields (omit until WASM exports them):
  // residentialDemand?, commercialDemand?, industrialDemand?
  // activeEvents?, researchCompleted?
};
```

C# mirror: `Forge.SimWasm.SimSnapshotDto` (`WasmSimHost.cs`).

### 4.2 v1 limitation

**Load today restores React/canvas state only** (`cityDataFromSnapshot`). WASM sim is **not** rewound from snapshot — full restore requires Layer B `LoadFromCmjr()` (planned). Until then, saves preserve visuals and summary stats, not deep sim state (demand, events, research).

---

## 5. Cloud API envelope

Implementation: `web/lib/save-store.ts`, `web/app/api/saves/route.ts`.

### 5.1 `SaveSlot` (list + create response)

```typescript
{
  id: string;          // UUID v4
  name: string;        // 1–64 chars, player-chosen
  createdAt: string;   // ISO 8601 UTC
  updatedAt: string;   // ISO 8601 UTC
  revision: number;    // v1.5+ optimistic concurrency (default 1 in v1 stub)
  formatVersion: number;   // CMJR formatVersion (0 = JSON-only stub)
  snapshotSchemaVersion: number; // Layer C version
  summary: {           // denormalized for list UI (no blob fetch)
    tick: number;
    population: number;
    cityFunds: number;
    era: number;
  };
  payload?: Record<string, unknown>; // v1 stub: inline SimSnapshot JSON
  blobKey?: string;    // v1.5: R2 object key (`saves/{userId}/{id}.cmjr`)
  blobBytes?: number;  // v1.5: compressed size
  thumbnailKey?: string; // v1.5 optional PNG in R2
}
```

Zod schemas: `SaveSlotSchema`, `CreateSaveBodySchema` in `save-store.ts`; client mirrors in `saves-client.ts`.

### 5.2 Endpoints

| Method | Path | Auth | Body | Response |
|--------|------|------|------|----------|
| `GET` | `/api/saves` | HMAC cookie `citymajor_uid` (see `user-identity.ts`) | — | `{ saves, count, maxSlots, tier }` |
| `POST` | `/api/saves` | same | `{ name, payload? }` | `{ save, count, maxSlots, tier }` or `403` slot full |
| `GET` | `/api/saves/:id` | v1.5 | — | `SaveSlot` + signed `downloadUrl` |
| `PUT` | `/api/saves/:id` | v1.5 | `{ name?, revision, blob upload complete }` | updated slot or `409` |
| `DELETE` | `/api/saves/:id` | v1.5 | — | `204` |

**v1 stub gaps:** No `GET/:id`, `PUT`, or `DELETE` yet. Tier resolved via `resolve-tier.ts` — production: Stripe webhook → `tier-store.ts` keyed by signed `citymajor_uid`; dev: mock `citymajor_tier` cookie or `X-CityMajor-Tier` header. See [SB-3693_AUTH_ENTITLEMENTS_GAP.md](./SB-3693_AUTH_ENTITLEMENTS_GAP.md).

### 5.3 Create flow (v1.5 target)

```
Client                          API                         R2
  │── POST /api/saves ──────────►│ mint id, revision=1       │
  │◄─ { uploadUrl, save } ───────│                           │
  │── PUT blob (CMJR) ──────────────────────────────────────►│
  │── POST /api/saves/:id/complete ─►│ verify size, checksum  │
  │◄─ { save } ──────────────────│                           │
```

---

## 6. Versioning

Two independent version numbers prevent JSON/UI drift from binary layout changes.

### 6.1 `formatVersion` (CMJR)

| Version | Scope |
|---------|-------|
| **0** | JSON-only stub (no CMJR blob) |
| **1** | Web v1: 256×256, chunks 0x01–0x05 required, zstd or brotli |
| **2** | + laws, tech, events chunks (planned) |
| **3** | + RNG / replay chunk (multiplayer prep) |

**Policy:** Loaders accept `formatVersion <= CURRENT`. Reject newer with user-facing “update required”. Run migrations sequentially `N → N+1`.

### 6.2 `snapshotSchemaVersion` (Layer C JSON)

| Version | Changes |
|---------|---------|
| **1** | Initial: tick, pop, funds, era, buildings, zones, roads |
| **2** | + RCI demand fields |
| **3** | + activeEvents, researchCompleted |

JSON consumers use `safeParse` — unknown fields ignored; missing required fields → “incompatible save” UI.

### 6.3 App `buildId`

16-byte hash in CMJR header ties saves to sim WASM build. Mismatch → warn + attempt load; hard fail only if chunk layout changed in same `formatVersion` (should not happen).

---

## 7. Migration

### 7.1 Principles

1. **Never rewrite cloud blobs in place** — write `saves/{id}.v{N}.cmjr`, update pointer, garbage-collect after 30 days.
2. **One hop at a time** — `migrate_1_to_2()`, `migrate_2_to_3()`, each unit-tested with golden files.
3. **Lazy migration** — transform on first load after app update; re-save emits latest `formatVersion`.
4. **JSON stub migration** — `formatVersion: 0` saves upgraded by deserializing `payload` into WASM, capturing CMJR on next save.

### 7.2 Example: v0 JSON → v1 CMJR

```
1. Parse SaveSlot.payload as SimSnapshot (schema v1).
2. WASM ApplySnapshot (partial) + capture full WorldState.
3. Serialize CMJR formatVersion=1.
4. Upload blob; set formatVersion=1, clear inline payload.
```

### 7.3 Desktop → web

Full desktop saves (500K pop, 1M tiles) are **out of scope** for web v1. Import may be offered later with map downscale — not in v1 charter.

---

## 8. Tier limits & entitlements

Source: `web/lib/entitlements.ts`.

| Tier | `maxSaveSlots` | `maxNarrativeEventsPerDay` | `llmEnabled` |
|------|----------------|----------------------------|--------------|
| `free` | **3** | 10 | false |
| `founder_pass` | **20** | unlimited | true |

**Enforcement:**

- `createSave()` returns `403` when `count >= maxSlots` (message includes tier + upgrade hint).
- `GET /api/saves` returns `maxSlots` so UI shows `slots 2/3` (`SaveLoadControls.tsx`).
- Downgrade (Founder → free): **grandfather** existing saves; block **new** creates until `count <= 3`. No auto-delete.

**User identity (v1):** HMAC-signed HTTP-only cookie `citymajor_uid` (`web/lib/user-identity.ts`); routes call `ensureUserId()` to mint/verify. **v1.5:** Supabase `auth.uid()` with RLS `user_id = auth.uid()` on `save_slots` table; optional merge from anonymous UUID on first login.

---

## 9. Conflict resolution

### 9.1 Single-player cloud saves — optimistic concurrency

Each `SaveSlot` carries monotonic `revision` (starts at 1).

```
PUT /api/saves/:id
  If-Match: <revision>
  Body: { name?, blobComplete: true }
```

| Result | Behavior |
|--------|----------|
| `revision` matches | Accept, increment revision, update `updatedAt` |
| `revision` stale | `409 Conflict` + `{ serverSave, clientRevision, serverRevision }` |
| Client receives 409 | Show dialog: **Keep mine** / **Keep cloud** / **Save as copy** |

**Keep mine:** force PUT with `If-Match: *` (Founder only) or new slot.  
**Keep cloud:** discard local, download blob.  
**Save as copy:** `POST /api/saves` with new name (respects slot limit).

Checksum: compare `contentHash` in CMJR header before accepting overwrite.

### 9.2 Async co-op (v1.5+, optional)

Per [`MULTIPLAYER_LIVE_PLAY_PROPOSAL.md`](./MULTIPLAYER_LIVE_PLAY_PROPOSAL.md):

| Field | Purpose |
|-------|---------|
| `lockedBy` | User id holding edit lease |
| `lockExpiresAt` | ISO 8601; auto-release after 15 min idle |
| `sharedWith` | UUID[] of invited players |

Only lock holder may `PUT` blob. Others get `423 Locked` with `lockedBy` display name. No CRDT — **last complete save wins** within lock window.

### 9.3 v1 stub

Local `.data/saves.json` — single process, no concurrency. No revision checking.

---

## 10. Security posture

Per [`LIVE_SERVICES_ARCHITECTURE.md`](./LIVE_SERVICES_ARCHITECTURE.md): client-authoritative in v1/v1.5.

- Validate CMJR magic, checksum, and size caps (reject &gt; 5 MB compressed).
- RLS: users read/write only their rows; shared cities via `shared_with` policy.
- Signed URLs: 15 min TTL, single-use upload tokens.
- Treat saves as untrusted for leaderboards; checksum logged for abuse review.

---

## 11. Implementation map

| Concern | File(s) |
|---------|---------|
| API routes | `web/app/api/saves/route.ts` |
| Store + Zod | `web/lib/save-store.ts` |
| Client types | `web/lib/saves-client.ts` |
| Tier limits | `web/lib/entitlements.ts`, `web/lib/resolve-tier.ts` |
| Save UI | `web/components/city/SaveLoadControls.tsx` |
| Snapshot type | `web/lib/sim-bridge.ts` |
| WASM export | `src/Forge.SimWasm/WasmSimHost.cs` (`SimSnapshotDto`) |
| Binary spec (desktop) | `docs/design/SIMULATION_ARCHITECTURE.md` §17 |
| Legacy binary | `src/Forge.Engine/IO/SaveManager.cs` (`FORG` — do not use for web) |

### 11.1 Planned work (v1 → v1.5)

- [ ] Add `revision`, `summary`, `formatVersion` to `SaveSlotSchema`
- [ ] WASM `LoadFromSnapshot` / `ExportCmjr` exports
- [ ] `GET/PUT/DELETE /api/saves/:id`
- [ ] R2 signed upload + Supabase `save_slots` table
- [ ] Conflict dialog in `SaveLoadControls`
- [ ] Autosave slot (`flags.autosave` in CMJR header)

---

## 12. Related documents

- [GAP_AUDIT_DESIGN_DOCS.md](./GAP_AUDIT_DESIGN_DOCS.md) — item #6 requested this spec
- [LIVE_SERVICES_ARCHITECTURE.md](./LIVE_SERVICES_ARCHITECTURE.md) — v1 / v1.5 / v2 persistence phases
- [MULTIPLAYER_LIVE_PLAY_PROPOSAL.md](./MULTIPLAYER_LIVE_PLAY_PROPOSAL.md) — async lock model
- [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) — cloud save v1 milestone
- [SIMULATION_ARCHITECTURE.md](./SIMULATION_ARCHITECTURE.md) §17 — CMJR chunk reference (desktop scale)
