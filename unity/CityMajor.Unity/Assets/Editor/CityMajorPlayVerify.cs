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
    }
}
#endif
