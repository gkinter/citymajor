#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityMajor.Editor
{
    public static class CityMajorPlayVerify
    {
        const string ChecklistRelative = "docs/design/UNITY_PLAY_CHECKLIST.md";

        [MenuItem("CityMajor/Open Play Verification Checklist (SB-4176)")]
        public static void OpenChecklist()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var path = Path.Combine(repoRoot, ChecklistRelative);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("CityMajor", $"Checklist not found:\n{path}", "OK");
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
    }
}
#endif
