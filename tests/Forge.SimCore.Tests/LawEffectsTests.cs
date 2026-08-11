using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Cathedral P6.1 — law toggles apply budget / traffic / spawn multipliers.
/// </summary>
public sealed class LawEffectsTests
{
    private const string SampleLawsJson = """
        [
          {
            "id": "speed_limit",
            "name": "Speed Limits",
            "category": "traffic",
            "era_min": "industrial",
            "parameters": [
              { "name": "max_speed_kmh", "min": 20, "max": 120, "default": 50, "step": 10 }
            ],
            "effects": {
              "accident_rate": -0.3,
              "road_capacity": -0.1
            },
            "faction_reactions": { "business": -0.1 },
            "cost_monthly": 500,
            "compliance_base": 0.7,
            "tech_prerequisite": null,
            "description": "Speed limits"
          },
          {
            "id": "property_tax_rate",
            "name": "Property Tax",
            "category": "tax",
            "era_min": "frontier",
            "parameters": [
              { "name": "rate_percent", "min": 0, "max": 5, "default": 2, "step": 0.5 }
            ],
            "effects": {
              "tax_revenue": 0.2
            },
            "faction_reactions": { "property_owners": -0.3 },
            "cost_monthly": 0,
            "compliance_base": 0.9,
            "tech_prerequisite": null,
            "description": "Property tax ordinance"
          },
          {
            "id": "height_limit",
            "name": "Height Limit",
            "category": "zoning",
            "era_min": "frontier",
            "parameters": [],
            "effects": {
              "housing_density": -0.2,
              "construction_cost": -0.05,
              "housing_supply": -0.15
            },
            "faction_reactions": {},
            "cost_monthly": 0,
            "compliance_base": 0.8,
            "tech_prerequisite": null,
            "description": "Caps building height"
          },
          {
            "id": "emission_limits",
            "name": "Emission Limits",
            "category": "environment",
            "era_min": "industrial",
            "parameters": [],
            "effects": {
              "industrial_output": -0.15,
              "industrial_operating_cost": 0.1
            },
            "faction_reactions": {},
            "cost_monthly": 0,
            "compliance_base": 0.7,
            "tech_prerequisite": null,
            "description": "Industrial emission caps"
          },
          {
            "id": "parking_regulations",
            "name": "Parking Regulations",
            "category": "traffic",
            "era_min": "industrial",
            "parameters": [],
            "effects": {
              "commercial_accessibility": -0.1
            },
            "faction_reactions": {},
            "cost_monthly": 0,
            "compliance_base": 0.75,
            "tech_prerequisite": null,
            "description": "Restricts curb parking"
          },
          {
            "id": "congestion_tax",
            "name": "Congestion Tax",
            "category": "traffic",
            "era_min": "industrial",
            "parameters": [],
            "effects": {
              "traffic_congestion": 0.2
            },
            "faction_reactions": {},
            "cost_monthly": 0,
            "compliance_base": 0.8,
            "tech_prerequisite": null,
            "description": "Raises effective congestion"
          }
        ]
        """;

    [Fact]
    public void GetAggregateEffect_SumsActiveLawEffects()
    {
        var laws = new LawSystem();
        laws.LoadFromJson(SampleLawsJson);

        Assert.Equal(0f, laws.GetAggregateEffect("accident_rate"));
        Assert.Equal(0f, laws.GetAggregateEffect("missing_key"));

        Assert.True(laws.SetActive("speed_limit", true));
        Assert.Equal(-0.3f, laws.GetAggregateEffect("accident_rate"), 3);
        Assert.Equal(-0.1f, laws.GetAggregateEffect(LawEffectKeys.RoadCapacity), 3);
    }

    [Fact]
    public void SetLawActive_RecomputesTrafficCapacityMultiplier()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.Laws.LoadFromJson(SampleLawsJson);

        Assert.Equal(1f, host.State.LawTrafficCapacityMult);

        Assert.True(host.SetLawActive("speed_limit", true));
        Assert.Equal(0.9f, host.State.LawTrafficCapacityMult, 3);
        Assert.Equal(1, host.State.ActiveLawCount);

