using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Platform
{
    public static class AchievementEvaluator
    {
        public static bool IsMet(
            AchievementEntry entry,
            CitySimState state,
            SimSnapshot snapshot,
            int unlockedTechs,
            int roadTiles)
        {
            if (entry == null)
                return false;

            return entry.conditionType switch
            {
                "population_gte" => state.Population >= entry.conditionValue,
                "approval_gte" => state.Approval * 100f >= entry.conditionValue,
                "happiness_gte" => state.Happiness * 100f >= entry.conditionValue,
                "funds_gte" => state.Funds >= entry.conditionValue,
                "zoned_tiles_gte" => state.ZonedTiles >= entry.conditionValue,
                "building_count_gte" => state.BuildingCount >= entry.conditionValue,
                "households_gte" => state.HouseholdCount >= entry.conditionValue,
                "active_laws_gte" => state.ActiveLawCount >= entry.conditionValue,
                "unlocked_techs_gte" => unlockedTechs >= entry.conditionValue,
                "road_tiles_gte" => roadTiles >= entry.conditionValue,
                "saved_once" => AchievementProgress.HasSavedOnce,
                _ => false,
            };
        }

        public static int CountRoadTiles(SimSnapshot snapshot)
        {
            if (snapshot?.TileRoadFlags == null)
                return 0;

            var count = 0;
            var flags = snapshot.TileRoadFlags;
            for (var i = 0; i < flags.Length; i++)
            {
                if (flags[i] != 0)
                    count++;
            }

            return count;
        }
    }
}
