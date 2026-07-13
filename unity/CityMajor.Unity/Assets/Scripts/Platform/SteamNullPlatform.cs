namespace CityMajor.Platform
{
    /// <summary>No-op backend — default when Steamworks.NET is not installed.</summary>
    public sealed class SteamNullPlatform : ISteamPlatform
    {
        public bool IsReady => false;

        public bool TryInit(uint appId) => false;

        public void Shutdown() { }

        public void RunCallbacks() { }

        public void SetRichPresence(string key, string value) { }

        public void ClearRichPresence() { }

        public bool CloudFileExists(string fileName) => false;

        public bool CloudWrite(string fileName, byte[] data) => false;

        public bool CloudRead(string fileName, out byte[] data)
        {
            data = null;
            return false;
        }
    }
}
