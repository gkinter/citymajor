#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityMajor.Editor
{
    /// <summary>
    /// Editor utilities for linking repo-shared assets into the Unity project (macOS-friendly symlinks).
    /// </summary>
    public static class CityMajorSetup
    {
        const string MenuPath = "CityMajor/Link Shared Assets";

        [MenuItem(MenuPath)]
        public static void LinkSharedAssets()
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "CityMajor",
                    "Could not locate repo root (expected base/data and web/public/assets/gltf/modern).",
                    "OK");
                return;
            }

            var assetsDir = Path.Combine(Application.dataPath);
            var modernTarget = Path.Combine(repoRoot, "web", "public", "assets", "gltf", "modern");
            var dataTarget = Path.Combine(repoRoot, "base", "data");
            var modernLink = Path.Combine(assetsDir, "Art", "Gltf", "modern");
            var dataLink = Path.Combine(assetsDir, "Data");

            if (!Directory.Exists(modernTarget))
            {
                Debug.LogError($"[CityMajor] Missing modern GLTF folder: {modernTarget}");
                return;
            }

            if (!Directory.Exists(dataTarget))
            {
                Debug.LogError($"[CityMajor] Missing base data folder: {dataTarget}");
                return;
            }

            try
            {
                CreateSymlink(modernTarget, modernLink);
                CreateSymlink(dataTarget, dataLink);
                AssetDatabase.Refresh();
                Debug.Log("[CityMajor] Shared asset symlinks updated.");
                EditorUtility.DisplayDialog("CityMajor", "Shared asset symlinks created.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CityMajor] Symlink failed: {ex.Message}");
                EditorUtility.DisplayDialog("CityMajor", $"Symlink failed:\n{ex.Message}", "OK");
            }
        }

        static string FindRepoRoot()
        {
            // Assets → CityMajor.Unity → unity → repo root
            var dir = new DirectoryInfo(Application.dataPath);
            for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                var data = Path.Combine(dir.FullName, "base", "data");
                var modern = Path.Combine(dir.FullName, "web", "public", "assets", "gltf", "modern");
                if (Directory.Exists(data) && Directory.Exists(modern))
                    return dir.FullName;
            }

            return null;
        }

        static void CreateSymlink(string targetPath, string linkPath)
        {
            targetPath = Path.GetFullPath(targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);

            if (Directory.Exists(linkPath) || File.Exists(linkPath))
            {
                var attrs = File.GetAttributes(linkPath);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                    Directory.Delete(linkPath, recursive: false);
                else if (Directory.Exists(linkPath))
                    Directory.Delete(linkPath, recursive: true);
                else
                    File.Delete(linkPath);
            }

            // Unity editor runtime: use ln -sfn (portable on macOS/Linux).
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/ln",
                Arguments = $"-sfn \"{targetPath}\" \"{linkPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit();
            if (proc == null || proc.ExitCode != 0)
                throw new InvalidOperationException($"ln -sfn failed for {linkPath}");

            Debug.Log($"[CityMajor] Linked {linkPath} → {targetPath}");
        }
    }
}
#endif
