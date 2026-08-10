using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

[Collection("SimHost")]
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

        var laws = new LawSystem();
        laws.LoadFromJson(SampleLawsJson);
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
}
