# CityMajor Live Services Architecture

This document outlines the evolution of CityMajor's live services and multiplayer capabilities from the current local spike to a fully authoritative cooperative architecture.

## Evolution Phases

### v1 Current: Standalone Spike
**Architecture:** Client-authoritative WASM + Local JSON saves.
- **Simulation:** Runs entirely in the browser using `Forge.SimWasm` compiled via .NET 8.
- **State Management:** Procedural or WASM-driven state is kept in-memory within the Web Worker.
- **Persistence:** Uses a file-backed JSON store (`.data/saves.json` and `.data/tiers.json`) mounted in the Next.js API. Save limits are gated by local stubs (Free vs Founder Pass).
- **Security:** The client is fully authoritative. The simulation state is open to client-side manipulation.

### v1.5: Cloud Saves & Asynchronous Multiplayer
**Architecture:** Client-authoritative + Managed Cloud Persistence.
- **Saves:** Transition from local JSON to cloud object storage (Cloudflare R2 or AWS S3) via signed upload/download URLs to minimize compute bandwidth on the main API.
- **Leaderboards & Snapshots:** Client periodically pushes authenticated sim snapshots and city metrics to the backend.
- **Async Trade Offers:** Players can propose resource trades asynchronously via standard REST endpoints backed by a relational database.
- **Security:** Still client-authoritative. Suitable for casual async interactions, but highly vulnerable to client memory hacking for competitive ladder metrics.

### v2: Dedicated Sim Tick Server
**Architecture:** Server-authoritative Headless Sim.
- **Simulation:** `Forge.SimCore` decoupled from WASM rendering, running as a headless .NET daemon on `docker-fleet`.
- **Tick Engine:** The server is the absolute source of truth, processing simulation ticks at fixed intervals.
- **Spectator Stream:** Read-only WebSocket streams broadcasting delta-compressed state snapshots for client viewers (e.g., viewing another player's city live without simulating it locally).
- **Security:** The golden rule of "never trust client sim for competitive features" is fully realized here. The client acts only as a dumb viewer and input relayer.

### v2 Co-op: Room-Based Authoritative Multiplayer
**Architecture:** Distributed Room-based Authoritative Servers.
- **Rooms:** Players join dedicated server sessions representing a single city or region instance.
- **Player Roles:** Granular control splitting responsibilities:
  - **Mayor:** Global budget approvals, macro-level planning.
  - **Zoning:** Placing residential/commercial/industrial zones and defining density.
  - **Finance:** Managing tax brackets, bonds, and micro-economy policies.
- **Networking:** High-frequency bi-directional WebSocket (or WebTransport) RPC handling inputs and distributing state deltas to multiple connected clients simultaneously.

## Identity & Authentication (Auth)

A robust player identity is crucial as we move to v1.5 and beyond.

**Supabase vs Clerk**
- **Supabase Auth:**
  - *Pros:* Native integration with PostgreSQL Row Level Security (RLS). Deep integration if we host relational game data (cities, trades, users) on Supabase. Open-source, easily self-hostable.
  - *Cons:* Slightly more boilerplate for edge-case OAuth flows and UI components compared to Clerk.
- **Clerk:**
  - *Pros:* Drop-in React components. Flawless Next.js integration. Extremely polished B2C onboarding experience.
  - *Cons:* Premium pricing scales steeply. Requires JWT syncing / webhooks to keep the backend database synchronized with identity.

*Decision:* For a game with heavy database requirements and complex session-based authorization (rooms, RLS on save files, trade ownership), **Supabase** often proves more technically cohesive and cost-effective, despite Clerk's frontend superiority.

## Security Posture

**Golden Rule: Never trust client sim for competitive features.**
- In v1/v1.5, any submitted leaderboard score, save state, or trade offer must be treated as potentially tainted. If leaderboards offer real rewards or seasonal resets, competitive integrity is impossible if the simulation ticks on the client.
- In v2, the client sends *Inputs/Intents* (e.g., "Zone X at coordinate Y,Z"), not *Results* (e.g., "I earned 10,000 credits"). The server evaluates if the input is valid, processes the tick, and sends back the resulting state.

## Cost Model (Rough estimates per 1K DAU)

As we scale up the backend to handle simulation state, infrastructure costs change dramatically.

- **v1.5 (Cloud Saves & Async):**
  - Next.js Edge APIs: ~$5-$10
  - Object Storage (R2): ~$5 (Save blobs are usually small, bandwidth is cheap on CF)
  - Postgres DB (Supabase): ~$25 (standard compute)
  - **Total:** ~$40 / mo / 1K DAU

- **v2 (Headless Tick Servers + Websockets):**
  - Dedicated Compute (`docker-fleet` nodes for `Forge.SimCore`): ~$100-$150. A C# headless sim is CPU/RAM intensive compared to a standard web API. Room-based simulation limits density per core.
  - Egress/Bandwidth (WebSockets state streams): ~$20-$50 depending on tick rate and delta compression.
  - Database & Cache (Redis for fast state sync): ~$50
  - **Total:** ~$170 - $250 / mo / 1K DAU
