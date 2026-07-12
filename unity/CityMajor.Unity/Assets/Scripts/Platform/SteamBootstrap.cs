using UnityEngine;

namespace CityMajor.Platform
{
    /// <summary>
    /// Phase 3 Steam init stub (SB-4180). Replace body with Steamworks.NET when package is added.
    /// </summary>
    public sealed class SteamBootstrap : MonoBehaviour
    {
        [SerializeField] bool enableInEditor;

        static bool _initialized;

        public static bool IsSteamReady => _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<SteamBootstrap>() != null)
                return;

            var go = new GameObject(nameof(SteamBootstrap));
            go.AddComponent<SteamBootstrap>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (_initialized)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            if (!enableInEditor)
            {
                Debug.Log("[CityMajor] Steam bootstrap skipped in Editor (enableInEditor to test).");
                return;
            }
#endif

            TryInitSteamStub();
        }

        void TryInitSteamStub()
        {
            // TODO(SB-4180): Steamworks.NET — SteamAPI.Init(), callbacks, overlay.
            Debug.Log($"[CityMajor] Steam stub ready (AppId={SteamAppConfig.AppId}). Add Steamworks.NET for live SDK.");
            _initialized = true;
        }

        void OnDestroy()
        {
            if (!_initialized)
                return;

            // TODO(SB-4180): SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
