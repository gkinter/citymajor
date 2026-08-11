#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CityMajor.Core;
using CityMajor.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityMajor.Editor
{
    public static class CityMajorPlayVerify
    {
        const string ChecklistRelative = "docs/design/UNITY_PLAY_CHECKLIST.md";
        const string DispatchRelative = "docs/design/UNITY_AGENT_DISPATCH.md";
        const string PlaySceneAssetPath = "Assets/Scenes/Play.unity";
        const string BootstrapScriptGuid = "6a757abf4f71b45e090c461e84794a68";
        const string SimHostTypeName = "Forge.SimCore.SimHost, Forge.SimCore";

        [MenuItem("CityMajor/Open Play Verification Checklist (SB-4176)")]
        public static void OpenChecklist() => OpenRepoDoc(ChecklistRelative, "Play Verification Checklist");

        [MenuItem("CityMajor/Open Agent Dispatch Doc")]
        public static void OpenAgentDispatch() => OpenRepoDoc(DispatchRelative, "Agent Dispatch Doc");

        static void OpenRepoDoc(string relativePath, string label)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var path = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("CityMajor", $"{label} not found:\n{path}", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        [MenuItem("CityMajor/Log Play Gate Reminder")]
        public static void LogReminder()
        {
            Debug.Log(
                "[CityMajor] SB-4176 Play gate: run CityMajor → Smoke → Run Smoke Prep first " +
                "(docs/design/UNITY_PLAY_SMOKE.md), then Open Play Verification Checklist. " +
                "U3.6: CityMajor → Run Play Gate Batch Checks; enter-Play guards block missing SimCore/bootstrap. " +
                "Rebuild SimCore after SimHost edits: ./scripts/build-simcore-for-unity.sh. " +
                "CLI: ./scripts/verify-unity-play-gate.sh. " +
                "Cathedral P1–P4: RoadTypeToolbar Local/Collector/Highway + Bridge/Tunnel/Ramp → PlaceRoad; ZonePaintTool density; " +
                "ZoningToolbar Office/Mixed/Ag/Park era gates; Med/High density era+tech gates; " +
                "CitySimState MeanRentBurden / ResidentialVacancy / MeanCommuteMinutes · O-D · commute sat · CommuteOdSample top pairs (F1 Help). " +
                "Sim speed: [ ] \\ (not 5/6/7/8 — those are Office/Mixed/Ag/Park).");
        }

        /// <summary>
        /// Blocking + warning diagnostics for Play Mode enter (U3.6). Used by menu batch + enter guard.
        /// </summary>
        public static void CollectEnterPlayDiagnostics(List<string> blocking, List<string> warnings)
        {
            CollectSimCoreDllIssues(blocking, warnings);
            CollectSimHostTypeIssues(blocking);
            CollectBuildSettingsIssues(blocking);
            CollectPlaySceneBootstrapIssues(blocking);
            CollectGltfCatalogIssues(warnings);
            CollectAchievementsCatalogIssues(warnings);
            CollectRenderingParityIssues(warnings);
            CollectZoneEraGateIssues(warnings);
            CollectCathedralSurfaceIssues(warnings);
        }

        [MenuItem("CityMajor/Run Play Gate Batch Checks (U3.6)", priority = 50)]
        public static void RunPlayGateBatchChecks()
        {
            var blocking = new List<string>();
            var warnings = new List<string>();
            CollectEnterPlayDiagnostics(blocking, warnings);

            foreach (var warning in warnings)
                Debug.LogWarning($"[CityMajor] Play gate batch warn: {warning}");

            foreach (var issue in blocking)
                Debug.LogError($"[CityMajor] Play gate batch BLOCK: {issue}");

            if (blocking.Count == 0 && warnings.Count == 0)
            {
                Debug.Log(
                    "[CityMajor] Play gate batch: PASS — SimCore DLL + SimHost type + Play.unity build entry + " +
                    "bootstrap + GLTF + achievements + rendering + zone gates + Cathedral surfaces");
                return;
            }

            if (blocking.Count == 0)
            {
                Debug.Log(
                    $"[CityMajor] Play gate batch: PASS with {warnings.Count} warning(s) — safe to enter Play; " +
                    "fix warns before SB-4176 sign-off");
                return;
            }

            Debug.LogError(
                $"[CityMajor] Play gate batch: FAIL — {blocking.Count} blocker(s), {warnings.Count} warning(s). " +
                "Enter Play will be cancelled until blockers clear (unless Skip Enter Guards).");
        }

        [MenuItem("CityMajor/Verify GLTF Catalog")]
        public static void VerifyGltfCatalog()
        {
            var failures = new List<string>();
            CollectGltfCatalogIssues(failures);
            ReportNamedVerify("GLTF catalog", failures, $"{GltfCatalog.ShippedKeys.Count} GLBs, modern symlink OK");
        }

        [MenuItem("CityMajor/Verify Achievements Catalog")]
        public static void VerifyAchievementsCatalog()
        {
            var failures = new List<string>();
            CollectAchievementsCatalogIssues(failures);
            if (failures.Count == 0)
            {
                var catalogPath = Path.Combine(Application.streamingAssetsPath, "achievements-v1.json");
                var count = JsonUtility.FromJson<AchievementCatalogFile>(File.ReadAllText(catalogPath)).achievements.Length;
                Debug.Log($"[CityMajor] Achievements catalog verify: PASS ({count} entries, no duplicate steamApiName)");
                return;
            }

            ReportNamedVerify("Achievements catalog", failures, null);
        }

        [MenuItem("CityMajor/Verify Rendering Parity")]
        public static void VerifyRenderingParity()
        {
            var failures = new List<string>();
            CollectRenderingParityIssues(failures);
            ReportNamedVerify(
                "Rendering parity",
                failures,
                "box LOD, camera Distance, demand foundation, EventTicker");
        }

        [MenuItem("CityMajor/Verify Zone Era Gates (U3.4)")]
        public static void VerifyZoneEraGates()
        {
            var failures = new List<string>();
            CollectZoneEraGateIssues(failures);
            ReportNamedVerify(
                "Zone era gates",
                failures,
                "Park + density Med/High + Office Industrial");
        }

        [MenuItem("CityMajor/Verify Play Scene Bootstrap")]
        public static void VerifyPlaySceneBootstrap()
        {
            var failures = new List<string>();
            CollectPlaySceneBootstrapIssues(failures);
            CollectBuildSettingsIssues(failures);
            ReportNamedVerify("Play scene bootstrap", failures, "Play.unity in build settings + CityMajorBootstrap");
        }

        [MenuItem("CityMajor/Verify Cathedral Surfaces (U3.6)")]
        public static void VerifyCathedralSurfaces()
        {
            var failures = new List<string>();
            CollectCathedralSurfaceIssues(failures);
            ReportNamedVerify(
                "Cathedral surfaces",
                failures,
                "Economy/Friction/Traffic/Cathedral HUD + road/tool UXML");
        }

        [MenuItem("CityMajor/Run Preflight Checks")]
        public static void RunPreflightChecks()
        {
            var blocking = new List<string>();
            var warnings = new List<string>();
            CollectEnterPlayDiagnostics(blocking, warnings);

            foreach (var warning in warnings)
                Debug.LogWarning($"[CityMajor] Preflight warn: {warning}");

            foreach (var issue in blocking)
                Debug.LogError($"[CityMajor] Preflight: {issue}");

            if (blocking.Count == 0)
                Debug.Log(
                    $"[CityMajor] Preflight: complete — enter Play and run SB-4176 checklist" +
                    (warnings.Count > 0 ? $" ({warnings.Count} warning(s))" : string.Empty));
            else
                Debug.LogError($"[CityMajor] Preflight: {blocking.Count} blocking issue(s) before Play");
        }

        static void CollectSimCoreDllIssues(List<string> blocking, List<string> warnings)
        {
            var simCoreDll = Path.Combine(Application.dataPath, "Plugins", "Forge", "Forge.SimCore.dll");
            if (!File.Exists(simCoreDll))
            {
                blocking.Add(
                    "Forge.SimCore.dll missing — run ./scripts/build-simcore-for-unity.sh (placeholder sim is not a Play gate)");
                return;
            }

            var info = new FileInfo(simCoreDll);
            var age = DateTime.UtcNow - info.LastWriteTimeUtc;
            Debug.Log(
                $"[CityMajor] Preflight: Forge.SimCore.dll OK ({info.Length / 1024} KB, age {age.TotalHours:F1}h)");

            if (age.TotalDays > 30)
                warnings.Add(
                    $"Forge.SimCore.dll is {age.TotalDays:F0}d old — rebuild after SimHost changes " +
                    "(./scripts/build-simcore-for-unity.sh)");
        }

        static void CollectSimHostTypeIssues(List<string> blocking)
        {
            Type simHostType = null;
            try
            {
                simHostType = Type.GetType(SimHostTypeName, throwOnError: false);
                if (simHostType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!string.Equals(asm.GetName().Name, "Forge.SimCore", StringComparison.Ordinal))
                            continue;
                        simHostType = asm.GetType("Forge.SimCore.SimHost", throwOnError: false);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                blocking.Add($"SimHost type load failed: {ex.Message}");
                return;
            }

            if (simHostType == null)
            {
                blocking.Add(
                    "Forge.SimCore.SimHost type not loaded — DLL missing/corrupt or domain reload needed after build-simcore-for-unity.sh");
                return;
            }

            var init = simHostType.GetMethod("Init", BindingFlags.Instance | BindingFlags.Public);
            var paint = simHostType.GetMethod("PaintZone", BindingFlags.Instance | BindingFlags.Public);
            if (init == null || paint == null)
                blocking.Add("Forge.SimCore.SimHost missing Init/PaintZone — rebuild SimCore DLL");
        }

        static void CollectBuildSettingsIssues(List<string> blocking)
        {
            var scenes = EditorBuildSettings.scenes;
            var found = false;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (!string.Equals(scenes[i].path, PlaySceneAssetPath, StringComparison.Ordinal))
                    continue;
                found = true;
                if (!scenes[i].enabled)
                    blocking.Add($"{PlaySceneAssetPath} is in build settings but disabled");
                break;
            }

            if (!found)
                blocking.Add($"{PlaySceneAssetPath} missing from EditorBuildSettings (player builds will fail)");
        }

        static void CollectPlaySceneBootstrapIssues(List<string> blocking)
        {
            var playScenePath = Path.Combine(Application.dataPath, "Scenes", "Play.unity");
            if (!File.Exists(playScenePath))
            {
                blocking.Add($"Play scene missing on disk: {PlaySceneAssetPath}");
                return;
            }

            var yaml = File.ReadAllText(playScenePath);
            if (!yaml.Contains(BootstrapScriptGuid, StringComparison.Ordinal))
            {
                blocking.Add(
                    "Play.unity missing CityMajorBootstrap — run CityMajor → Setup Play Scene (Phase 1)");
            }

            // If Play scene is already open, also assert live component (catches broken GUID remap).
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() &&
                string.Equals(active.path, PlaySceneAssetPath, StringComparison.Ordinal) &&
                Object.FindFirstObjectByType<CityMajorBootstrap>() == null)
            {
                blocking.Add(
                    "Active Play.unity has no CityMajorBootstrap component — run Setup Play Scene");
            }
        }

        static void CollectGltfCatalogIssues(List<string> failures)
        {
            var modernLink = Path.Combine(Application.dataPath, "Art", "Gltf", "modern");

            if (!Directory.Exists(modernLink))
                failures.Add($"modern GLTF path missing: {modernLink}");
            else if (!IsSymlink(modernLink))
                failures.Add("Art/Gltf/modern is not a symlink (run ./scripts/setup-unity.sh)");

            foreach (var key in GltfCatalog.ShippedKeys)
            {
                var glbPath = Path.Combine(modernLink, $"{key}.glb");
                if (!File.Exists(glbPath))
                    failures.Add($"missing GLB for key '{key}' (expected {glbPath})");
            }
        }

        static void CollectAchievementsCatalogIssues(List<string> failures)
        {
            const int minEntries = 10;
            var catalogPath = Path.Combine(Application.streamingAssetsPath, "achievements-v1.json");

            if (!File.Exists(catalogPath))
            {
                failures.Add($"achievements catalog missing: {catalogPath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(catalogPath);
                var file = JsonUtility.FromJson<AchievementCatalogFile>(json);
                var achievements = file?.achievements;

                if (achievements == null || achievements.Length == 0)
                {
                    failures.Add("achievements array is missing or empty");
                    return;
                }

                if (achievements.Length < minEntries)
                    failures.Add($"expected at least {minEntries} entries, found {achievements.Length}");

                var seenApiNames = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < achievements.Length; i++)
                {
                    var entry = achievements[i];
                    var prefix = $"achievements[{i}]";

                    if (string.IsNullOrWhiteSpace(entry.steamApiName))
                        failures.Add($"{prefix}: missing steamApiName (apiName)");
                    else if (!entry.steamApiName.StartsWith("CM_", StringComparison.Ordinal))
                        failures.Add($"{prefix}: steamApiName must start with CM_ (got '{entry.steamApiName}')");
                    else if (!seenApiNames.Add(entry.steamApiName))
                        failures.Add($"{prefix}: duplicate steamApiName '{entry.steamApiName}'");

                    if (string.IsNullOrWhiteSpace(entry.name))
                        failures.Add($"{prefix}: missing name (displayName)");

                    if (string.IsNullOrWhiteSpace(entry.description))
                        failures.Add($"{prefix}: missing description");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"invalid JSON or parse error: {ex.Message}");
            }
        }

        static void CollectRenderingParityIssues(List<string> failures)
        {
            var instancerType = typeof(BuildingInstancer);
            if (instancerType.GetField("boxLodDistance", BindingFlags.Instance | BindingFlags.NonPublic) == null)
                failures.Add("BuildingInstancer missing boxLodDistance field (camera-distance box LOD)");

            var cameraDistance = typeof(IsometricCameraController).GetProperty(
                "Distance", BindingFlags.Instance | BindingFlags.Public);
            if (cameraDistance == null || cameraDistance.PropertyType != typeof(float))
                failures.Add("IsometricCameraController missing public float Distance property");

            var demandUxml = Path.Combine(Application.dataPath, "UI", "DemandOverlay.uxml");
            if (!File.Exists(demandUxml))
                failures.Add($"DemandOverlay.uxml missing: {demandUxml}");
            else
            {
                var demandText = File.ReadAllText(demandUxml);
                if (!demandText.Contains("goods-row"))
                    failures.Add("DemandOverlay.uxml missing goods-row (goods shortage bar)");
                if (!demandText.Contains("util-row"))
                    failures.Add("DemandOverlay.uxml missing util-row (utility stress)");
            }

            var tickerUxml = Path.Combine(Application.dataPath, "UI", "EventTicker.uxml");
            if (!File.Exists(tickerUxml))
                failures.Add($"EventTicker.uxml missing: {tickerUxml}");
        }

        static void CollectZoneEraGateIssues(List<string> failures)
        {
            var parkFound = false;
            foreach (var tier in CityMajor.Sim.ZoneTiers.Paintable)
            {
                if (tier.Kind != CityMajor.Input.ZonePaintTool.ZoneKind.Park)
                    continue;
                parkFound = true;
                if (tier.EngineZoneType != 8)
                    failures.Add($"Park EngineZoneType expected 8, got {tier.EngineZoneType}");
                if (tier.MinEra != 0)
                    failures.Add($"Park MinEra expected 0 (Frontier), got {tier.MinEra}");
                if (tier.RequiredTechId != CityMajor.Sim.ZoneTiers.ParkTechId)
                    failures.Add($"Park RequiredTechId expected {CityMajor.Sim.ZoneTiers.ParkTechId}, got {tier.RequiredTechId}");
            }

            if (!parkFound)
                failures.Add("ZoneTiers.Paintable missing Park");

            var dens = CityMajor.Sim.ZoneDensityTiers.Levels;
            if (dens.Length != 3)
                failures.Add($"ZoneDensityTiers.Levels expected 3 entries, got {dens.Length}");
            else
            {
                if (dens[0].Density != 1 || dens[0].MinEra != 0 || dens[0].RequiredTechId != -1)
                    failures.Add("Low density gate mismatch (expect era 0, no tech)");
                if (dens[1].Density != 2 || dens[1].MinEra != 0 ||
                    dens[1].RequiredTechId != CityMajor.Sim.ZoneDensityTiers.MediumTechId)
                    failures.Add("Med density gate mismatch (expect T029)");
                if (dens[2].Density != 3 || dens[2].MinEra != 1 ||
                    dens[2].RequiredTechId != CityMajor.Sim.ZoneDensityTiers.HighTechId)
                    failures.Add("High density gate mismatch (expect Industrial + T031)");
            }

            foreach (var tier in CityMajor.Sim.ZoneTiers.Paintable)
            {
                if (tier.Kind != CityMajor.Input.ZonePaintTool.ZoneKind.Office)
                    continue;
                if (tier.MinEra != 1)
                    failures.Add($"Office MinEra expected 1 (Industrial), got {tier.MinEra}");
            }
        }

        static void CollectCathedralSurfaceIssues(List<string> failures)
        {
            RequireType(failures, "CityMajor.UI.CathedralMetricsHudController, Assembly-CSharp");
            RequireType(failures, "CityMajor.UI.EconomyPanelController, Assembly-CSharp");
            RequireType(failures, "CityMajor.Rendering.FrictionCorridorOverlay, Assembly-CSharp");
            RequireType(failures, "CityMajor.Rendering.EdgeTrafficOverlay, Assembly-CSharp");
            RequireType(failures, "CityMajor.UI.RoadTypeToolbarController, Assembly-CSharp");
            RequireType(failures, "CityMajor.UI.ToolModeHudController, Assembly-CSharp");
            RequireType(failures, "CityMajor.UI.ZoningToolbarController, Assembly-CSharp");
            RequireType(failures, "CityMajor.UI.StatusToastController, Assembly-CSharp");

            RequireUiFile(failures, "RoadTypeToolbar.uxml");
            RequireUiFile(failures, "StatusToast.uxml");
            RequireUiFile(failures, "ToolModeHud.uxml");
            RequireUiFile(failures, "ZoningToolbar.uxml");
        }

        static void RequireType(List<string> failures, string assemblyQualifiedName)
        {
            var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
            if (type != null)
                return;

            // Fallback: scan loaded assemblies by short name.
            var comma = assemblyQualifiedName.IndexOf(',');
            var fullName = comma > 0 ? assemblyQualifiedName.Substring(0, comma).Trim() : assemblyQualifiedName;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullName, throwOnError: false) != null)
                        return;
                }
                catch
                {
                    // Dynamic assemblies can throw — ignore.
                }
            }

            failures.Add($"missing type {fullName} (Cathedral Sprint 3 surface)");
        }

        static void RequireUiFile(List<string> failures, string fileName)
        {
            var path = Path.Combine(Application.dataPath, "UI", fileName);
            if (!File.Exists(path))
                failures.Add($"UI asset missing: Assets/UI/{fileName}");
        }

        static void ReportNamedVerify(string label, List<string> failures, string passDetail)
        {
            if (failures.Count == 0)
            {
                Debug.Log(
                    passDetail != null
                        ? $"[CityMajor] {label} verify: PASS ({passDetail})"
                        : $"[CityMajor] {label} verify: PASS");
                return;
            }

            foreach (var failure in failures)
                Debug.LogError($"[CityMajor] {label} verify: {failure}");

            Debug.LogError($"[CityMajor] {label} verify: FAIL ({failures.Count} issue(s))");
        }

        [Serializable]
        sealed class AchievementCatalogFile
        {
            public AchievementCatalogEntry[] achievements;
        }

        [Serializable]
        sealed class AchievementCatalogEntry
        {
            public string steamApiName;
            public string name;
            public string description;
        }

        static bool IsSymlink(string path)
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
    }
}
#endif
