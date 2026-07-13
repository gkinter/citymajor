namespace CityMajor.Platform
{
    /// <summary>CMJR cloud sync via Steam Remote Storage (SB-4180).</summary>
    public static class SteamCloudSave
    {
        public const string CloudFileName = CityMajor.Sim.CitySimBridge.DefaultSaveFileName;

        public static bool TryUpload(byte[] cmjrBytes) =>
            SteamRuntime.IsReady && SteamRuntime.Backend.CloudWrite(CloudFileName, cmjrBytes);

        public static bool TryDownload(out byte[] cmjrBytes) =>
            SteamRuntime.IsReady && SteamRuntime.Backend.CloudRead(CloudFileName, out cmjrBytes);

        public static bool HasCloudSave() =>
            SteamRuntime.IsReady && SteamRuntime.Backend.CloudFileExists(CloudFileName);
    }
}
