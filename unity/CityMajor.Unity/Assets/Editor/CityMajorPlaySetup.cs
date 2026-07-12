#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CityMajor.Core;

namespace CityMajor.Editor
{
    public static class CityMajorPlaySetup
    {
        const string Menu = "CityMajor/Setup Play Scene (Phase 1)";

        [MenuItem(Menu)]
        public static void SetupPlayScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                EditorUtility.DisplayDialog("CityMajor", "Open Play.unity first.", "OK");
                return;
            }

            var existing = Object.FindFirstObjectByType<CityMajorBootstrap>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("CityMajor", "CityMajorBootstrap already in scene.", "OK");
                return;
            }

            var go = new GameObject("CityMajor_Bootstrap");
            go.AddComponent<CityMajorBootstrap>();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CityMajor] Phase 1 bootstrap added. Press Play to test zone painting.");
        }
    }
}
#endif
