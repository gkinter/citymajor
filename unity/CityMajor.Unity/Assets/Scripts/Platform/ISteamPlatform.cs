namespace CityMajor.Platform
{
    /// <summary>Steam API surface used by CityMajor (null backend when SDK absent).</summary>
    public interface ISteamPlatform
    {
        bool IsReady { get; }

        bool TryInit(uint appId);

        void Shutdown();

        void RunCallbacks();

        void SetRichPresence(string key, string value);

        void ClearRichPresence();

        bool CloudFileExists(string fileName);

        bool CloudWrite(string fileName, byte[] data);

        bool CloudRead(string fileName, out byte[] data);
    }
}
