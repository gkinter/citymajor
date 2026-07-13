using UnityEngine;

namespace CityMajor.Platform
{
    /// <summary>
    /// Phase 3 Steam init (SB-4180). Uses Steamworks.NET when STEAMWORKS_NET is defined.
    /// </summary>
    public sealed class SteamBootstrap : MonoBehaviour
    {
        [SerializeField] bool enableInEditor;
        [SerializeField] bool preferNativeBackend = true;

        static SteamBootstrap _instance;

        public static bool IsSteamReady => SteamRuntime.IsReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(SteamBootstrap));
            _instance = go.AddComponent<SteamBootstrap>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

#if UNITY_EDITOR
            if (!enableInEditor)
            {
                SteamRuntime.SetBackend(new SteamNullPlatform());
                Debug.Log("[CityMajor] Steam skipped in Editor (enable enableInEditor on SteamBootstrap to test).");
                return;
            }
#endif

            TryInit();
        }

        void TryInit()
        {
            ISteamPlatform backend = preferNativeBackend
                ? new SteamNativePlatform()
                : new SteamNullPlatform();

            if (backend.TryInit(SteamAppConfig.AppId))
            {
                SteamRuntime.SetBackend(backend);
                return;
            }

            backend.Shutdown();
            SteamRuntime.SetBackend(new SteamNullPlatform());
            Debug.Log($"[CityMajor] Steam offline — stub mode (AppId={SteamAppConfig.AppId}). Run setup-steamworks-unity.sh + STEAMWORKS_NET define for live SDK.");
        }

        void Update() => SteamRuntime.Backend.RunCallbacks();

        void OnApplicationQuit() => Shutdown();

        void OnDestroy()
        {
            if (_instance == this)
                Shutdown();
        }

        void Shutdown()
        {
            if (!SteamRuntime.IsReady)
                return;

            SteamRuntime.Backend.ClearRichPresence();
            SteamRuntime.Backend.Shutdown();
            SteamRuntime.SetBackend(new SteamNullPlatform());
        }
    }
}
