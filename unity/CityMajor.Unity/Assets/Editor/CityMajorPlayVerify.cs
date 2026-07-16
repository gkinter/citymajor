#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                "[CityMajor] SB-4176 Play gate: open CityMajor → Open Play Verification Checklist. " +
                "Rebuild SimCore after SimHost edits: ./scripts/build-simcore-for-unity.sh");
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
