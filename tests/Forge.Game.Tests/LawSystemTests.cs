using Forge.Game.Simulation;
using Xunit;

namespace Forge.Game.Tests;

public sealed class LawSystemTests
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
            "effects": { "accident_rate": -0.3 },
            "faction_reactions": { "business": -0.1 },
            "cost_monthly": 500,
            "compliance_base": 0.7,
            "tech_prerequisite": null,
            "description": "Speed limits"
          },
          {
            "id": "recycling_mandate",
            "name": "Recycling Mandate",
            "category": "environment",
            "era_min": "modern",
            "parameters": [
              { "name": "participation_pct", "min": 0, "max": 100, "default": 60, "step": 5 }
            ],
            "effects": { "waste_reduction": 0.2 },
            "faction_reactions": { "workers": 0.05 },
            "cost_monthly": 1200,
            "compliance_base": 0.8,
            "tech_prerequisite": null,
            "description": "Mandatory recycling"
          }
        ]
        """;

    [Fact]
    public void LoadFromJson_ParsesDefinitions()
    {
        var laws = new LawSystem();
        laws.LoadFromJson(SampleLawsJson);

        Assert.Equal(2, laws.DefinitionCount);
        Assert.Equal(0, laws.ActiveLawCount);
        Assert.Equal("Speed Limits", laws.Definitions[0].Name);
        Assert.Equal(50f, laws.GetParameterValue(0, 0));
    }

    [Fact]
    public void SetActive_TracksActiveLawCount()
    {
        var laws = new LawSystem();
        laws.LoadFromJson(SampleLawsJson);

        Assert.True(laws.SetActive("speed_limit", true));
        Assert.Equal(1, laws.ActiveLawCount);
        Assert.True(laws.IsActive(0));

        Assert.True(laws.SetActive("recycling_mandate", true));
        Assert.Equal(2, laws.ActiveLawCount);

        Assert.True(laws.SetActive(0, false));
        Assert.Equal(1, laws.ActiveLawCount);
        Assert.False(laws.IsActive(0));
    }

    [Fact]
    public void SetParameterValue_ClampsToMinMax()
    {
        var laws = new LawSystem();
        laws.LoadFromJson(SampleLawsJson);

        Assert.True(laws.SetParameterValue(0, 0, 999f));
        Assert.Equal(120f, laws.GetParameterValue(0, 0));

        Assert.True(laws.SetParameterValue(0, 0, 5f));
        Assert.Equal(20f, laws.GetParameterValue(0, 0));
    }
}
