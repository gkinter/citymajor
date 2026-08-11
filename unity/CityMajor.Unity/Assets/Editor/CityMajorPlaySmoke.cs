#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityMajor.Editor
{
    /// <summary>
    /// Fast Play Mode smoke gate — open scene, play, road, zone, buildings, HUD, FPS.
    /// See docs/design/UNITY_PLAY_SMOKE.md.
    /// </summary>
    public static class CityMajorPlaySmoke
    {
        const string SmokeDocRelative = "docs/design/UNITY_PLAY_SMOKE.md";
        const string PlaySceneAssetPath = "Assets/Scenes/Play.unity";

        const string MenuRoot = "CityMajor/Smoke/";

        [MenuItem(MenuRoot + "Open Play Scene", priority = 10)]
        public static void OpenPlayScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "CityMajor Smoke",
                    "Exit Play Mode before opening Play.unity.",
                    "OK");
                return;
            }

            if (!File.Exists(Path.Combine(Application.dataPath, "Scenes", "Play.unity")))
            {
                EditorUtility.DisplayDialog(
                    "CityMajor Smoke",
                    $"Play scene missing:\n{PlaySceneAssetPath}",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(PlaySceneAssetPath, OpenSceneMode.Single);
            Debug.Log($"[CityMajor] Smoke: opened {scene.path}. Press Play, then run the smoke steps.");
        }

        [MenuItem(MenuRoot + "Open Play Mode Smoke Checklist", priority = 20)]
        public static void OpenSmokeChecklist()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var path = Path.Combine(repoRoot, SmokeDocRelative);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("CityMajor Smoke", $"Checklist not found:\n{path}", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        [MenuItem(MenuRoot + "Log Smoke Steps", priority = 30)]
        public static void LogSmokeSteps()
        {
            Debug.Log(
                "[CityMajor] Play Mode smoke (UNITY_PLAY_SMOKE):\n" +
                "  1. Open Play.unity (CityMajor → Smoke → Open Play Scene)\n" +
                "  2. Enter Play — no red errors; [CityMajor] sim init\n" +
                "  3. Place road — key 4, LMB drag\n" +
                "  4. Paint zone — keys 1/2/3, LMB drag near roads\n" +
                "  5. See buildings — wait for zone growth / instances\n" +
                "  6. HUD metrics — Pop / Funds / Hour update with sim\n" +
                "  7. FPS — Game view Stats ≥ ~30 while painting\n" +
                "Doc: docs/design/UNITY_PLAY_SMOKE.md");
        }

        [MenuItem(MenuRoot + "Run Smoke Prep", priority = 40)]
        public static void RunSmokePrep()
        {
            OpenPlayScene();
            CityMajorPlayVerify.RunPlayGateBatchChecks();
            LogSmokeSteps();
            Debug.Log(
                "[CityMajor] Smoke prep done — press Play and walk the 7 steps. " +
                "Enter Play is blocked if SimCore/bootstrap/build-settings fail (U3.6).");
        }
    }
}
#endif
