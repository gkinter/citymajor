# Unity Play Mode Smoke

**Fast gate** before deeper [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) (SB-4176).  
**Product surface:** Unity Editor / player — not browser `/play`.  
**Scene:** `unity/CityMajor.Unity/Assets/Scenes/Play.unity`

**Editor menu:** **CityMajor → Smoke → …**

| Menu | Action |
|------|--------|
| **Open Play Scene** | Loads `Play.unity` |
| **Open Play Mode Smoke Checklist** | Opens this doc |
| **Log Smoke Steps** | Prints the 7 steps below to Console |
| **Run Smoke Prep** | Open scene + **U3.6 Play gate batch** + log steps |

**Also:** **CityMajor → Run Play Gate Batch Checks (U3.6)** · CLI `./scripts/verify-unity-play-gate.sh`  
Entering Play is **cancelled** if SimCore DLL / SimHost type / bootstrap / build-settings blockers fail (toggle skip under **CityMajor → Play Gate**).

---

## Prerequisites (30s)

- [ ] Project open at `unity/CityMajor.Unity` (Unity 6000.5+ / URP)
- [ ] `./scripts/build-simcore-for-unity.sh` after any `SimHost` change
- [ ] **CityMajor → Smoke → Run Smoke Prep** — Console shows Play gate batch PASS (or fix blockers)

---

## Smoke steps (≈2–3 min)

Use **CityMajor → Smoke → Open Play Scene**, then press **Play**.

| # | Check | Pass criteria |
|---|--------|----------------|
| 1 | **Open scene** | `Play.unity` active; `CityMajorBootstrap` present (else **CityMajor → Setup Play Scene**) |
| 2 | **Enter Play** | No red Console errors; `[CityMajor]` sim init (not placeholder-only) |
| 3 | **Place road** | `4` → road mode → LMB drag paints roads on the grid |
| 4 | **Paint zone** | `1` / `2` / `3` → R / C / I → LMB drag paints zones adjacent to roads |
| 5 | **See buildings** | After a few sim ticks, zone growth / building instances appear (GLTF or box LOD) |
| 6 | **HUD metrics** | Top-left **Pop / Funds / Hour** move with the sim (not frozen sine placeholders); RCI demand strip reacts |
| 7 | **FPS** | Game view **Stats** (or Profiler) ≥ **~30 FPS** while painting; no sustained hitch loop |

### Quick controls

| Key | Action |
|-----|--------|
| `4` | Toggle road brush |
| `1` / `2` / `3` | Residential / Commercial / Industrial |
| `0` | Erase zones |
| `X` | Bulldoze |
| MMB / Alt+RMB | Pan camera |
| `Space` | Pause sim |
| `[` / `]` / Backslash | Sim speed 1× / 2× / 4× (keypad 1/2/4) |
| `F1` | Help overlay |
| Cathedral HUD (top-right) | Commute minutes · O-D coverage · commute sat · top O-D tile pairs |

---

## Fail → next

| Symptom | Likely fix |
|---------|------------|
| Play cancelled on enter | Read Play gate BLOCK errors — missing DLL/bootstrap/build settings; or toggle Skip Enter Guards |
| No sim / placeholder HUD | Rebuild SimCore DLL; domain reload; Console must show `SimHost ready` |
| Roads/zones don’t stick | Confirm Play mode + raycast hits ZoneGrid collider |
| No buildings | Wait ~8 ticks; check GLTF symlink (`CityMajor → Verify GLTF Catalog`) |
| HUD static | `CitySimBridge` not publishing `OnStateChanged` — check bootstrap |
| FPS ≪ 30 | Drop zoom / disable overlays (`T`/`V`/`U`); file note for LOD work |

---

## Sign-off (smoke only)

| Role | Name | Date | Pass? |
|------|------|------|-------|
| Dev | | | |

Pass here ≠ full SB-4176. For panels, save/load, Steam, life layers → [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md).
