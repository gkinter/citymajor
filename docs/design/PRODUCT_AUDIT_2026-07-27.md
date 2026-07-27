# CityMajor — Product Audit: How Far Is the Game?

**Date:** 2026-07-27
**Scope:** The game as a *product* — what a player can do, what's hollow, what's missing to ship
**Companion:** [`CODEBASE_AUDIT_2026-07-27.md`](CODEBASE_AUDIT_2026-07-27.md) (engineering)
**Baseline:** [`WEB_V1_SCOPE.md`](WEB_V1_SCOPE.md) — 60–90 min Frontier → Industrial arc

---

## Verdict

**The simulation is the strongest part of this project and it is genuinely good. The
*game* wrapped around it barely exists yet.**

You have built a real city simulation — RCI demand curves, Frank–Wolfe traffic assignment,
household-level population modeling, event effects that propagate into crime and fire risk,
service coverage fields, a monthly budget. That is the hard part, and it works.

What is missing is almost everything that makes a simulation into something a player
*finishes*: research that changes numbers, laws that do anything, goals, an ending, and
sound. Three of the four content systems you have authored data for are wired to nothing.

Rough completeness against the locked v1 charter:

| Layer | Done | Note |
|-------|------|------|
| Core simulation | **~75%** | Real and working; gaps are integration, not depth |
| HUD / UI shell | **~85%** | 60+ components, every panel the design calls for |
| Content **authored** | **~80%** | 60 v1 techs, ~37 v1 laws, ~25 v1 events, 199 buildings |
| Content **wired to the sim** | **~25%** | Research effects, laws, and transport are all inert |
| Art | **~30%** | 36 real meshes; the entire civic layer shares one modern mesh |
| Audio | **0%** | None. No music, no ambience, no UI sound |
| Game structure (goals, ending) | **0%** | No objectives, no scenario, no win or lose state |

The honest summary: **you can play with this, but you cannot play it.** There is no reason
to start, no reason to stop, and nothing that tells you that you did well.

---

## What genuinely works

Worth being specific, because this is real engineering and it should not get lost under the
gap list.

**Zone growth is a real demand model.** `ZoneGrowthSystem` computes residential demand from
job availability, immigration pressure, and housing supply; spawn probability is
`demand × desirability × (1 + services × weight)`, with a symmetric decline path when demand
goes negative. Land value recalculates monthly and buildings upgrade off it. Paint a zone
next to jobs and it fills; paint it in the wilderness and it does not. That is the core
city-builder loop, and it is correct.

**Traffic is real traffic.** `WasmTrafficLite` runs Frank–Wolfe equilibrium assignment with
BPR volume-delay on a 64-zone grid, batched off the 8 Hz tick so it never blocks. Most
projects at this stage fake congestion with a heat blur.

**Events actually propagate.** `EventSystem` applies effects through a phase multiplier
(building → peak → subsiding) into happiness, approval, per-tile fire risk, per-tile crime,
and property damage. Events have a lifecycle, not just a toast.

**Population is modeled at household granularity** with cultural DNA, satisfaction, and
job/home assignment — 10,240 household capacity, which meets the charter's 10k target.

**The HUD is close to complete.** Research, economy, law, citizen, budget, population,
herald, minimap, demand overlay, service coverage overlays, traffic overlay, era progress,
onboarding, help. The shell of the game is built.

**The build layer is broad**: 7 zone tiers, 43 civic ploppables across Civic / Education /
Fire / Police / Health / Utilities / Parks, road tiers, and WASM `place_building`.

---

## The hollow layers

These are systems that look finished — data authored, UI built, player-facing — but change
nothing in the simulation. This is the highest-value work in the project, because the cost
is small and the perceived gain is enormous.

### 1. Research unlocks content but has zero balance effect

`ResearchSystem.CompleteTech()` is three lines:

```csharp
internal void CompleteTech(int techId, WorldState state)
{
    state.UnlockTech(techId);          // set a bit
    EurekaBonuses.Remove(techId);      // clear consumed bonus
}
```

