#if UNITY_EDITOR
using UnityEditor;

namespace CityMajor.Editor
{
    /// <summary>Adds STEAMWORKS_NET scripting define after Steamworks.NET plugin install.</summary>
    public static class SteamworksDefineMenu
    {
        const string Define = "STEAMWORKS_NET";

        [MenuItem("CityMajor/Platform/Enable Steamworks.NET Define")]
        static void EnableDefine()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (!defines.Contains(Define))
            {
                defines = string.IsNullOrEmpty(defines) ? Define : $"{defines};{Define}";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
                UnityEngine.Debug.Log($"[CityMajor] Added {Define} for {group}. Reimport scripts.");
            }
            else
            {
                UnityEngine.Debug.Log($"[CityMajor] {Define} already set for {group}.");
            }
        }

        [MenuItem("CityMajor/Platform/Disable Steamworks.NET Define")]
        static void DisableDefine()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (defines.Contains(Define))
            {
                defines = defines.Replace(Define, "").Replace(";;", ";").Trim(';');
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
                UnityEngine.Debug.Log($"[CityMajor] Removed {Define} for {group}.");
            }
        }
    }
}
#endif
