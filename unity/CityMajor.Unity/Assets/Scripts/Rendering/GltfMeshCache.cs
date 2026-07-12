using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityMajor.Rendering
{
    /// <summary>
    /// Loads GLB meshes for catalog keys — editor AssetDatabase first, glTFast runtime fallback.
    /// </summary>
    public static class GltfMeshCache
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        public static Mesh GetOrLoad(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey))
                return null;

            if (Cache.TryGetValue(catalogKey, out var cached) && cached != null)
                return cached;

            if (!GltfCatalog.TryGetAssetPath(catalogKey, out var assetPath))
                return null;

#if UNITY_EDITOR
            var mesh = LoadFromAssetDatabase(assetPath);
#else
            var mesh = LoadFromGltfFile(assetPath);
#endif
            if (mesh != null)
                Cache[catalogKey] = mesh;
            return mesh;
        }

        public static void Preload(IEnumerable<string> catalogKeys)
        {
            foreach (var key in catalogKeys)
                GetOrLoad(key);
        }

#if UNITY_EDITOR
        static Mesh LoadFromAssetDatabase(string assetPath)
        {
            if (!File.Exists(assetPath))
                return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Mesh best = null;
            foreach (var asset in assets)
            {
                if (asset is not Mesh mesh)
                    continue;
                if (best == null || mesh.vertexCount > best.vertexCount)
                    best = mesh;
            }
            return best;
        }
#endif

        static Mesh LoadFromGltfFile(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return null;

            var import = new GltfImport();
            var task = LoadAsync(import, fullPath);
            task.Wait();
            return task.Result;
        }

        static async Task<Mesh> LoadAsync(GltfImport import, string fullPath)
        {
            var uri = new System.Uri(fullPath).AbsoluteUri;
            if (!await import.Load(uri))
                return null;

            var temp = new GameObject("GltfMeshExtract");
            try
            {
                if (!await import.InstantiateMainSceneAsync(temp.transform))
                    return null;

                Mesh best = null;
                foreach (var filter in temp.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;
                    if (best == null || mesh.vertexCount > best.vertexCount)
                        best = mesh;
                }

                if (best == null)
                    return null;

                return Object.Instantiate(best);
            }
            finally
            {
                Object.Destroy(temp);
            }
        }
    }
}
