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

namespace CityMajor.Editor
{
    public static class CityMajorPlayVerify
    {
        const string ChecklistRelative = "docs/design/UNITY_PLAY_CHECKLIST.md";
        const string DispatchRelative = "docs/design/UNITY_AGENT_DISPATCH.md";

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
                "Rebuild SimCore after SimHost edits: ./scripts/build-simcore-for-unity.sh. " +
                "Cathedral P1–P4: RoadTypeToolbar Local/Collector/Highway + Bridge/Tunnel/Ramp → PlaceRoad; ZonePaintTool density; " +
                "CitySimState MeanRentBurden / ResidentialVacancy (F1 Help).");
        }

        [MenuItem("CityMajor/Verify GLTF Catalog")]
        public static void VerifyGltfCatalog()
        {
            var failures = new List<string>();
            var modernLink = Path.Combine(Application.dataPath, "Art", "Gltf", "modern");

            if (!Directory.Exists(modernLink))
                failures.Add($"modern GLTF path missing: {modernLink}");
            else if (!IsSymlink(modernLink))
                failures.Add($"Art/Gltf/modern is not a symlink (run ./scripts/setup-unity.sh)");

            foreach (var key in GltfCatalog.ShippedKeys)
            {
                var glbPath = Path.Combine(modernLink, $"{key}.glb");
                if (!File.Exists(glbPath))
                    failures.Add($"missing GLB for key '{key}' (expected {glbPath})");
            }

            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[CityMajor] GLTF catalog verify: PASS ({GltfCatalog.ShippedKeys.Count} GLBs, modern symlink OK)");
                return;
            }

            foreach (var failure in failures)
                Debug.LogError($"[CityMajor] GLTF catalog verify: {failure}");

            Debug.LogError($"[CityMajor] GLTF catalog verify: FAIL ({failures.Count} issue(s))");
        }

        [MenuItem("CityMajor/Verify Achievements Catalog")]
        public static void VerifyAchievementsCatalog()
        {
            const int minEntries = 10;
            var failures = new List<string>();
            var catalogPath = Path.Combine(Application.streamingAssetsPath, "achievements-v1.json");

            if (!File.Exists(catalogPath))
            {
                Debug.LogError($"[CityMajor] Achievements catalog verify: missing {catalogPath}");
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
                }
                else
                {
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
            }
            catch (Exception ex)
            {
                failures.Add($"invalid JSON or parse error: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                var count = JsonUtility.FromJson<AchievementCatalogFile>(File.ReadAllText(catalogPath)).achievements.Length;
                Debug.Log($"[CityMajor] Achievements catalog verify: PASS ({count} entries, no duplicate steamApiName)");
                return;
            }

            foreach (var failure in failures)
                Debug.LogError($"[CityMajor] Achievements catalog verify: {failure}");

            Debug.LogError($"[CityMajor] Achievements catalog verify: FAIL ({failures.Count} issue(s))");
        }

        [MenuItem("CityMajor/Verify Rendering Parity")]
        public static void VerifyRenderingParity()
        {
            var failures = new List<string>();

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

            if (failures.Count == 0)
            {
                Debug.Log(
                    "[CityMajor] Rendering parity verify: PASS (box LOD, camera Distance, demand foundation, EventTicker)");
                return;
            }

            foreach (var failure in failures)
                Debug.LogError($"[CityMajor] Rendering parity verify: {failure}");

            Debug.LogError($"[CityMajor] Rendering parity verify: FAIL ({failures.Count} issue(s))");
        }

        [MenuItem("CityMajor/Run Preflight Checks")]
        public static void RunPreflightChecks()
        {
            var issues = 0;
            var simCoreDll = Path.Combine(
                Application.dataPath, "Plugins", "Forge", "Forge.SimCore.dll");

            if (!File.Exists(simCoreDll))
            {
                Debug.LogError(
                    "[CityMajor] Preflight: Forge.SimCore.dll missing — run ./scripts/build-simcore-for-unity.sh");
                issues++;
            }
            else
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(simCoreDll);
                Debug.Log(
                    $"[CityMajor] Preflight: Forge.SimCore.dll OK ({new FileInfo(simCoreDll).Length / 1024} KB, age {age.TotalHours:F1}h)");
            }

            VerifyGltfCatalog();
            VerifyAchievementsCatalog();
            VerifyRenderingParity();

            if (issues == 0)
                Debug.Log("[CityMajor] Preflight: complete — enter Play and run SB-4176 checklist");
            else
                Debug.LogError($"[CityMajor] Preflight: {issues} blocking issue(s) before Play");
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
