#if UNITY_EDITOR
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

        static bool IsSymlink(string path)
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
    }
}
#endif
