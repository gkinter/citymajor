namespace CityMajor.Platform
{
    /// <summary>Steam App ID placeholder — replace before Steam partner upload (SB-4180).</summary>
    public static class SteamAppConfig
    {
        /// <summary>480 = Spacewar test app. Swap for CityMajor App ID from Steamworks partner site.</summary>
        public const uint AppId = 480;

        public const string DepotsNote =
            "Windows x64 IL2CPP standalone under unity/CityMajor.Unity; upload via steamcmd + app build vdf.";
    }
}
