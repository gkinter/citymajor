using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityMajor.UI
{
    /// <summary>Shared UXML loader for CityMajor HUD panels.</summary>
    internal static class UiAssetLoader
    {
        public static VisualTreeAsset LoadUxml(string assetPath)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
#else
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            return Resources.Load<VisualTreeAsset>(name);
#endif
        }
    }
}
