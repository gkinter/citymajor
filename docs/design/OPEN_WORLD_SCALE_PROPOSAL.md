# Open World Scale Proposal: CityMajor Expansion

## Executive Summary

CityMajor v1 currently targets a 256x256 single-city map, utilizing chunked R3F rendering and a WASM-based simulation. To transition from a single-city builder to a regional "open world" experience, we must scale our map sizes (512/1024), implement simulation level-of-detail (LOD), and leverage our existing economic systems to support multi-city interactions. This document outlines the architectural roadmap for this expansion.

## 1. Beyond 256: Map Scaling & Streaming

Scaling to 512x512 (262k tiles) or 1024x1024 (1M tiles) requires moving away from loading the entire world into memory and rendering it simultaneously.

*   **Streaming Chunks:** The current web implementation (`web/lib/chunks.ts`) divides the 256x256 map into 64 chunks (32x32 tiles each) and performs frustum culling. For larger maps, we will need to implement true streaming—unloading chunks from WASM memory and WebGL when they are far outside the camera's view, and loading them asynchronously from a local indexedDB cache or server.
*   **Simulation LOD:** Simulating 1 million tiles at the agent/building level is not feasible for browser performance. We will implement Simulation LOD:
    *   **High LOD (Camera focus):** Full building-level Leontief production and agent pathfinding.
    *   **Low LOD (Distant districts):** Aggregate simulation at the chunk or partition level. Production and consumption are calculated mathematically based on chunk-level statistics rather than individual building ticks.

## 2. Multi-City Regions & Trade

The foundation for regional play is already built into the simulation engine. 

*   **Inter-City Trade:** `Forge.Game.Simulation.TradeSystem` already supports trade routes via the `PartnerCityId` field in the `TradeRoute` struct. Currently, `-1` represents the global market, but this is explicitly designed to support point-to-point trade between distinct city entities.
*   **Regional Specialization:** `ProductionChainRegistry` defines complex, multi-tier production chains (e.g., Iron Ore + Coal -> Steel -> Vehicles). Because resources like Coal or Lithium will be geographically constrained on a regional map, cities *must* specialize and trade. One city becomes the mining hub, exporting raw materials to an industrial partner city.

## 3. Procedural Region Generation vs. Hand-Crafted

To populate a massive open world, we will use a hybrid approach:
*   **Procedural Generation:** The macro-region (county/metro level) will be procedurally generated using Simplex noise (already present in `Forge.Engine.Math.SimplexNoise`) to determine biomes, elevation, and resource deposits (oil, rare earth, arable land).
*   **Hand-Crafted Scenarios:** We will support hand-crafted regional maps for specific campaign scenarios or balanced multiplayer maps, defining starting nodes and resource bottlenecks.

## 4. Browser Performance Budget

Running a regional simulation in the browser imposes strict limits:
*   **WASM Memory:** Currently, the engine allocates ~256MB for tile data and state. Scaling to 1024x1024 increases tile data footprint by 16x. We must pack tile data tighter (bitfields) and page inactive regions out of WASM memory to keep the footprint under 500MB (safe limit for mobile/low-end browsers).
*   **Instancing Limits (R3F):** WebGL can comfortably push ~100k-200k instances. A 1024x1024 map has 1M tiles. We will strictly enforce the LOD system (`updateChunkLod` in `chunks.ts`), merging distant 32x32 chunks into single simplified meshes or low-res textures, ensuring draw calls and instance counts remain within budget regardless of world size.

## 5. Phased Rollout

We will approach the open-world expansion in three distinct phases to mitigate risk:

*   **Phase 1 (v1.0) - Single City:** The current baseline. 256x256 map, trading exclusively with the anonymous global market (`PartnerCityId = -1`). Global price events (oil shocks, booms) drive the economy.
*   **Phase 2 (v1.5) - Neighboring NPC Towns:** Introduction of the regional map UI. Players can establish targeted trade routes with simulated NPC towns that have specific supply/demand profiles, utilizing the existing `TradeSystem` contract mechanics.
*   **Phase 3 (v2.0) - Player Cities on Shared Map:** True open world. Players can claim multiple plots on a massive 1024x1024 regional map, building specialized cities that trade with each other. This can be played asynchronously single-player (managing a whole county) or as a multiplayer shared region.