The docstring above it says *"unlock it and apply its effects."* It does not apply effects.

All 156 technologies carry an `effects` block in `technologies.json` — `T001 Cobblestone
Paving` declares `{"road_capacity_multiplier": 3}`. Those blocks are parsed into
`TechDefinition.Effects` and then **never read by any system**. The only consumers of
`IsTechUnlocked` anywhere in the sim are: counting techs for era derivation, collecting ids
for the save/snapshot, and a legacy ImGui panel.

So researching Cobblestone Paving unlocks the *ability to place* cobblestone roads, but does
not make roads carry more traffic. Sixty v1 technologies, each with authored numbers, and
none of them touch a single simulation value.

**This is the single highest-leverage fix in the project.** The data exists, the parser
exists, the UI exists. What is missing is an effect-application pass — roughly: on
`CompleteTech`, fold the tech's effects into a modifier set that the relevant systems query.
Doing this turns a tech tree that is currently a shopping list into an actual progression
system.

### 2. Laws are toggles that do nothing

`LawSystem`'s own class docstring is candid: *"Effect application and council voting land in
follow-up work."* `SetActive` flips a bool and increments a counter. Nothing reads it.

70 authored ordinances (~37 in the v1 era range), a Law panel in the HUD, a `set_law_active`
command threaded all the way from React through the worker into WASM — and the end of that
pipe is a boolean nobody queries. The player toggles a law, the UI confirms it, and the city
does not notice.

Same fix shape as research: laws need to contribute to the same modifier set.

### 3. Transport is a toolbar with no sim behind it

Bus and rail placement exist in `TransportToolbar.tsx` as UI with overlay colors, but there
is no `PaintTransit` WASM export and no `transit_paint` worker command. **(tracked)** as
Wave 17 item #2. Players can select transit modes and paint nothing.

### 4. The full traffic system is compiled but unused

`TrafficSystem.cs` (933 lines — mode choice, full assignment) is pulled into the WASM bundle
by the `Forge.Game/Simulation/*.cs` glob, but `WasmSimHost` instantiates `WasmTrafficLite`
instead. It is download weight for a system the game does not run. Either the lite version is
the v1 answer and the full one should be excluded from the glob, or the intent was to grow
into it — worth a decision either way.

---

## The missing game

The hollow layers are things half-built. These are things not started, and they are what
separates "a simulation you can poke" from "a game you play."

### 5. There are no goals, no scenario, and no ending

There is no objective system, no milestone system, no scenario definition, no victory
condition, no fail state. I searched for all of them; the only progression structure in the
entire product is the era gate.

The charter promises **"roughly 60–90 minutes of focused progression."** What actually
exists is: a 5-step tooltip tour (welcome → paint residential → watch demand → open Herald →
open research), and then an open sandbox that never ends. Nothing tells the player what to
aim for, nothing marks achievement, nothing says "you did it."

For a browser game where the player arrives with no manual and no install commitment, this
is the difference between a 4-minute session and a 60-minute one. **This is the biggest
product gap in the project** and, unlike the art backlog, no amount of asset production
fixes it.

