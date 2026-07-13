namespace CityMajor.Platform
{
    /// <summary>Process-wide Steam backend selector (null or native).</summary>
    public static class SteamRuntime
    {
        static ISteamPlatform _backend = new SteamNullPlatform();

        public static ISteamPlatform Backend => _backend;

        public static bool IsReady => _backend.IsReady;

        public static void SetBackend(ISteamPlatform backend) =>
            _backend = backend ?? new SteamNullPlatform();
    }
}
