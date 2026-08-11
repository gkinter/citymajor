# Unity Hub — "My project" path error (fixed)

## What happened

Unity Hub **New Project** was pointed at:

```
.../unity/CityMajor.Unity/My project   ← wrong (nested default name)
```

That folder was never created. The real project root is:

```
/Users/fredericbeeg/citymajor/citymajor-unity-port-plan/unity/CityMajor.Unity
```

## Fix applied (2026-07-12)

1. Bootstrapped a valid Unity 6 URP project (ProjectSettings, Play.unity, packages-lock)
2. Merged CityMajor scripts + `com.coplaydev.unity-mcp` into `manifest.json`
3. Updated Hub registry (`~/Library/Application Support/UnityHub/projects-v1.json`) → **CityMajor** at correct path
4. Removed stale **My project** entry

## Open the project

**Unity Hub → Projects → CityMajor** (or **Add** → select folder above)

Do **not** use "New Project" inside `CityMajor.Unity/` — that creates another nested folder.

## If Hub still shows the old entry

1. Hub → Projects → remove **My project** (⋯ menu)
2. **Add** → `/Users/fredericbeeg/citymajor/citymajor-unity-port-plan/unity/CityMajor.Unity`
3. Rename display title to **CityMajor** if needed
