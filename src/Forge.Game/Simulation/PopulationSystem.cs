using System.Runtime.CompilerServices;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Full household-based population simulation for up to 100K households.
/// Handles births, deaths, aging, migration, employment matching,
/// satisfaction calculation, and wealth class transitions.
///
/// Staggered processing: each Tick processes 1/30th of households
/// for even load distribution across frames.
/// </summary>
public sealed class PopulationSystem
{
    // =========================================================================
    // Constants — design doc values
    // =========================================================================

    private const int StaggerBuckets = 30;

    // Birth rate
    private const float BaseBirthRatePerMonth = 0.015f;
    private const int MinFertileAge = 18;
    private const int MaxFertileAge = 45;

    // Death rates by age bracket (monthly probability)
    private const float DeathRate_0_40 = 0.001f;
    private const float DeathRate_40_60 = 0.005f;
    private const float DeathRate_60_75 = 0.02f;
    private const float DeathRate_75_85 = 0.08f;
    private const float DeathRate_85Plus = 0.20f;

    // Immigration base
    private const int BaseImmigrationPerMonth = 5;

    // Emigration
    private const byte EmigrationSatisfactionThreshold = 77; // ~30/100 mapped to 0-255 scale
    private const int EmigrationMonthsUnhappy = 6;

    // Satisfaction weights (sum = 1.0)
    private const float WeightEmployment = 0.20f;
    private const float WeightHousing = 0.15f;
    private const float WeightCommute = 0.12f;
    private const float WeightServices = 0.12f;
    private const float WeightSafety = 0.10f;
    private const float WeightEnvironment = 0.08f;
    private const float WeightEducation = 0.08f;
    private const float WeightLeisure = 0.05f;
    private const float WeightTaxFairness = 0.05f;
    private const float WeightCultural = 0.05f;

    // Retirement age
    private const int RetirementAge = 65;

    // Wealth class thresholds (monthly income vs cost-of-living ratio)
    private const float WealthThreshold_Poor = 0.5f;
    private const float WealthThreshold_Lower = 0.8f;
    private const float WealthThreshold_Middle = 1.5f;
    private const float WealthThreshold_Upper = 3.0f;
    // Above Upper = Wealthy (4)

    // Housing market — rent burden (Cathedral P2.3)
    private const float BaseRentConstant = 200f;
    private const float BaseRentLandMultiplier = 1800f;
    private const float VacancyNormalization = 0.15f;
    private const float GoodsShortageRentWeight = 0.25f;
    private const float RentBurdenAffordThreshold = 0.45f;
    private const float HighRentBurdenThreshold = 0.55f;
    private const int HighRentBurdenEmigrationMonths = 3;
    private const float HighRentBurdenEmigrationBonus = 0.25f;
    private const float PhysicalHousingWeight = 0.55f;
    private const float AffordHousingWeight = 0.45f;
    private const float RentAttractivenessBase = 1.2f;

    // =========================================================================
    // Per-household tracking for emigration dissatisfaction duration
    // =========================================================================

    private byte[] _monthsUnhappy;
    private int _monthsUnhappyCapacity;

    private byte[] _monthsHighBurden;
    private int _monthsHighBurdenCapacity;

    private float[] _rentBurden;
    private int _rentBurdenCapacity;

    // Per-household age in game years (more granular than AgeGroup)
    // Stored externally because HouseholdData.AgeGroup is only 3 buckets
    private byte[] _headAge; // age of household head (0-255 years)
    private int _headAgeCapacity;

    // Random state for deterministic simulation
    private uint _rngState;

    /// <summary>Net population change from the most recent MonthlyTick (births + immigration − deaths − emigration).</summary>
    public int LastMonthlyPopulationGrowth { get; private set; }

    /// <summary>Mean household rent burden (rent / monthly income) from the latest monthly rollup.</summary>
    public float MeanRentBurden { get; private set; }

    /// <summary>
    /// Create a new PopulationSystem with the given RNG seed.
    /// </summary>
    public PopulationSystem(uint seed = 42)
    {
        _rngState = seed == 0 ? 1 : seed;
        _monthsUnhappy = Array.Empty<byte>();
        _monthsHighBurden = Array.Empty<byte>();
        _rentBurden = Array.Empty<float>();
        _headAge = Array.Empty<byte>();
    }

    // =========================================================================
    // Tick — staggered per-household processing (1/30th per call)
    // =========================================================================

    /// <summary>
    /// Process 1/30th of households per tick for even load distribution.
    /// Updates satisfaction and transport satisfaction per household.
    /// </summary>
    public void Tick(WorldState state, int tickIndex)
    {
        EnsureCapacity(state.Households.Capacity);

        int aliveCount = state.Households.Count;
        if (aliveCount == 0) return;

        int batchSize = Math.Max(1, aliveCount / StaggerBuckets);
        int bucketIndex = tickIndex % StaggerBuckets;

        int processed = 0;
        int skipped = 0;
        int target = bucketIndex * batchSize;
        int end = (bucketIndex == StaggerBuckets - 1) ? aliveCount : target + batchSize;

        // Walk active households to find our batch window
        int activeIndex = 0;
        for (int i = 0; i < state.Households.Capacity && processed < end; i++)
        {
            if (!state.Households.IsActive(i)) continue;

            if (activeIndex >= target && activeIndex < end)
            {
                float satisfaction = CalculateSatisfaction(state, i);
                state.Households.Happiness[i] = (byte)Math.Clamp((int)(satisfaction * 2.55f), 0, 255);
                processed++;
            }
            else
            {
                skipped++;
            }
            activeIndex++;
        }
    }

    // =========================================================================
    // MonthlyTick — births, deaths, aging, migration
    // =========================================================================

    /// <summary>
    /// Monthly tick: aging, births, deaths, immigration, emigration,
    /// employment matching, wealth class transitions.
    /// </summary>
    public void MonthlyTick(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);

        int populationBefore = state.Population;

        AssignUnhousedHouseholds(state);
        UpdateRentBurdenRollup(state);

        AgeHouseholds(state);
        int deaths = CalculateDeaths(state);
        int births = CalculateBirths(state);
        int immigrants = CalculateImmigration(state);
        int emigrants = CalculateEmigration(state);

