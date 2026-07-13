namespace CityMajor.Platform
{
    /// <summary>One-shot player actions not visible in CitySimState alone.</summary>
    public static class AchievementProgress
    {
        public static bool HasSavedOnce { get; private set; }

        public static void NotifySaved() => HasSavedOnce = true;
    }
}
