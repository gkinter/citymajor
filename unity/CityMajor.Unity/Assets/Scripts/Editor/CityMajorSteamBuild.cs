#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityMajor.Editor
{
    /// <summary>Headless / menu Windows Steam player build (SB-4181).</summary>
    public static class CityMajorSteamBuild
    {
        const string PlayScene = "Assets/Scenes/Play.unity";

        [MenuItem("CityMajor/Build/Windows Steam Player")]
        public static void BuildWindowsMenu() => BuildWindows(GetDefaultOutputPath());

        public static void BuildWindowsCi()
        {
            var output = GetDefaultOutputPath();
            if (!BuildWindows(output))
                EditorApplication.Exit(1);
        }

        static string GetDefaultOutputPath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var repoRoot = Directory.GetParent(projectRoot ?? "")?.FullName;
            var outDir = Path.Combine(repoRoot ?? projectRoot ?? ".", "Build", "Steam", "windows");
            Directory.CreateDirectory(outDir);
            return Path.Combine(outDir, "CityMajor.exe");
        }

        static bool BuildWindows(string outputPath)
        {
            var scenePath = Path.Combine(Application.dataPath, "Scenes", "Play.unity");
            if (!File.Exists(scenePath))
            {
                Debug.LogError("[CityMajor] Missing Assets/Scenes/Play.unity");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PlayScene },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[CityMajor] Steam build failed: {report.summary.result}");
                return false;
            }

            CopySteamSupportFiles(Path.GetDirectoryName(outputPath)!);
            Debug.Log($"[CityMajor] Steam build OK → {outputPath}");
            return true;
        }

        static void CopySteamSupportFiles(string outputDir)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (projectRoot == null)
                return;

            var appIdSrc = Path.Combine(projectRoot, "steam_appid.txt");
            if (File.Exists(appIdSrc))
            {
                File.Copy(appIdSrc, Path.Combine(outputDir, "steam_appid.txt"), overwrite: true);
            }
        }
    }
}
#endif
