#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CityMajor.Editor
{
    /// <summary>
    /// U3.6 — fail-fast Play Mode enter guards (SimCore DLL, Play scene bootstrap, build settings).
    /// Soft catalog issues warn only; critical blockers cancel entering Play.
    /// Disable: EditorPrefs <c>CityMajor.PlayGate.SkipEnterGuards</c> = 1, or menu below.
    /// </summary>
    [InitializeOnLoad]
    public static class CityMajorPlayGateGuard
    {
        public const string SkipPrefsKey = "CityMajor.PlayGate.SkipEnterGuards";

        static CityMajorPlayGateGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            if (EditorPrefs.GetBool(SkipPrefsKey, false))
            {
                Debug.LogWarning(
                    "[CityMajor] Play gate enter guards skipped (EditorPrefs CityMajor.PlayGate.SkipEnterGuards).");
                return;
            }

            var blocking = new List<string>();
            var warnings = new List<string>();
            CityMajorPlayVerify.CollectEnterPlayDiagnostics(blocking, warnings);

            foreach (var warning in warnings)
                Debug.LogWarning($"[CityMajor] Play gate warn: {warning}");

            if (blocking.Count == 0)
            {
                if (warnings.Count == 0)
                    Debug.Log("[CityMajor] Play gate enter: OK (SimCore + Play scene + build settings)");
                else
                    Debug.Log(
                        $"[CityMajor] Play gate enter: OK with {warnings.Count} warning(s) — continuing Play");
                return;
            }

            foreach (var issue in blocking)
                Debug.LogError($"[CityMajor] Play gate BLOCK: {issue}");

            var summary =
                "Play Mode cancelled — fix blockers first:\n\n• " +
                string.Join("\n• ", blocking) +
                "\n\nRun CityMajor → Run Play Gate Batch Checks (U3.6).\n" +
                "To bypass once: CityMajor → Play Gate → Skip Enter Guards (session).";

            EditorUtility.DisplayDialog("CityMajor Play Gate", summary, "OK");
            EditorApplication.isPlaying = false;
        }

        [MenuItem("CityMajor/Play Gate/Skip Enter Guards (toggle)", priority = 200)]
        public static void ToggleSkipEnterGuards()
        {
            var next = !EditorPrefs.GetBool(SkipPrefsKey, false);
            EditorPrefs.SetBool(SkipPrefsKey, next);
            Debug.Log(
                next
                    ? "[CityMajor] Play gate enter guards DISABLED until re-toggled"
                    : "[CityMajor] Play gate enter guards ENABLED");
        }

        [MenuItem("CityMajor/Play Gate/Skip Enter Guards (toggle)", true)]
        public static bool ToggleSkipEnterGuardsValidate()
        {
            Menu.SetChecked("CityMajor/Play Gate/Skip Enter Guards (toggle)", EditorPrefs.GetBool(SkipPrefsKey, false));
            return true;
        }
    }
}
#endif
