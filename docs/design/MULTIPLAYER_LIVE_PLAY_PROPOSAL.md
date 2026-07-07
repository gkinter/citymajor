# CityMajor LIVE: Multiplayer Play Proposal

This document outlines the architecture and phased roadmap for introducing multiplayer capabilities to CityMajor. Building upon the single-player WASM simulation (v1), these options range from asynchronous collaboration to a fully simulated regional open world.

---

## 1. Async Co-op (Shared City)
*Lowest Lift*

**Concept:** Players share a single city but play asynchronously. Think "shift-based" mayor duties or turn-based zoning. Player A zones residential and sets up basic infrastructure; Player B logs in later to manage the industrial sector and trade.
- **Tech Stack:** Supabase (Postgres + Storage).
- **Sync Model:** State Snapshots. The city state is serialized (e.g., zstd-compressed binary) and saved to the cloud. A locking mechanism ensures only one player edits the city at a time. When a player logs in, they download the latest snapshot.
- **Cheating Concerns:** Low. Since it is cooperative and asynchronous, cheating only affects the shared city. Basic server-side integrity checks on save files — schema validation plus checksums for *corruption detection*, which is **not** the same as anti-cheat validation — prevent blatant corruption, but strict anti-cheat is unnecessary.
- **Monetization Fit:** Premium feature for "City Server" hosting (e.g., pay to host a persistent async city for your friend group), or cosmetic items for mayors.

---

## 2. Live Co-op (Same City)
*Medium Lift*

**Concept:** 2-4 players actively building and managing the same city simultaneously. Players can divide responsibilities—one manages transport and zoning, while another handles the economy and policies.
- **Tech Stack:** Supabase Realtime or Cloudflare Durable Objects for fast, low-latency state coordination and presence.
- **Sync Model:** Hybrid snapshot sync + input forwarding. **Host-authoritative** (not client-authoritative): the host runs the canonical simulation loop and clock, and broadcasts state snapshots. Clients forward their inputs (e.g. zoning commands, road placement) to the host and never mutate world state locally without host confirmation. CRC32 subsystem checksums are used **only for desync detection** between host and clients — they are *not* a validation mechanism (an unauthorized client can always compute a matching checksum for whatever state it produces). Real anti-tamper still requires host-side rule checks on every forwarded input.
- **Cheating Concerns:** Medium. Requires host-authoritative validation. Since it's cooperative, strict anti-cheat isn't critical, but preventing desyncs and griefing (if public matchmaking is allowed) is important.
- **Monetization Fit:** "Live Server" passes, or free for small groups with paid cosmetic expansions and unique multiplayer-only building skins.

---

## 3. Regional Open World
*High Lift*

**Concept:** Players manage adjacent city tiles in a persistent region. Cities are separate (each player controls their own) but connected by trade routes, shared infrastructure, migration, and regional events. Trade negotiations happen in real-time between players.
- **Tech Stack:** Dedicated C# Simulation Server (`Forge.Server`) running headlessly. Cloudflare Durable Objects for regional state coordination (trade, migration flows, global events).
- **Sync Model:** Lockstep or frequent state snapshots for the regional layer. Individual cities run locally on the client, but regional data (trade agreements, migration, pollution) syncs continuously with the authoritative server.
- **Cheating Concerns:** High. Competitive elements (trade, resources, migration) require strict server-authoritative validation to prevent economic exploits and memory injection.
- **Monetization Fit:** MMO-style subscription, battle passes for "Seasons" (e.g., a 3-month regional competition), or premium regions with unique biomes and rulesets.

---

## 4. Spectator / Mayor Elections
*Social Layer*

**Concept:** A meta-game social layer where players can spectate live cities. Communities can vote on "Mayors" for persistent regional cities, or vote on policy changes in a Twitch-plays-style integration.
- **Tech Stack:** Supabase Realtime for voting, chat, and presence. Read-only state replication for spectators.
- **Sync Model:** Broadcast state snapshots (high latency tolerance). Spectators receive periodic updates to render the city without needing to run the full simulation loop.
- **Cheating Concerns:** Low for spectating. Medium for voting (requires sybil protection and authentication to prevent botting elections).
- **Monetization Fit:** Tipping, spectator passes, engagement-driven ad revenue, or premium voting power (e.g., "campaign contributions").

---

## Phased Roadmap

To manage technical risk and incrementally deliver value, multiplayer will be rolled out in three phases:

1. **v1.5: Async Invites**
   - Implement cloud saves and basic session locking.
   - Allow players to invite friends to asynchronously manage a shared city.
   - Establishes the Supabase backend infrastructure.

2. **v2.0: Live Co-op**
   - Introduce real-time input forwarding and hybrid snapshot sync.
   - Support 2-4 players in the same city.
   - Implement host migration and desync recovery.

3. **v3.0: Regional Open World**
   - Deploy headless `Forge.Server` instances.
   - Connect multiple cities via trade, migration, and shared infrastructure.
   - Introduce the competitive/cooperative regional economy and global events.