Minimum viable version: 5–8 authored objectives per era with visible progress ("reach 400
population", "achieve 60% health coverage", "keep approval above 50% for a year"), a
completion screen at the Industrial era transition, and a fail state for bankruptcy.

### 6. Era transition does not match its own design

`WEB_V1_SCOPE` §3 specifies: *"Era flip when Industrial-era research milestones are met
(e.g. structural steel, coal plant, asphalt roads)."*

The implementation is generic thresholds:

```csharp
new() { Era = 1, Name = "Industrial", MinPopulation = 400, RequiredTechCount = 5 }
```

Population ≥ 400 and **any** 5 techs. Not the designed milestone techs — any five. The one
authored moment of drama in the v1 arc fires on a counter. This also means the Era Progress
panel shows the player a generic gauge rather than the three named technologies the design
intended as the act break.

### 7. There is no audio at all

Zero. No music, no ambient city loop, no UI feedback sounds, no placement confirmation. The
only file matching a sound search is `paint-feedback.ts`, which is visual.

`GAME_FEEL_BIBLE.md` exists in the design folder. City builders are substantially an
ambience genre — the sound of a city growing is a large fraction of why players stay in the
window. Even a minimal pass (one ambient loop per era, placement/demolish/error stingers,
UI clicks) would move perceived quality more than the next 40 building meshes.

### 8. The entire civic layer renders as one modern building

This is the art gap that matters most, and it is a taxonomy problem rather than a production
backlog.

`gltf-catalog.ts:237`:

```ts
if (category === "svc" && "svc_modern_00" in GLTF_CATALOG) {
  return "svc_modern_00";
}
```

Every service building resolves to a single shared mesh. And `deriveEra` returns
`Era.Modern` for all services unconditionally, so services are era-blind by construction.

Concretely: a frontier **well**, a **sheriff's office**, an **elementary school**, and a
**coal power plant** all render as the same modern service block. All 43 buildable
ploppables — the things the player *actively places*, the objects they look at most closely
— share one model from the wrong era.

In a v1 whose entire pitch is a Frontier → Industrial visual arc, the civic layer is
visually outside that arc. The `svc_modern_01`–`07` batch **(tracked)** as Wave 17 #3 adds
variety but does not fix the era-blindness — the taxonomy needs `svc_frontier_*` /
`svc_industrial_*` bands first.

### 9. Art coverage against the team's own target

36 real meshes exist for the v1 eras (26 frontier, 10 industrial) against the checklist's
**"84 more GLBs to reach v1 art minimum"** — roughly 30% of target, **(tracked)** as
SB-3730/SB-3740. Residential, commercial and industrial are the covered categories; civic is
zero (see #8).

---

## Gap to a shippable game

Ordered by player-visible impact per unit of work. The first block is small and transforms
how the game feels; the last block is large and mostly production.

### Block 1 — Make the systems you already built actually matter (highest leverage)

1. **Apply tech effects.** Fold `TechDefinition.Effects` into a modifier set on
   `CompleteTech`; have Traffic/Economy/Services/ZoneGrowth query it. Turns 60 authored
   techs from a checklist into progression. *(Data, parser, and UI all already exist.)*
2. **Apply law effects.** Same modifier set. Turns 37 v1 ordinances from inert toggles into
   the political layer the design describes.
3. **Fix the era gate** to fire on the three designed milestone techs instead of a raw
   count, and surface those three by name in the Era Progress panel.

### Block 2 — Give the player a reason to start and a reason to stop

4. **Objectives system** — 5–8 per era, visible progress, tied to metrics the sim already
   exposes (population, coverage, approval, treasury).
5. **An ending** — a completion screen at the Industrial transition summarising the city.
6. **A fail state** — bankruptcy, with warning escalation. `CrisisWarningModal` already
   exists as a hook.

### Block 3 — Make it feel like a city

7. **Audio pass** — ambient loop per era, placement/demolish/error stingers, UI clicks.
   Highest game-feel return of anything on this list.
8. **Civic art taxonomy** — add `svc_frontier_*` / `svc_industrial_*` bands and stop
   collapsing every service building to `svc_modern_00`.
9. **Wire the transport toolbar** to a real `PaintTransit` export, or hide it until it does
   something.

### Block 4 — Production and polish

10. **Art batch** — the ~84 remaining GLBs, run through the compression pipeline from the
    engineering audit (§3) rather than shipped raw.
11. **Balance pass** — impossible before Block 1, because the numbers that would be balanced
    are currently not connected to anything.

---

## The one thing to take away

Blocks 1 and 2 are, in engineering terms, small — an effect-application pass over data that
is already authored and parsed, plus an objectives system reading metrics the sim already
publishes. They are what convert this from a simulation demo into a game.

The instinct will be to keep pushing on art, because the art gap is the most *visible* one
and it is already tracked with issue numbers. But 84 more building meshes on a game with no
goals, no ending, and no sound produces a prettier tech demo. The same weeks spent on
Blocks 1–3 produce something a player finishes and tells someone about.