        MatchEmployment(state);
        UpdateWealthClasses(state);
        UpdateAverageHappiness(state);
        UpdatePopulationCount(state);
        LastMonthlyPopulationGrowth = state.Population - populationBefore;
    }

    // =========================================================================
    // Satisfaction — weighted multi-factor score per household (0-100)
    // =========================================================================

    /// <summary>
    /// Calculate satisfaction for a single household on a 0-100 scale.
    /// Factors: employment, housing, commute, services, safety,
    /// environment, education, leisure, tax fairness, cultural fit.
    /// </summary>
    public float CalculateSatisfaction(WorldState state, int householdIndex)
    {
        var hh = state.Households;
        if (!hh.IsActive(householdIndex)) return 0f;

        float employment = CalculateEmploymentSatisfaction(state, householdIndex);
        float housing = CalculateHousingSatisfaction(state, householdIndex);
        float commute = CalculateCommuteSatisfaction(state, householdIndex);
        float services = CalculateServicesSatisfaction(state, householdIndex);
        float safety = CalculateSafetySatisfaction(state, householdIndex);
        float environment = CalculateEnvironmentSatisfaction(state, householdIndex);
        float education = CalculateEducationSatisfaction(state, householdIndex);
        float leisure = hh.LeisureSatisfaction[householdIndex] / 2.55f;
        float taxFairness = CalculateTaxFairnessSatisfaction(state, householdIndex);
        float cultural = CalculateCulturalSatisfaction(state, householdIndex);

        float weighted =
            employment * WeightEmployment +
            housing * WeightHousing +
            commute * WeightCommute +
            services * WeightServices +
            safety * WeightSafety +
            environment * WeightEnvironment +
            education * WeightEducation +
            leisure * WeightLeisure +
            taxFairness * WeightTaxFairness +
            cultural * WeightCultural;

        return Math.Clamp(weighted, 0f, 100f);
    }

    // =========================================================================
    // Employment matching
    // =========================================================================

    /// <summary>
    /// Match unemployed citizens to available jobs based on education
    /// and commute tolerance. Citizens with higher education get priority
    /// for higher-paying jobs.
    /// </summary>
    public void MatchEmployment(WorldState state)
    {
        var hh = state.Households;
        var buildings = state.Buildings;

        // Build list of available jobs (buildings with open occupant slots)
        var availableJobs = new List<(int buildingId, int openSlots, int requiredEducation)>();
        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b)) continue;
            if (buildings.State[b] != 1) continue; // Only operational buildings

            if (!IsEmploymentBuilding(state, b))
                continue;

            byte zoneType = GetEmploymentZoneType(state, b);
            if (zoneType == 0) continue;
                int open = buildings.MaxOccupants[b] - buildings.Occupants[b];
                if (open > 0)
                {
                    int reqEdu = zoneType switch
                    {
                        5 => 3, // Office needs university
                        3 => 1, // Commercial needs basic
                        4 => 1, // Industrial needs basic
                        6 => 2, // Mixed-use needs advanced
                        _ => 0,
                    };
                    availableJobs.Add((b, open, reqEdu));
                }
        }

        // Sort jobs by required education descending (best jobs first)
        availableJobs.Sort((a, b) => b.requiredEducation.CompareTo(a.requiredEducation));

        // Match unemployed households to jobs
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if ((hh.Flags[i] & 4) == 0) continue; // Not unemployed
            if (hh.AgeGroup[i] == 2) continue; // Retired

            byte edu = hh.Education[i];

            for (int j = 0; j < availableJobs.Count; j++)
            {
                var (bId, open, reqEdu) = availableJobs[j];
                if (edu < reqEdu) continue;
                if (open <= 0) continue;

                // Check commute tolerance: distance from home to work building
                ushort homeBuilding = hh.HomeBuildingId[i];
                if (homeBuilding == 0) continue;

                float distance = CalculateBuildingDistance(state, homeBuilding, bId);
                float maxCommute = 50f + edu * 10f; // Educated tolerate longer commutes

                if (distance <= maxCommute)
                {
                    hh.WorkBuildingId[i] = (ushort)bId;
                    hh.Flags[i] = (byte)(hh.Flags[i] & ~4); // Clear unemployed flag
                    state.Buildings.Occupants[bId]++;
                    availableJobs[j] = (bId, open - 1, reqEdu);

                    // Set income based on job type
                    hh.Income[i] = CalculateJobIncome(state, bId, edu);
                    break;
                }
            }
        }
    }

    // =========================================================================
    // Wealth class transitions
    // =========================================================================

    /// <summary>
    /// Update wealth classes based on income vs cost of living, education,
    /// and employment status. Job loss triggers downgrade, promotion triggers upgrade.
    /// </summary>
    public void UpdateWealthClasses(WorldState state)
    {
        var hh = state.Households;
        float baseCostOfLiving = CalculateBaseCostOfLiving(state);

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            float income = hh.Income[i];
            float ratio = baseCostOfLiving > 0 ? income / baseCostOfLiving : 0f;

            // Education bonus: educated households climb faster
            float eduBonus = hh.Education[i] * 0.1f;
            ratio += eduBonus;

            // Unemployed penalty
            if ((hh.Flags[i] & 4) != 0)
            {
                ratio *= 0.3f;
            }

            byte newWealth;
            if (ratio < WealthThreshold_Poor) newWealth = 0;
            else if (ratio < WealthThreshold_Lower) newWealth = 1;
            else if (ratio < WealthThreshold_Middle) newWealth = 2;
            else if (ratio < WealthThreshold_Upper) newWealth = 3;
            else newWealth = 4;

            // Gradual transition: only move one step per month
            byte current = hh.WealthLevel[i];
            if (newWealth > current)
                hh.WealthLevel[i] = (byte)(current + 1);
            else if (newWealth < current)
                hh.WealthLevel[i] = (byte)(current - 1);

            // Update savings
            int surplus = hh.Income[i] - (int)(baseCostOfLiving * (1f + hh.WealthLevel[i] * 0.2f));
            hh.Savings[i] = Math.Max(0, hh.Savings[i] + surplus);
        }
    }

    // =========================================================================
    // Birth calculation
    // =========================================================================

    /// <summary>
    /// Calculate births for the month.
    /// births = base_rate * healthcare_mod * housing_mod * wealth_mod * cultural_mod
    /// Base rate: 0.015/month per household with adults 18-45.
    /// </summary>
    public int CalculateBirths(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);
        var hh = state.Households;
        float healthcareMod = GetHealthcareModifier(state);
        float housingMod = GetHousingAvailabilityModifier(state);
        float culturalMod = GetCulturalBirthModifier(state);

        int births = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            int age = _headAge[i];
            if (age < MinFertileAge || age > MaxFertileAge) continue;

            float wealthMod = hh.WealthLevel[i] switch
            {
                0 => 1.3f,   // Poor: higher birth rate
                1 => 1.1f,
                2 => 1.0f,
                3 => 0.8f,
                4 => 0.6f,   // Wealthy: lower birth rate
                _ => 1.0f,
            };

            float birthProbability = BaseBirthRatePerMonth *
                                     healthcareMod * housingMod * wealthMod * culturalMod;

            if (NextRandomFloat() < birthProbability)
            {
                // Add a family member
                if (hh.MemberCount[i] < 8)
                {
                    hh.MemberCount[i]++;
                    births++;
                }
            }
        }

        state.Population += births;
        return births;
    }

    // =========================================================================
    // Death calculation
    // =========================================================================

    /// <summary>
    /// Calculate deaths for the month by age bracket.
    /// Rates: 0-40: 0.001, 40-60: 0.005, 60-75: 0.02, 75-85: 0.08, 85+: 0.20
    /// Modified by healthcare (0.5-2.0), pollution (1.0-1.5), happiness (1.0-1.3).
    /// </summary>
    public int CalculateDeaths(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);
        var hh = state.Households;
        float healthcareMod = GetHealthcareDeathModifier(state);
        float pollutionMod = GetPollutionDeathModifier(state);
        float happinessMod = GetHappinessDeathModifier(state);

        int deaths = 0;
        var toRemove = new List<int>();

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            int age = _headAge[i];
            float baseRate = age switch
            {
                < 40 => DeathRate_0_40,
                < 60 => DeathRate_40_60,
                < 75 => DeathRate_60_75,
                < 85 => DeathRate_75_85,
                _ => DeathRate_85Plus,
            };

            float deathProbability = baseRate * healthcareMod * pollutionMod * happinessMod;

            if (NextRandomFloat() < deathProbability)
            {
                if (hh.MemberCount[i] > 1)
                {
                    // Lose a family member but household survives
                    hh.MemberCount[i]--;
                    deaths++;
                }
                else
                {
                    // Last member dies — remove household
                    toRemove.Add(i);
                    deaths++;
                }
            }
        }

        // Remove dead households
        foreach (int idx in toRemove)
        {
            RemoveHousehold(state, idx);
        }

        state.Population -= deaths;
        if (state.Population < 0) state.Population = 0;

        return deaths;
    }

    // =========================================================================
    // Immigration
    // =========================================================================

    /// <summary>
    /// Calculate immigration for the month.
    /// rate = base * job_availability * housing_availability * reputation * tax_modifier
    /// Base: 5 households/month, scales with city size.
    /// </summary>
    public int CalculateImmigration(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);

        float jobAvailability = GetJobAvailabilityFactor(state);
        float housingAvailability = GetHousingAvailabilityModifier(state);
        float reputation = Math.Clamp(state.Happiness * 2f, 0f, 2f); // 0-2
        float taxMod = GetTaxAttractivenessModifier(state);

        // Scale base with city size (log scale)
        float sizeScale = 1f + (float)Math.Log(Math.Max(1, state.Population / 1000f), 2);
        float rawRate = BaseImmigrationPerMonth * sizeScale *
                        jobAvailability * housingAvailability * reputation * taxMod *
                        GetRentAttractivenessModifier();

        int count = (int)rawRate;
        // Fractional part becomes probability for +1
        if (NextRandomFloat() < (rawRate - count))
            count++;

        int actualImmigrants = 0;
        for (int n = 0; n < count; n++)
        {
            int slot = state.Households.Allocate();
            if (slot < 0) break; // Pool full

            // Initialize new household
            var hh = state.Households;
            hh.MemberCount[slot] = (byte)(2 + NextRandom(3)); // 2-4 members
            hh.AgeGroup[slot] = 1; // Working age
            _headAge[slot] = (byte)(20 + NextRandom(25)); // 20-44
            hh.Education[slot] = (byte)NextRandom(4); // 0-3
            hh.WealthLevel[slot] = (byte)Math.Clamp(NextRandom(3), 0, 4); // 0-2 initially
            hh.Happiness[slot] = 128; // Neutral
            hh.HealthSatisfaction[slot] = 128;
            hh.SafetySatisfaction[slot] = 128;
            hh.TransportSatisfaction[slot] = 128;
            hh.LeisureSatisfaction[slot] = 128;
            hh.Flags[slot] = (byte)(1 | 4); // Active + unemployed
            hh.Income[slot] = 0;
            hh.Savings[slot] = 500 + NextRandom(2000);
            _monthsUnhappy[slot] = 0;

            // Assign to available housing
            AssignHousing(state, slot);

            actualImmigrants++;
            state.Population += hh.MemberCount[slot];
        }

        return actualImmigrants;
    }

    // =========================================================================
    // Emigration
    // =========================================================================

    /// <summary>
    /// Calculate emigration for the month.
    /// Households leave when satisfaction is below 30 for 6+ consecutive months.
    /// Educated households leave faster (more options).
    /// </summary>
    public int CalculateEmigration(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);
        var hh = state.Households;
        var toRemove = new List<int>();

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            bool isUnhappy = hh.Happiness[i] < EmigrationSatisfactionThreshold;
            if (isUnhappy)
            {
                _monthsUnhappy[i]++;

                // Educated leave faster: reduce threshold by education level
                int threshold = EmigrationMonthsUnhappy - hh.Education[i];
                threshold = Math.Max(1, threshold);

                if (_monthsUnhappy[i] >= threshold)
                {
                    // Brain drain: educated are more likely to leave
                    float leaveProbability = 0.5f + hh.Education[i] * 0.15f;

                    // Check for matching jobs — if no job matches education, higher chance
                    if ((hh.Flags[i] & 4) != 0) // Unemployed
                        leaveProbability += 0.2f;

                    if (_monthsHighBurden[i] >= HighRentBurdenEmigrationMonths)
                        leaveProbability += HighRentBurdenEmigrationBonus;

                    if (NextRandomFloat() < leaveProbability)
                    {
                        toRemove.Add(i);
                    }
                }
            }
            else
            {
                _monthsUnhappy[i] = 0;
            }
        }

        int emigrated = 0;
        foreach (int idx in toRemove)
        {
            state.Population -= state.Households.MemberCount[idx];
            RemoveHousehold(state, idx);
            emigrated++;
        }

        if (state.Population < 0) state.Population = 0;
        return emigrated;
    }

    // =========================================================================
    // Aging
    // =========================================================================

    /// <summary>
    /// Age all household heads by 1 year per game year (called monthly,
    /// so increment age on the birth month — approximated as January).
    /// Citizens retire at 65.
    /// </summary>
    private void AgeHouseholds(WorldState state)
    {
        var hh = state.Households;

        // Age once per year (when month == 1)
        if (state.Month != 1) return;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            if (_headAge[i] < 255)
                _headAge[i]++;

            int age = _headAge[i];

            // Update age group
            if (age < 18)
                hh.AgeGroup[i] = 0; // Young
            else if (age < RetirementAge)
                hh.AgeGroup[i] = 1; // Working
            else
            {
                // Retirement
                if (hh.AgeGroup[i] != 2)
                {
                    hh.AgeGroup[i] = 2; // Retired
                    // Leave job
                    if (hh.WorkBuildingId[i] != 0)
                    {
                        int workId = hh.WorkBuildingId[i];
                        if (workId < state.Buildings.Capacity && state.Buildings.IsActive(workId))
                        {
                            if (state.Buildings.Occupants[workId] > 0)
                                state.Buildings.Occupants[workId]--;
                        }
                        hh.WorkBuildingId[i] = 0;
                    }
                    hh.Income[i] = hh.Income[i] / 3; // Pension: ~1/3 of working income
                    hh.Flags[i] = (byte)(hh.Flags[i] & ~4); // Not "unemployed" — retired
                }
            }
        }
    }

    // =========================================================================
    // Satisfaction component calculations
    // =========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateEmploymentSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        if (hh.AgeGroup[idx] == 2) return 70f; // Retired: moderate baseline
        if ((hh.Flags[idx] & 4) != 0) return 10f; // Unemployed: very low
        // Employed: scale by income vs city average
        float avgIncome = CalculateAverageIncome(state);
        if (avgIncome <= 0) return 50f;
        float ratio = hh.Income[idx] / avgIncome;
        return Math.Clamp(ratio * 50f, 0f, 100f);
    }

    private float CalculateHousingSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[idx];

        float physicalScore;
        if (homeId == 0)
        {
            physicalScore = 5f;
        }
        else if (homeId >= state.Buildings.Capacity || !state.Buildings.IsActive(homeId))
        {
            physicalScore = 20f;
        }
        else
        {
            float condition = state.Buildings.Condition[homeId] / 255f;
            float level = state.Buildings.Level[homeId] / 5f;
            float overcrowding = state.Buildings.MaxOccupants[homeId] > 0
                ? 1f - (float)state.Buildings.Occupants[homeId] / state.Buildings.MaxOccupants[homeId] * 0.5f
                : 0.5f;

            physicalScore = Math.Clamp(condition * 40f + level * 30f + overcrowding * 30f, 0f, 100f);
        }

        float burden = GetHouseholdRentBurden(state, idx);
        float affordScore = Math.Clamp(1f - burden / RentBurdenAffordThreshold, 0f, 1f) * 100f;
        float housingSat = PhysicalHousingWeight * physicalScore + AffordHousingWeight * affordScore;

        if (burden > RentBurdenAffordThreshold)
            housingSat = Math.Max(0f, housingSat - 15f);

        return Math.Clamp(housingSat, 0f, 100f);
    }

    private float CalculateCommuteSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        if (hh.AgeGroup[idx] == 2) return 80f; // Retired: no commute
        if (hh.WorkBuildingId[idx] == 0) return 50f; // No work

        float distance = CalculateBuildingDistance(state, hh.HomeBuildingId[idx], hh.WorkBuildingId[idx]);
        // Under 10 tiles = excellent, 50+ = terrible
        return Math.Clamp(100f - distance * 1.8f, 0f, 100f);
    }

    private float CalculateServicesSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[idx];
        if (homeId == 0 || homeId >= state.Buildings.Capacity) return 10f;

        int gx = state.Buildings.GridX[homeId];
        int gy = state.Buildings.GridY[homeId];
        if (!state.Tiles.InBounds(gx, gy)) return 10f;

        int tileIdx = state.Tiles.Index(gx, gy);
        float fire = state.Tiles.GetFireCoverage(tileIdx) / 3f;
        float police = state.Tiles.GetPoliceCoverage(tileIdx) / 3f;
        float health = state.Tiles.GetHealthCoverage(tileIdx) / 3f;
        float edu = state.Tiles.GetEducationCoverage(tileIdx) / 3f;

        return Math.Clamp((fire + police + health + edu) * 25f, 0f, 100f);
    }

    private float CalculateSafetySatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[idx];
        if (homeId == 0 || homeId >= state.Buildings.Capacity) return 30f;

        int gx = state.Buildings.GridX[homeId];
        int gy = state.Buildings.GridY[homeId];
        if (!state.Tiles.InBounds(gx, gy)) return 30f;

        int tileIdx = state.Tiles.Index(gx, gy);
        float crime = state.Tiles.Crime[tileIdx];
        return Math.Clamp(100f * (1f - crime), 0f, 100f);
    }

    private float CalculateEnvironmentSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[idx];
        if (homeId == 0 || homeId >= state.Buildings.Capacity) return 50f;

        int gx = state.Buildings.GridX[homeId];
        int gy = state.Buildings.GridY[homeId];
        if (!state.Tiles.InBounds(gx, gy)) return 50f;

        int tileIdx = state.Tiles.Index(gx, gy);
        float pollution = state.Tiles.Pollution[tileIdx];
        float noise = state.Tiles.Noise[tileIdx];
        float desirability = (state.Tiles.Desirability[tileIdx] + 1f) / 2f; // -1..1 -> 0..1

        return Math.Clamp((1f - pollution) * 40f + (1f - noise) * 30f + desirability * 30f, 0f, 100f);
    }

    private float CalculateEducationSatisfaction(WorldState state, int idx)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[idx];
        if (homeId == 0 || homeId >= state.Buildings.Capacity) return 30f;

        int gx = state.Buildings.GridX[homeId];
        int gy = state.Buildings.GridY[homeId];
        if (!state.Tiles.InBounds(gx, gy)) return 30f;

        int tileIdx = state.Tiles.Index(gx, gy);
        float eduCoverage = state.Tiles.GetEducationCoverage(tileIdx) / 3f;
        return Math.Clamp(eduCoverage * 100f, 0f, 100f);
    }

    private float CalculateTaxFairnessSatisfaction(WorldState state, int idx)
    {
        // Higher tax rates reduce satisfaction, but wealthy tolerate them more
        float taxRate = state.PropertyTaxRate;
        float wealthTolerance = 1f + state.Households.WealthLevel[idx] * 0.15f;
        float fairness = 1f - (taxRate / wealthTolerance);
        return Math.Clamp(fairness * 100f, 0f, 100f);
    }

    private float CalculateCulturalSatisfaction(WorldState state, int idx)
    {
        // CulturalDna[3]: Isolation vs Cosmopolitan
        // More cosmopolitan cities attract diverse immigrants
        float cosmopolitan = (state.CulturalDna[3] + 1f) / 2f; // -1..1 -> 0..1
        float innovation = (state.CulturalDna[0] + 1f) / 2f;
        return Math.Clamp((cosmopolitan * 50f + innovation * 50f), 0f, 100f);
    }

    // =========================================================================
    // Housing market — rent burden (Cathedral P2.3)
    // =========================================================================

    private void AssignUnhousedHouseholds(WorldState state)
    {
        var hh = state.Households;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.HomeBuildingId[i] != 0) continue;
            AssignHousing(state, i);
        }
    }

    private void UpdateRentBurdenRollup(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);

        float supplyFactor = CalculateSupplyFactor(state);
        float goodsFactor = CalculateGoodsFactor(state);
        float marketBaseRent = CalculateMarketBaseRent(state) * supplyFactor * goodsFactor;

        var hh = state.Households;
        double burdenSum = 0d;
        int burdenCount = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            float burden = CalculateHouseholdRentBurden(state, i, supplyFactor, goodsFactor, marketBaseRent);
            _rentBurden[i] = burden;

            if (burden > HighRentBurdenThreshold)
                _monthsHighBurden[i]++;
            else
                _monthsHighBurden[i] = 0;

            if (hh.HomeBuildingId[i] != 0)
            {
                burdenSum += burden;
                burdenCount++;
            }
            else if (burden > 0f)
            {
                // Homeless households still face market rent pressure (included in city mean).
                burdenSum += burden;
                burdenCount++;
            }
        }

        MeanRentBurden = burdenCount > 0 ? (float)(burdenSum / burdenCount) : 0f;
        state.MeanRentBurden = MeanRentBurden;
        state.ResidentialVacancy = HousingHeraldSystem.CalculateCityVacancy(state);
    }

    /// <summary>Refresh housing snapshot fields before WASM / render export.</summary>
    public void RefreshHousingSnapshotMetrics(WorldState state) => UpdateRentBurdenRollup(state);

    /// <summary>
    /// Assign home/work buildings after bulk household seed (starter city, restore).
    /// Cathedral P4.1 — ensures commuters have explicit O-D building IDs.
    /// </summary>
    public void BootstrapCommuterAssignments(WorldState state)
    {
        EnsureCapacity(state.Households.Capacity);
        ClearBuildingOccupantCounts(state);

        var hh = state.Households;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.AgeGroup[i] != 1) continue;
            if (hh.WorkBuildingId[i] == 0)
                hh.Flags[i] = (byte)(hh.Flags[i] | 4);
        }

        AssignUnhousedHouseholds(state);
        MatchEmployment(state);
        AssignRemainingCommuters(state);
    }

    /// <summary>
    /// Assign any still-unemployed working households to open jobs (ignores commute distance).
    /// Used only during bootstrap so starter cities reach P4.1 coverage targets.
    /// </summary>
    private void AssignRemainingCommuters(WorldState state)
    {
        var hh = state.Households;
        var buildings = state.Buildings;
        var openJobs = new List<(int buildingId, int openSlots, int requiredEducation)>();

        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b) || buildings.State[b] != 1) continue;

            if (!IsEmploymentBuilding(state, b)) continue;

            byte zoneType = GetEmploymentZoneType(state, b);
            if (zoneType == 0) continue;

            int open = buildings.MaxOccupants[b] - buildings.Occupants[b];
            if (open <= 0) continue;

            int reqEdu = zoneType switch
            {
                5 => 3,
                3 => 1,
                4 => 1,
                6 => 2,
                _ => 0,
            };
            openJobs.Add((b, open, reqEdu));
        }

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.AgeGroup[i] != 1) continue;
            if (hh.WorkBuildingId[i] != 0) continue;
            if (hh.HomeBuildingId[i] == 0) continue;
            if ((hh.Flags[i] & 4) == 0) continue;

            byte edu = hh.Education[i];
            for (int j = 0; j < openJobs.Count; j++)
            {
                var (bId, open, reqEdu) = openJobs[j];
                if (edu < reqEdu || open <= 0) continue;

                hh.WorkBuildingId[i] = (ushort)bId;
                hh.Flags[i] = (byte)(hh.Flags[i] & ~4);
                buildings.Occupants[bId]++;
                hh.Income[i] = CalculateJobIncome(state, bId, edu);
                openJobs[j] = (bId, open - 1, reqEdu);
                break;
            }
        }
    }

    private static void ClearBuildingOccupantCounts(WorldState state)
    {
        Array.Clear(state.Buildings.Occupants, 0, state.Buildings.Capacity);
    }

    /// <summary>
    /// Recompute mean rent burden from household rent/income without mutating per-household trackers.
    /// Characterization tests use this to verify snapshot export matches rollup math.
    /// </summary>
    public float AuditMeanRentBurden(WorldState state)
    {
        float supplyFactor = CalculateSupplyFactor(state);
        float goodsFactor = CalculateGoodsFactor(state);
        float marketBaseRent = CalculateMarketBaseRent(state) * supplyFactor * goodsFactor;

        var hh = state.Households;
        double burdenSum = 0d;
        int burdenCount = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            float burden = CalculateHouseholdRentBurden(
                state, i, supplyFactor, goodsFactor, marketBaseRent);

            if (hh.HomeBuildingId[i] != 0)
            {
                burdenSum += burden;
                burdenCount++;
            }
            else if (burden > 0f)
            {
                burdenSum += burden;
                burdenCount++;
            }
        }

        return burdenCount > 0 ? (float)(burdenSum / burdenCount) : 0f;
    }

    private float GetHouseholdRentBurden(WorldState state, int householdIndex)
    {
        float supplyFactor = CalculateSupplyFactor(state);
        float goodsFactor = CalculateGoodsFactor(state);
        float marketBaseRent = CalculateMarketBaseRent(state) * supplyFactor * goodsFactor;
        return CalculateHouseholdRentBurden(state, householdIndex, supplyFactor, goodsFactor, marketBaseRent);
    }

    private float CalculateHouseholdRentBurden(
        WorldState state,
        int householdIndex,
        float supplyFactor,
        float goodsFactor,
        float marketBaseRent)
    {
        var hh = state.Households;
        float monthlyRent = CalculateHouseholdMonthlyRent(
            state, householdIndex, supplyFactor, goodsFactor, marketBaseRent);
        float monthlyIncome = Math.Max(hh.Income[householdIndex], 1);
        return monthlyRent / monthlyIncome;
    }

    private float CalculateHouseholdMonthlyRent(
        WorldState state,
        int householdIndex,
        float supplyFactor,
        float goodsFactor,
        float marketBaseRent)
    {
        var hh = state.Households;
        ushort homeId = hh.HomeBuildingId[householdIndex];
        if (homeId == 0)
            return marketBaseRent;

        if (homeId >= state.Buildings.Capacity || !state.Buildings.IsActive(homeId))
            return marketBaseRent;

        int gx = state.Buildings.GridX[homeId];
        int gy = state.Buildings.GridY[homeId];
        if (!state.Tiles.InBounds(gx, gy))
            return marketBaseRent;

        float baseRent = CalculateBaseRentAtTile(state, gx, gy);
        float levelMod = 0.85f + 0.05f * state.Buildings.Level[homeId];
        return baseRent * supplyFactor * goodsFactor * levelMod;
    }

    private static float CalculateBaseRentAtTile(WorldState state, int tileX, int tileY)
    {
        float landValue = state.Tiles.LandValue[state.Tiles.Index(tileX, tileY)];
        return BaseRentConstant + BaseRentLandMultiplier * landValue;
    }

    private static float CalculateMarketBaseRent(WorldState state)
    {
        float totalLandValue = 0f;
        int count = 0;
        var tiles = state.Tiles;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.ZoneType[i] == 0) continue;
            totalLandValue += tiles.LandValue[i];
            count++;
        }

        float avgLandValue = count > 0 ? totalLandValue / count : 0.35f;
        return BaseRentConstant + BaseRentLandMultiplier * avgLandValue;
    }

    private static float CalculateSupplyFactor(WorldState state)
    {
        int totalCapacity = 0;
        int totalOccupants = 0;
        var buildings = state.Buildings;

        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b)) continue;
            byte zone = GetBuildingZoneTypeStatic(state, b);
            if (zone is 1 or 2 or 6)
            {
                totalCapacity += buildings.MaxOccupants[b];
                totalOccupants += buildings.Occupants[b];
            }
        }

        if (totalCapacity == 0)
            return 2f;

        float vacancy = 1f - (float)totalOccupants / totalCapacity;
        return Math.Clamp(vacancy / VacancyNormalization, 0.5f, 2f);
    }

    private static float CalculateGoodsFactor(WorldState state) =>
        1f + GoodsShortageRentWeight * Math.Clamp(state.GoodsShortageIndex, 0f, 1f);

    private float GetRentAttractivenessModifier() =>
        Math.Clamp(RentAttractivenessBase - MeanRentBurden, 0.3f, 1.5f);

    private static byte GetBuildingZoneTypeStatic(WorldState state, int buildingId)
    {
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return 0;

        int gx = state.Buildings.GridX[buildingId];
        int gy = state.Buildings.GridY[buildingId];
        if (!state.Tiles.InBounds(gx, gy)) return 0;

        byte tileZone = state.Tiles.ZoneType[state.Tiles.Index(gx, gy)];
        if (tileZone is 1 or 2 or 6)
            return tileZone;

        ushort typeId = state.Buildings.TypeId[buildingId];
        byte typeZone = typeId switch
        {
            >= 100 and < 300 => 1,
            >= 300 and < 400 => 3,
            >= 400 and < 500 => 4,
            _ => (byte)0,
        };

        return typeZone != 0 ? typeZone : tileZone;
    }

    // =========================================================================
    // Modifier helpers
    // =========================================================================

    /// <summary>Get healthcare modifier for births (0.5-1.5).</summary>
    private float GetHealthcareModifier(WorldState state)
    {
        // Average health coverage across populated tiles
        float avgHealth = GetAverageHealthCoverage(state);
        return 0.5f + avgHealth * 1f; // 0.5 (no healthcare) to 1.5 (full)
    }

    /// <summary>Get healthcare modifier for deaths (0.5-2.0). Lower = better healthcare.</summary>
    private float GetHealthcareDeathModifier(WorldState state)
    {
        float avgHealth = GetAverageHealthCoverage(state);
        return 2f - avgHealth * 1.5f; // 2.0 (no healthcare) to 0.5 (full)
    }

    /// <summary>Get pollution modifier for deaths (1.0-1.5).</summary>
    private float GetPollutionDeathModifier(WorldState state)
    {
        float avgPollution = GetAveragePollution(state);
        return 1f + avgPollution * 0.5f;
    }

    /// <summary>Get happiness modifier for deaths (1.0-1.3). Unhappy = more deaths.</summary>
    private float GetHappinessDeathModifier(WorldState state)
    {
        return 1.3f - state.Happiness * 0.3f; // 1.3 (happiness=0) to 1.0 (happiness=1)
    }

    private float GetHousingAvailabilityModifier(WorldState state)
    {
        int totalCapacity = 0;
        int totalOccupants = 0;
        var buildings = state.Buildings;

        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b)) continue;
            byte zone = GetBuildingZoneType(state, b);
            if (zone is 1 or 2 or 6) // Residential low/high, mixed-use
            {
                totalCapacity += buildings.MaxOccupants[b];
                totalOccupants += buildings.Occupants[b];
            }
        }

        if (totalCapacity == 0) return 0.1f;
        float vacancy = 1f - (float)totalOccupants / totalCapacity;
        return Math.Clamp(vacancy * 3f, 0.1f, 2f);
    }

    private float GetJobAvailabilityFactor(WorldState state)
    {
        int totalJobs = 0;
        int filledJobs = 0;
        var buildings = state.Buildings;

        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b)) continue;
            if (buildings.State[b] != 1) continue;

            byte zone = GetBuildingZoneType(state, b);
            if (zone is 3 or 4 or 5 or 6)
            {
                totalJobs += buildings.MaxOccupants[b];
                filledJobs += buildings.Occupants[b];
            }
        }

        if (totalJobs == 0) return 0.1f;
        float openRatio = 1f - (float)filledJobs / totalJobs;
        return Math.Clamp(openRatio * 3f, 0.1f, 2f);
    }

    private float GetTaxAttractivenessModifier(WorldState state)
    {
        // Lower taxes attract more immigrants
        float avgTax = (state.PropertyTaxRate + state.CommercialTaxRate + state.IndustrialTaxRate) / 3f;
        return Math.Clamp(2f - avgTax * 10f, 0.3f, 2f);
    }

    private float GetCulturalBirthModifier(WorldState state)
    {
        // Traditional cultures have higher birth rates
        float tradition = -state.CulturalDna[0]; // -1 = traditional (+1 births), +1 = innovative (-births)
        return Math.Clamp(1f + tradition * 0.3f, 0.5f, 1.5f);
    }

    // =========================================================================
    // Aggregate helpers
    // =========================================================================

    private float GetAverageHealthCoverage(WorldState state)
    {
        long total = 0;
        int count = 0;
        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] != 0)
            {
                total += tiles.GetHealthCoverage(i);
                count++;
            }
        }
        return count > 0 ? (float)total / count / 3f : 0f;
    }

    private float GetAveragePollution(WorldState state)
    {
        float total = 0;
        int count = 0;
        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] != 0)
            {
                total += tiles.Pollution[i];
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    private float CalculateAverageIncome(WorldState state)
    {
        long totalIncome = 0;
        int count = 0;
        var hh = state.Households;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            totalIncome += hh.Income[i];
            count++;
        }
        return count > 0 ? (float)totalIncome / count : 1000f;
    }

    private float CalculateBaseCostOfLiving(WorldState state)
    {
        // Base cost scales with land value and tax rate
        float avgLandValue = 0f;
        int count = 0;
        var tiles = state.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.BuildingId[i] != 0)
            {
                avgLandValue += tiles.LandValue[i];
                count++;
            }
        }
        if (count > 0) avgLandValue /= count;

        return 500f + avgLandValue * 2000f + state.PropertyTaxRate * 1000f;
    }

    private byte GetBuildingZoneType(WorldState state, int buildingId) =>
        GetBuildingZoneTypeStatic(state, buildingId);

    private static bool IsResidentialBuilding(WorldState state, int buildingId)
    {
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return false;
        if (!state.Buildings.IsActive(buildingId)) return false;

        byte tileZone = GetTileZone(state, buildingId);
        if (tileZone is 1 or 2 or 6) return true;

        ushort typeId = state.Buildings.TypeId[buildingId];
        return typeId is >= 100 and < 300;
    }

    private static bool IsEmploymentBuilding(WorldState state, int buildingId)
    {
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return false;
        if (!state.Buildings.IsActive(buildingId)) return false;

        ushort typeId = state.Buildings.TypeId[buildingId];
        if (typeId is >= 300 and < 500) return true;

        byte tileZone = GetTileZone(state, buildingId);
        return tileZone is 3 or 4 or 5 or 6;
    }

    private static byte GetEmploymentZoneType(WorldState state, int buildingId)
    {
        ushort typeId = state.Buildings.TypeId[buildingId];
        if (typeId is >= 300 and < 400) return 3;
        if (typeId is >= 400 and < 500) return 4;

        byte tileZone = GetTileZone(state, buildingId);
        return tileZone is 3 or 4 or 5 or 6 ? tileZone : (byte)0;
    }

    private static byte GetTileZone(WorldState state, int buildingId)
    {
        int gx = state.Buildings.GridX[buildingId];
        int gy = state.Buildings.GridY[buildingId];
        if (!state.Tiles.InBounds(gx, gy)) return 0;
        return state.Tiles.ZoneType[state.Tiles.Index(gx, gy)];
    }

    private float CalculateBuildingDistance(WorldState state, int buildingA, int buildingB)
    {
        if (buildingA < 0 || buildingA >= state.Buildings.Capacity ||
            buildingB < 0 || buildingB >= state.Buildings.Capacity)
            return float.MaxValue;

        int ax = state.Buildings.GridX[buildingA];
        int ay = state.Buildings.GridY[buildingA];
        int bx = state.Buildings.GridX[buildingB];
        int by = state.Buildings.GridY[buildingB];

        float dx = ax - bx;
        float dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private int CalculateJobIncome(WorldState state, int buildingId, byte education)
    {
        byte zone = GetBuildingZoneType(state, buildingId);
        int baseIncome = zone switch
        {
            3 => 1500,   // Commercial
            4 => 1200,   // Industrial
            5 => 2500,   // Office
            6 => 2000,   // Mixed-use
            _ => 1000,
        };

        // Education multiplier
        float eduMult = 1f + education * 0.3f;
        // Building level multiplier
        float levelMult = 1f + (state.Buildings.Level[buildingId] - 1) * 0.15f;

        return (int)(baseIncome * eduMult * levelMult);
    }

    private void AssignHousing(WorldState state, int householdIndex)
    {
        var buildings = state.Buildings;

        for (int b = 0; b < buildings.Capacity; b++)
        {
            if (!buildings.IsActive(b)) continue;
            if (buildings.State[b] != 1) continue;

            if (!IsResidentialBuilding(state, b)) continue;

            if (buildings.Occupants[b] < buildings.MaxOccupants[b])
            {
                state.Households.HomeBuildingId[householdIndex] = (ushort)b;
                buildings.Occupants[b]++;
                return;
            }
        }
        // No housing found: household remains homeless (HomeBuildingId stays 0)
    }

    private void RemoveHousehold(WorldState state, int idx)
    {
        var hh = state.Households;

        // Free building occupant slots
        if (hh.HomeBuildingId[idx] != 0)
        {
            int homeId = hh.HomeBuildingId[idx];
            if (homeId < state.Buildings.Capacity && state.Buildings.IsActive(homeId))
            {
                if (state.Buildings.Occupants[homeId] > 0)
                    state.Buildings.Occupants[homeId]--;
            }
        }

        if (hh.WorkBuildingId[idx] != 0)
        {
            int workId = hh.WorkBuildingId[idx];
            if (workId < state.Buildings.Capacity && state.Buildings.IsActive(workId))
            {
                if (state.Buildings.Occupants[workId] > 0)
                    state.Buildings.Occupants[workId]--;
            }
        }

        _monthsUnhappy[idx] = 0;
        _monthsHighBurden[idx] = 0;
        _rentBurden[idx] = 0f;
        _headAge[idx] = 0;
        hh.Free(idx);
    }

    private void UpdateAverageHappiness(WorldState state)
    {
        long total = 0;
        int count = 0;
        var hh = state.Households;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            total += hh.Happiness[i];
            count++;
        }

        state.Happiness = count > 0 ? (float)total / count / 255f : 0.5f;
    }

    private void UpdatePopulationCount(WorldState state)
    {
        int pop = 0;
        var hh = state.Households;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (hh.IsActive(i))
                pop += hh.MemberCount[i];
        }
        state.Population = pop;
    }

    // =========================================================================
    // Capacity management
    // =========================================================================

    private void EnsureCapacity(int capacity)
    {
        if (_monthsUnhappyCapacity < capacity)
        {
            var newArr = new byte[capacity];
            if (_monthsUnhappy.Length > 0)
                Array.Copy(_monthsUnhappy, newArr, Math.Min(_monthsUnhappy.Length, capacity));
            _monthsUnhappy = newArr;
            _monthsUnhappyCapacity = capacity;
        }

        if (_monthsHighBurdenCapacity < capacity)
        {
            var newArr = new byte[capacity];
            if (_monthsHighBurden.Length > 0)
                Array.Copy(_monthsHighBurden, newArr, Math.Min(_monthsHighBurden.Length, capacity));
            _monthsHighBurden = newArr;
            _monthsHighBurdenCapacity = capacity;
        }

        if (_rentBurdenCapacity < capacity)
        {
            var newArr = new float[capacity];
            if (_rentBurden.Length > 0)
                Array.Copy(_rentBurden, newArr, Math.Min(_rentBurden.Length, capacity));
            _rentBurden = newArr;
            _rentBurdenCapacity = capacity;
        }

        if (_headAgeCapacity < capacity)
        {
            var newArr = new byte[capacity];
            if (_headAge.Length > 0)
                Array.Copy(_headAge, newArr, Math.Min(_headAge.Length, capacity));
            _headAge = newArr;
            _headAgeCapacity = capacity;
        }
    }

    // =========================================================================
    // Deterministic RNG (xorshift32)
    // =========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint NextRandomRaw()
    {
        uint x = _rngState;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _rngState = x;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float NextRandomFloat()
    {
        return (NextRandomRaw() & 0x7FFFFF) / (float)0x800000; // [0, 1)
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int NextRandom(int exclusiveMax)
    {
        if (exclusiveMax <= 0) return 0;
        return (int)(NextRandomRaw() % (uint)exclusiveMax);
    }

    // =========================================================================
    // Public accessors for testing
    // =========================================================================

    /// <summary>Get the head age of a household (for testing/debugging).</summary>
    public int GetHeadAge(int householdIndex)
    {
        EnsureCapacity(householdIndex + 1);
        return _headAge[householdIndex];
    }

    /// <summary>Set the head age of a household (for testing/setup).</summary>
    public void SetHeadAge(int householdIndex, byte age)
    {
        EnsureCapacity(householdIndex + 1);
        _headAge[householdIndex] = age;
    }

    // =========================================================================
    // Commuter O-D audit (Cathedral P4.1)
    // =========================================================================

    /// <summary>Commuter assignment rollup for snapshot export and tests.</summary>
    public readonly struct CommuterAudit
    {
        /// <summary>Working-age households with a workplace building.</summary>
        public int WorkingCommuters { get; init; }
        /// <summary>Working commuters with valid active home + work building IDs.</summary>
        public int AssignedCommuters { get; init; }
        public float Coverage =>
            WorkingCommuters > 0 ? AssignedCommuters / (float)WorkingCommuters : 0f;
    }

    /// <summary>Aggregated home→work tile pair for traffic / debug export.</summary>
    public readonly struct CommuteOdSampleRow
    {
        public int HomeTileX { get; init; }
        public int HomeTileZ { get; init; }
        public int WorkTileX { get; init; }
        public int WorkTileZ { get; init; }
        public int TripCount { get; init; }
    }

    /// <summary>
    /// Count working commuters with explicit home/work building assignments.
    /// A commuter is working-age with a workplace; assigned means both buildings are active.
    /// </summary>
    public CommuterAudit AuditCommuters(WorldState state)
    {
        var hh = state.Households;
        var buildings = state.Buildings;
        int working = 0;
        int assigned = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.AgeGroup[i] != 1) continue;
            if (hh.WorkBuildingId[i] == 0) continue;

            working++;

            ushort homeId = hh.HomeBuildingId[i];
            ushort workId = hh.WorkBuildingId[i];
            if (homeId == 0) continue;
            if (homeId >= buildings.Capacity || workId >= buildings.Capacity) continue;
            if (!buildings.IsActive(homeId) || !buildings.IsActive(workId)) continue;
            if (buildings.State[homeId] != 1 || buildings.State[workId] != 1) continue;

            assigned++;
        }

        return new CommuterAudit
        {
            WorkingCommuters = working,
            AssignedCommuters = assigned,
        };
    }

    /// <summary>
    /// Aggregate O-D pairs by home/work tile for snapshot debug export.
    /// </summary>
    public CommuteOdSampleRow[] CollectCommuteOdSample(WorldState state, int limit = 16)
    {
        if (limit <= 0 || state.Households.Count == 0)
            return [];

        var counts = new Dictionary<(int hx, int hz, int wx, int wz), int>();
        var hh = state.Households;
        var buildings = state.Buildings;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.AgeGroup[i] != 1) continue;
            if (hh.WorkBuildingId[i] == 0 || hh.HomeBuildingId[i] == 0) continue;

            int homeId = hh.HomeBuildingId[i];
            int workId = hh.WorkBuildingId[i];
            if (homeId >= buildings.Capacity || workId >= buildings.Capacity) continue;
            if (!buildings.IsActive(homeId) || !buildings.IsActive(workId)) continue;
            if (buildings.State[homeId] != 1 || buildings.State[workId] != 1) continue;

            var key = (
                buildings.GridX[homeId],
                buildings.GridY[homeId],
                buildings.GridX[workId],
                buildings.GridY[workId]);

            counts.TryGetValue(key, out int existing);
            counts[key] = existing + 1;
        }

        if (counts.Count == 0)
            return [];

        var rows = new List<CommuteOdSampleRow>(counts.Count);
        foreach (var kv in counts)
        {
            rows.Add(new CommuteOdSampleRow
            {
                HomeTileX = kv.Key.hx,
                HomeTileZ = kv.Key.hz,
                WorkTileX = kv.Key.wx,
                WorkTileZ = kv.Key.wz,
                TripCount = kv.Value,
            });
        }

        rows.Sort((a, b) => b.TripCount.CompareTo(a.TripCount));
        int count = Math.Min(limit, rows.Count);
        return rows.GetRange(0, count).ToArray();
    }

    // =========================================================================
    // L2 export — top households for web CitizenPanel drill-down
    // =========================================================================

    /// <summary>Named household row for WASM populationL2 export.</summary>
    public readonly struct HouseholdSampleRow
    {
        public string Id { get; init; }
        public int TileX { get; init; }
        public int TileZ { get; init; }
        /// <summary>0–1 satisfaction.</summary>
        public float Happiness { get; init; }
        /// <summary>Commute time in game minutes.</summary>
        public float CommuteMin { get; init; }
    }

    private const float TilesToCommuteMinutes = 3f;

    /// <summary>
    /// Collect up to <paramref name="limit"/> active households sorted by happiness (desc)
    /// for the web CitizenPanel L2 list.
    /// </summary>
    public HouseholdSampleRow[] CollectHouseholdSample(WorldState state, int limit = 50)
    {
        if (limit <= 0 || state.Households.Count == 0)
            return [];

        var candidates = new List<(int idx, byte happiness)>(state.Households.Count);
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;
            candidates.Add((i, state.Households.Happiness[i]));
        }

        if (candidates.Count == 0)
            return [];

        candidates.Sort((a, b) => b.happiness.CompareTo(a.happiness));

        int count = Math.Min(limit, candidates.Count);
        var result = new HouseholdSampleRow[count];
        for (int j = 0; j < count; j++)
        {
            int idx = candidates[j].idx;
            var hh = state.Households;
            int tileX = 0;
            int tileZ = 0;
            ushort homeId = hh.HomeBuildingId[idx];
            if (homeId != 0 && homeId < state.Buildings.Capacity && state.Buildings.IsActive(homeId))
            {
                tileX = state.Buildings.GridX[homeId];
                tileZ = state.Buildings.GridY[homeId];
            }

            float commuteMin = 0f;
            if (hh.AgeGroup[idx] != 2 && hh.WorkBuildingId[idx] != 0)
            {
                float distance = CalculateBuildingDistance(state, homeId, hh.WorkBuildingId[idx]);
                commuteMin = distance * TilesToCommuteMinutes;
            }

            result[j] = new HouseholdSampleRow
            {
                Id = $"HH-{idx:D5}",
                TileX = tileX,
                TileZ = tileZ,
                Happiness = hh.Happiness[idx] / 255f,
                CommuteMin = commuteMin,
            };
        }

        return result;
    }
}
