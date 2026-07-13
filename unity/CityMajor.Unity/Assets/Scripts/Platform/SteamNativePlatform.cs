#if STEAMWORKS_NET
using Steamworks;
#endif

namespace CityMajor.Platform
{
    /// <summary>
    /// Live Steamworks.NET backend. Compile with <c>STEAMWORKS_NET</c> after running
    /// <c>scripts/setup-steamworks-unity.sh</c>.
    /// </summary>
    public sealed class SteamNativePlatform : ISteamPlatform
    {
        public bool IsReady { get; private set; }

        public bool TryInit(uint appId)
        {
#if STEAMWORKS_NET
            if (IsReady)
                return true;

            try
            {
                if (!Packsize.Test())
                {
                    UnityEngine.Debug.LogError("[CityMajor] Steam Packsize.Test failed — wrong Steamworks.NET build.");
                    return false;
                }

                if (!DllCheck.Test())
                {
                    UnityEngine.Debug.LogError("[CityMajor] Steam DllCheck.Test failed — steam_api64.dll missing.");
                    return false;
                }

                IsReady = SteamAPI.Init();
                if (!IsReady)
                {
                    UnityEngine.Debug.LogWarning("[CityMajor] SteamAPI.Init failed — is Steam running?");
                    return false;
                }

                UnityEngine.Debug.Log($"[CityMajor] Steam initialized (AppId={appId}, user={SteamFriends.GetPersonaName()}).");
                return true;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CityMajor] Steam init exception: {ex.Message}");
                IsReady = false;
                return false;
            }
#else
            _ = appId;
            return false;
#endif
        }

        public void Shutdown()
        {
#if STEAMWORKS_NET
            if (!IsReady)
                return;

            SteamAPI.Shutdown();
            IsReady = false;
#endif
        }

        public void RunCallbacks()
        {
#if STEAMWORKS_NET
            if (IsReady)
                SteamAPI.RunCallbacks();
#endif
        }

        public void SetRichPresence(string key, string value)
        {
#if STEAMWORKS_NET
            if (IsReady)
                SteamFriends.SetRichPresence(key, value);
#else
            _ = key;
            _ = value;
#endif
        }

        public void ClearRichPresence()
        {
#if STEAMWORKS_NET
            if (IsReady)
                SteamFriends.ClearRichPresence();
#endif
        }

        public bool CloudFileExists(string fileName)
        {
#if STEAMWORKS_NET
            if (!IsReady)
                return false;

            return SteamRemoteStorage.FileExists(fileName);
#else
            _ = fileName;
            return false;
#endif
        }

        public bool CloudWrite(string fileName, byte[] data)
        {
#if STEAMWORKS_NET
            if (!IsReady || data == null || data.Length == 0)
                return false;

            return SteamRemoteStorage.FileWrite(fileName, data, data.Length);
#else
            _ = fileName;
            _ = data;
            return false;
#endif
        }

        public bool CloudRead(string fileName, out byte[] data)
        {
#if STEAMWORKS_NET
            data = null;
            if (!IsReady || !SteamRemoteStorage.FileExists(fileName))
                return false;

            int size = SteamRemoteStorage.GetFileSize(fileName);
            if (size <= 0)
                return false;

            data = new byte[size];
            int read = SteamRemoteStorage.FileRead(fileName, data, size);
            return read == size;
#else
            _ = fileName;
            data = null;
            return false;
#endif
        }

        public bool IsAchievementUnlocked(string apiName)
        {
#if STEAMWORKS_NET
            if (!IsReady || string.IsNullOrEmpty(apiName))
                return false;

            return SteamUserStats.GetAchievement(apiName, out var achieved) && achieved;
#else
            _ = apiName;
            return false;
#endif
        }

        public bool TryUnlockAchievement(string apiName)
        {
#if STEAMWORKS_NET
            if (!IsReady || string.IsNullOrEmpty(apiName))
                return false;

            if (IsAchievementUnlocked(apiName))
                return false;

            if (!SteamUserStats.SetAchievement(apiName))
                return false;

            SteamUserStats.StoreStats();
            return true;
#else
            _ = apiName;
            return false;
#endif
        }
    }
}
