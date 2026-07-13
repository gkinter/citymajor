using System;
using UnityEngine;

namespace CityMajor.Platform
{
    [Serializable]
    public sealed class AchievementEntry
    {
        public string id;
        public string steamApiName;
        public string name;
        public string description;
        public string conditionType;
        public int conditionValue;
        public bool hidden;
    }

    [Serializable]
    sealed class AchievementListFile
    {
        public AchievementEntry[] achievements;
    }

    /// <summary>Loads v1 modern-era achievement rows from StreamingAssets.</summary>
    public static class AchievementCatalog
    {
        const string FileName = "achievements-v1.json";

        static AchievementEntry[] _cached;

        public static AchievementEntry[] All()
        {
            if (_cached != null && _cached.Length > 0)
                return _cached;

            _cached = LoadFromDisk() ?? BuiltInFallback();
            return _cached;
        }

        static AchievementEntry[] LoadFromDisk()
        {
            try
            {
                var path = System.IO.Path.Combine(Application.streamingAssetsPath, FileName);
                if (!System.IO.File.Exists(path))
                    return null;

                var json = System.IO.File.ReadAllText(path);
                var file = JsonUtility.FromJson<AchievementListFile>(json);
                return file?.achievements is { Length: > 0 } ? file.achievements : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CityMajor] Achievement catalog load failed: {ex.Message}");
                return null;
            }
        }

        static AchievementEntry[] BuiltInFallback() => new[]
        {
            new AchievementEntry
            {
                id = "pop_1k",
                steamApiName = "CM_POP_1000",
                name = "Growing Town",
                description = "Reach 1,000 population.",
                conditionType = "population_gte",
                conditionValue = 1000,
            },
        };
    }
}