        var snap = host.GetSnapshot();
        Assert.Equal(1, snap.ActiveLawCount);
        Assert.Equal(0.9f, snap.LawTrafficCapacityMult, 3);
    }

    [Fact]
    public void SetLawActive_TrafficCongestion_ReducesCapacityMultiplier()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        host.Laws.LoadFromJson(SampleLawsJson);

        Assert.True(host.SetLawActive("congestion_tax", true));
        Assert.Equal(0.8f, host.State.LawTrafficCapacityMult, 3);
        Assert.Equal(0.8f, host.GetSnapshot().LawTrafficCapacityMult, 3);
    }

    [Fact]
    public void MonthlyBudget_AppliesLawRevenueAndOperatingCosts()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        host.Laws.LoadFromJson(SampleLawsJson);

        long fundsBefore = host.State.CityFunds;
        Assert.True(host.SetLawActive("speed_limit", true));

        for (int i = 0; i < 40; i++)
            host.Tick(1.0);

        Assert.True(host.State.CityFunds < fundsBefore,
            "active speed_limit should deduct monthly operating cost from treasury");
    }

    [Fact]
    public void SetLawActive_RecomputesZoneSpawnMultipliers()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        host.Laws.LoadFromJson(SampleLawsJson);

        Assert.Equal(1f, host.State.LawSpawnDemandMult);
        Assert.Equal(1f, host.State.LawResidentialSpawnMult);
        Assert.Equal(1f, host.State.LawIndustrialSpawnMult);
        Assert.Equal(1f, host.State.LawCommercialSpawnMult);

        Assert.True(host.SetLawActive("height_limit", true));
        // construction_cost = -0.05 → demand = 1 - (-0.05)*0.5 = 1.025
        Assert.Equal(1.025f, host.State.LawSpawnDemandMult, 3);
        // residential = 1 + (-0.15) + (-0.2)*0.5 - (-0.05)*0.3 = 0.765
        Assert.Equal(0.765f, host.State.LawResidentialSpawnMult, 3);

        Assert.True(host.SetLawActive("emission_limits", true));
        // industrial = 1 + (-0.15) - 0.1*0.3 - (-0.05)*0.3 = 0.835
        Assert.Equal(0.835f, host.State.LawIndustrialSpawnMult, 3);

        Assert.True(host.SetLawActive("parking_regulations", true));
        // commercial = 1 + (-0.1) + 0 - 0 - (-0.05)*0.2 = 0.91
        Assert.Equal(0.91f, host.State.LawCommercialSpawnMult, 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.LawSpawnDemandMult, snap.LawSpawnDemandMult, 3);
        Assert.Equal(host.State.LawResidentialSpawnMult, snap.LawResidentialSpawnMult, 3);
        Assert.Equal(host.State.LawIndustrialSpawnMult, snap.LawIndustrialSpawnMult, 3);
        Assert.Equal(host.State.LawCommercialSpawnMult, snap.LawCommercialSpawnMult, 3);
    }

    [Fact]
    public void GetZoneLawSpawnMult_CompoundsBaselineAndZoneMult()
    {
        var state = new WorldState(16)
        {
            LawSpawnDemandMult = 0.9f,
            LawResidentialSpawnMult = 0.8f,
            LawIndustrialSpawnMult = 1.2f,
            LawCommercialSpawnMult = 0.7f,
        };

        // ZoneGrowthSystem: 1=res low, 3=commercial, 4=industrial, 0=none
        Assert.Equal(0.72f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 1), 3);
        Assert.Equal(1.08f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 4), 3);
        Assert.Equal(0.63f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 3), 3);
        Assert.Equal(0.9f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 0), 3);
    }

    [Fact]
    public void RestrictiveHousingLaws_ReduceResidentialSpawnsVsBaseline()
    {
        int withLaws = CountResidentialSpawns(lawResidentialMult: 0.5f, lawDemandMult: 0.8f, seed: 42);
        int withoutLaws = CountResidentialSpawns(lawResidentialMult: 1f, lawDemandMult: 1f, seed: 42);

        Assert.True(withLaws < withoutLaws,
            $"lower law spawn mult should suppress residential spawns (with={withLaws}, without={withoutLaws})");
    }

    private static int CountResidentialSpawns(float lawResidentialMult, float lawDemandMult, int seed)
    {
        const int size = 32;
        var growth = new ZoneGrowthSystem(size, seed: seed);
        var state = new WorldState(size, maxBuildings: 512)
        {
            Population = 5_000,
            CityFunds = 200_000,
            LawSpawnDemandMult = lawDemandMult,
            LawResidentialSpawnMult = lawResidentialMult,
        };
        var economy = new EconomySystem();

        // Vertical road corridor + residential zones with high land value for desirability.
        int center = size / 2;
        for (int y = center - 6; y <= center + 6; y++)
        {
            state.Tiles.RoadFlags[state.Tiles.Index(center, y)] = 0x0F;
            for (int x = center - 6; x <= center + 6; x++)
            {
                if (x == center) continue; // keep the road tile itself unzoned
                int idx = state.Tiles.Index(x, y);
                state.Tiles.ZoneType[idx] = 1; // residential low
                state.Tiles.LandValue[idx] = 0.9f;
                state.Tiles.PowerGrid[idx] = 1;
                state.Tiles.WaterGrid[idx] = 1;
            }
        }

        int before = state.Buildings.Count;
        for (int day = 0; day < 25; day++)
            growth.Tick(state, economy);

        return state.Buildings.Count - before;
    }
}
