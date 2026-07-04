using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class WorldStateTests
{
    private readonly WorldState _state = new(16);

    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        Assert.Equal(1, _state.Day);
        Assert.Equal(1, _state.Month);
        Assert.Equal(2024, _state.Year);
        Assert.Equal(0, _state.TickCount);
        Assert.Equal(50_000, _state.CityFunds);
        Assert.Equal(0.5f, _state.Happiness);
        Assert.Equal(0, _state.Population);
        Assert.Equal(4, _state.Era); // Modern era
        Assert.Equal("2024-01-01", _state.DateString);
        Assert.Equal(-1, _state.CurrentResearchId);
    }

    [Fact]
    public void AdvanceDay_IncrementsDay()
    {
        bool newMonth = _state.AdvanceDay();

        Assert.Equal(2, _state.Day);
        Assert.Equal(1, _state.Month);
        Assert.Equal(2024, _state.Year);
        Assert.False(newMonth);
        Assert.Equal("2024-01-02", _state.DateString);
    }

    [Fact]
    public void AdvanceDay_RollsToNewMonth()
    {
        _state.Day = 30;

        bool newMonth = _state.AdvanceDay();

        Assert.True(newMonth);
        Assert.Equal(1, _state.Day);
        Assert.Equal(2, _state.Month);
    }

    [Fact]
    public void AdvanceDay_RollsToNewYear()
    {
        _state.Day = 30;
        _state.Month = 12;

        bool newMonth = _state.AdvanceDay();

        Assert.True(newMonth);
        Assert.Equal(1, _state.Day);
        Assert.Equal(1, _state.Month);
        Assert.Equal(2025, _state.Year);
    }

    [Fact]
    public void AdvanceDay_FullYearCycle()
    {
        int startYear = _state.Year;
        for (int i = 0; i < 360; i++) // 12 months * 30 days
        {
            _state.AdvanceDay();
        }

        Assert.Equal(startYear + 1, _state.Year);
        Assert.Equal(1, _state.Month);
        Assert.Equal(1, _state.Day);
    }

    [Fact]
    public void IsTechUnlocked_ReturnsFalseForNewState()
    {
        Assert.False(_state.IsTechUnlocked(0));
        Assert.False(_state.IsTechUnlocked(63));
        Assert.False(_state.IsTechUnlocked(127));
        Assert.False(_state.IsTechUnlocked(255));
    }

    [Fact]
    public void UnlockTech_SetsCorrectBit()
    {
        _state.UnlockTech(0);
        Assert.True(_state.IsTechUnlocked(0));
        Assert.False(_state.IsTechUnlocked(1));

        _state.UnlockTech(63);
        Assert.True(_state.IsTechUnlocked(63));

        _state.UnlockTech(64);
        Assert.True(_state.IsTechUnlocked(64));
        Assert.False(_state.IsTechUnlocked(65));
    }

    [Fact]
    public void UnlockTech_MultipleInSameWord()
    {
        _state.UnlockTech(0);
        _state.UnlockTech(5);
        _state.UnlockTech(63);

        Assert.True(_state.IsTechUnlocked(0));
        Assert.True(_state.IsTechUnlocked(5));
        Assert.True(_state.IsTechUnlocked(63));
        Assert.False(_state.IsTechUnlocked(1));
    }

    [Fact]
    public void UnlockTech_AcrossAllFourWords()
    {
        _state.UnlockTech(0);    // word 0
        _state.UnlockTech(64);   // word 1
        _state.UnlockTech(128);  // word 2
        _state.UnlockTech(192);  // word 3

        Assert.True(_state.IsTechUnlocked(0));
        Assert.True(_state.IsTechUnlocked(64));
        Assert.True(_state.IsTechUnlocked(128));
        Assert.True(_state.IsTechUnlocked(192));
    }

    [Fact]
    public void IsOrdinanceActive_InitiallyAllInactive()
    {
        for (int i = 0; i < 64; i++)
        {
            Assert.False(_state.IsOrdinanceActive(i));
        }
    }

    [Fact]
    public void ToggleOrdinance_ActivatesAndDeactivates()
    {
        _state.ToggleOrdinance(5);
        Assert.True(_state.IsOrdinanceActive(5));
        Assert.False(_state.IsOrdinanceActive(4));

        _state.ToggleOrdinance(5);
        Assert.False(_state.IsOrdinanceActive(5));
    }

    [Fact]
    public void ToggleOrdinance_MultipleOrdinances()
    {
        _state.ToggleOrdinance(0);
        _state.ToggleOrdinance(10);
        _state.ToggleOrdinance(63);

        Assert.True(_state.IsOrdinanceActive(0));
        Assert.True(_state.IsOrdinanceActive(10));
        Assert.True(_state.IsOrdinanceActive(63));
        Assert.False(_state.IsOrdinanceActive(1));
    }

    [Fact]
    public void BudgetLedger_Reset_ClearsAll()
    {
        _state.Income.ResidentialTax = 1000;
        _state.Income.CommercialTax = 500;
        _state.Income.Miscellaneous = 200;

        _state.Income.Reset();

        Assert.Equal(0, _state.Income.ResidentialTax);
        Assert.Equal(0, _state.Income.CommercialTax);
        Assert.Equal(0, _state.Income.Miscellaneous);
        Assert.Equal(0, _state.Income.Total);
    }

    [Fact]
    public void BudgetLedger_Total_SumsAllEntries()
    {
        _state.Income.ResidentialTax = 100;
        _state.Income.CommercialTax = 200;
        _state.Income.IndustrialTax = 300;

        Assert.Equal(600, _state.Income.Total);
    }

    [Fact]
    public void AddEvent_ReturnsTrue_WhenSpace()
    {
        bool result = _state.AddEvent(1, 5.0f, 0.8f);

        Assert.True(result);
        Assert.Equal(1, _state.ActiveEventCount);
        Assert.Equal(1, _state.Events[0].EventId);
        Assert.Equal(5.0f, _state.Events[0].RemainingDays);
        Assert.Equal(0.8f, _state.Events[0].Severity);
    }

    [Fact]
    public void AddEvent_ReturnsFalse_WhenFull()
    {
        for (int i = 0; i < 16; i++)
        {
            Assert.True(_state.AddEvent(i, 10f, 0.5f));
        }

        Assert.False(_state.AddEvent(99, 1f, 1f));
        Assert.Equal(16, _state.ActiveEventCount);
    }

    [Fact]
    public void RemoveEvent_SwapRemoves()
    {
        _state.AddEvent(1, 10f, 0.5f);
        _state.AddEvent(2, 10f, 0.5f);
        _state.AddEvent(3, 10f, 0.5f);

        _state.RemoveEvent(0); // Remove first; last (3) should take its place

        Assert.Equal(2, _state.ActiveEventCount);
        Assert.Equal(3, _state.Events[0].EventId);
        Assert.Equal(2, _state.Events[1].EventId);
    }

    [Fact]
    public void RemoveEvent_OutOfRange_DoesNothing()
    {
        _state.AddEvent(1, 10f, 0.5f);

        _state.RemoveEvent(-1);
        _state.RemoveEvent(5);

        Assert.Equal(1, _state.ActiveEventCount);
    }

    [Fact]
    public void TickEvents_DecrementsAndRemovesExpired()
    {
        _state.AddEvent(1, 2f, 0.5f);   // Will survive 1 tick
        _state.AddEvent(2, 1f, 0.5f);   // Will expire after 1 tick
        _state.AddEvent(3, 0.5f, 0.5f); // Will expire after 1 tick

        _state.TickEvents();

        Assert.Equal(1, _state.ActiveEventCount);
        Assert.Equal(1, _state.Events[0].EventId);
        Assert.Equal(1f, _state.Events[0].RemainingDays);
    }

    [Fact]
    public void MemoryReport_ReturnsNonEmptyString()
    {
        string report = _state.MemoryReport();

        Assert.False(string.IsNullOrEmpty(report));
        Assert.Contains("WorldState memory", report);
        Assert.Contains("MB", report);
    }

    [Fact]
    public void Season_DerivedFromMonth()
    {
        _state.Month = 3; Assert.Equal(0, _state.Season); // Spring
        _state.Month = 6; Assert.Equal(1, _state.Season); // Summer
        _state.Month = 9; Assert.Equal(2, _state.Season); // Autumn
        _state.Month = 12; Assert.Equal(3, _state.Season); // Winter
        _state.Month = 1; Assert.Equal(3, _state.Season);  // Winter
    }

    [Fact]
    public void DateString_UpdatesOnAdvance()
    {
        _state.AdvanceDay();
        Assert.Equal("2024-01-02", _state.DateString);

        _state.Day = 30;
        _state.AdvanceDay(); // rolls to Feb
        Assert.Equal("2024-02-01", _state.DateString);
    }
}
