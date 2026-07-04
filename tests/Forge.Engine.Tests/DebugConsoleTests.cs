using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class DebugConsoleTests
{
    [Fact]
    public void NewConsole_IsClosedByDefault()
    {
        var console = new DebugConsole();
        Assert.False(console.IsOpen);
    }

    [Fact]
    public void NewConsole_HasBuiltInCommands()
    {
        var console = new DebugConsole();
        Assert.True(console.CommandCount > 0);
        Assert.True(console.HasCommand("help"));
        Assert.True(console.HasCommand("clear"));
        Assert.True(console.HasCommand("quit"));
        Assert.True(console.HasCommand("exit"));
        Assert.True(console.HasCommand("fps"));
        Assert.True(console.HasCommand("speed"));
        Assert.True(console.HasCommand("pause"));
        Assert.True(console.HasCommand("money"));
        Assert.True(console.HasCommand("pop"));
        Assert.True(console.HasCommand("spawn_building"));
        Assert.True(console.HasCommand("demolish"));
        Assert.True(console.HasCommand("set_zoom"));
        Assert.True(console.HasCommand("goto"));
        Assert.True(console.HasCommand("weather"));
        Assert.True(console.HasCommand("time"));
        Assert.True(console.HasCommand("season"));
        Assert.True(console.HasCommand("era"));
        Assert.True(console.HasCommand("unlock_all_tech"));
        Assert.True(console.HasCommand("save"));
        Assert.True(console.HasCommand("load"));
        Assert.True(console.HasCommand("map_info"));
        Assert.True(console.HasCommand("sim_info"));
        Assert.True(console.HasCommand("mem_info"));
        Assert.True(console.HasCommand("reload_data"));
        Assert.True(console.HasCommand("screenshot"));
        Assert.True(console.HasCommand("debug_overlay"));
    }

    [Fact]
    public void RegisterCommand_ActionOverload_CanExecute()
    {
        var console = new DebugConsole();
        int callCount = 0;
        string[]? receivedArgs = null;

        console.RegisterCommand("test_cmd", "A test command", args =>
        {
            callCount++;
            receivedArgs = args;
        });

        Assert.True(console.HasCommand("test_cmd"));

        console.Execute("test_cmd arg1 arg2");

        Assert.Equal(1, callCount);
        Assert.NotNull(receivedArgs);
        Assert.Equal(2, receivedArgs!.Length);
        Assert.Equal("arg1", receivedArgs[0]);
        Assert.Equal("arg2", receivedArgs[1]);
    }

    [Fact]
    public void RegisterCommand_FuncOverload_OutputLogged()
    {
        var console = new DebugConsole();

        console.RegisterCommand("greet", "Greet someone", args =>
        {
            return args.Length > 0 ? $"Hello, {args[0]}!" : "Hello!";
        });

        console.Execute("greet World");

        // History should contain: the echoed command, then the output
        Assert.True(console.History.Count >= 2);
        bool found = false;
        foreach (var entry in console.History)
        {
            if (entry.Text == "Hello, World!")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected output 'Hello, World!' in history");
    }

    [Fact]
    public void Execute_EmptyString_DoesNothing()
    {
        var console = new DebugConsole();
        int historyBefore = console.History.Count;

        console.Execute("");
        console.Execute("   ");

        Assert.Equal(historyBefore, console.History.Count);
    }

    [Fact]
    public void Execute_UnknownCommand_LogsError()
    {
        var console = new DebugConsole();

        console.Execute("nonexistent_cmd");

        bool hasError = false;
        foreach (var entry in console.History)
        {
            if (entry.Text.Contains("Unknown command"))
            {
                hasError = true;
                break;
            }
        }
        Assert.True(hasError, "Expected error about unknown command in history");
    }

    [Fact]
    public void Execute_HandlerThrows_LogsError()
    {
        var console = new DebugConsole();

        console.RegisterCommand("crash", "Crash command", (Action<string[]>)(_ =>
        {
            throw new InvalidOperationException("Intentional test crash");
        }));

        console.Execute("crash");

        bool hasError = false;
        foreach (var entry in console.History)
        {
            if (entry.Text.Contains("failed") && entry.Text.Contains("Intentional test crash"))
            {
                hasError = true;
                break;
            }
        }
        Assert.True(hasError, "Expected error log for crashed command");
    }

    [Fact]
    public void CommandHistory_TracksExecutedCommands()
    {
        var console = new DebugConsole();

        console.Execute("help");
        console.Execute("fps");
        console.Execute("mem_info");

        Assert.Equal(3, console.CommandHistory.Count);
        Assert.Equal("help", console.CommandHistory[0]);
        Assert.Equal("fps", console.CommandHistory[1]);
        Assert.Equal("mem_info", console.CommandHistory[2]);
    }

    [Fact]
    public void CommandHistory_NoDuplicateConsecutive()
    {
        var console = new DebugConsole();

        console.Execute("help");
        console.Execute("help");
        console.Execute("help");

        Assert.Single(console.CommandHistory);
    }

    [Fact]
    public void Log_AddsToHistory()
    {
        var console = new DebugConsole();

        console.Log("Test message");
        console.LogWarning("Warning message");
        console.LogError("Error message");

        Assert.Equal(3, console.History.Count);
        Assert.Equal("Test message", console.History[0].Text);
        Assert.Contains("[WARN]", console.History[1].Text);
        Assert.Contains("[ERROR]", console.History[2].Text);
    }

    [Fact]
    public void ClearHistory_RemovesAllEntries()
    {
        var console = new DebugConsole();

        console.Log("Line 1");
        console.Log("Line 2");
        console.Log("Line 3");
        Assert.Equal(3, console.History.Count);

        console.ClearHistory();
        Assert.Empty(console.History);
    }

    [Fact]
    public void GetCompletions_EmptyPartial_ReturnsAllCommands()
    {
        var console = new DebugConsole();
        var completions = console.GetCompletions("");

        Assert.True(completions.Length > 0);
        Assert.Equal(console.CommandCount, completions.Length);
    }

    [Fact]
    public void GetCompletions_PartialMatch_ReturnsMatches()
    {
        var console = new DebugConsole();
        var completions = console.GetCompletions("sp");

        Assert.Contains("spawn_building", completions);
        Assert.Contains("speed", completions);
        Assert.DoesNotContain("help", completions);
    }

    [Fact]
    public void GetCompletions_ExactMatch_ReturnsSingle()
    {
        var console = new DebugConsole();
        var completions = console.GetCompletions("screenshot");

        Assert.Single(completions);
        Assert.Equal("screenshot", completions[0]);
    }

    [Fact]
    public void GetCompletions_NoMatch_ReturnsEmpty()
    {
        var console = new DebugConsole();
        var completions = console.GetCompletions("zzzzz");

        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletions_CaseInsensitive()
    {
        var console = new DebugConsole();
        var completions = console.GetCompletions("HE");

        Assert.Contains("help", completions);
    }

    [Fact]
    public void RegisterCommand_CaseInsensitive()
    {
        var console = new DebugConsole();
        bool called = false;

        console.RegisterCommand("MyCommand", "Test", _ => called = true);

        Assert.True(console.HasCommand("mycommand"));
        Assert.True(console.HasCommand("MYCOMMAND"));

        console.Execute("MYCOMMAND");
        Assert.True(called);
    }

    [Fact]
    public void Execute_NoArgs_PassesEmptyArray()
    {
        var console = new DebugConsole();
        string[]? receivedArgs = null;

        console.RegisterCommand("noargs", "No args test", args => receivedArgs = args);

        console.Execute("noargs");

        Assert.NotNull(receivedArgs);
        Assert.Empty(receivedArgs!);
    }

    [Fact]
    public void Speed_Command_SetsLevel()
    {
        var console = new DebugConsole();
        int receivedLevel = -1;
        console.GameSpeedChanged += level => receivedLevel = level;

        console.Execute("speed 3");

        Assert.Equal(3, receivedLevel);
        Assert.Equal(3, console.GameSpeedLevel);
    }

    [Fact]
    public void Pause_Command_TogglesSpeed()
    {
        var console = new DebugConsole();
        int lastLevel = -1;
        console.GameSpeedChanged += level => lastLevel = level;

        console.Execute("pause");
        Assert.Equal(0, console.GameSpeedLevel);

        console.Execute("pause");
        Assert.Equal(1, console.GameSpeedLevel);
    }

    [Fact]
    public void Money_Command_FiresEvent()
    {
        var console = new DebugConsole();
        long receivedAmount = 0;
        console.MoneyChanged += amount => receivedAmount = amount;

        console.Execute("money 50000");

        Assert.Equal(50000, receivedAmount);
    }

    [Fact]
    public void Weather_Command_InvalidInput()
    {
        var console = new DebugConsole();
        string? received = null;
        console.WeatherChanged += w => received = w;

        console.Execute("weather tornado");

        Assert.Null(received); // Invalid weather type should not fire event
    }

    [Fact]
    public void Weather_Command_ValidInput()
    {
        var console = new DebugConsole();
        string? received = null;
        console.WeatherChanged += w => received = w;

        console.Execute("weather rain");

        Assert.Equal("rain", received);
    }

    [Fact]
    public void Help_Command_ListsCommands()
    {
        var console = new DebugConsole();

        console.Execute("help");

        bool hasHelp = false;
        foreach (var entry in console.History)
        {
            if (entry.Text.Contains("Available commands"))
            {
                hasHelp = true;
                break;
            }
        }
        Assert.True(hasHelp);
    }

    [Fact]
    public void Help_SpecificCommand_ShowsDescription()
    {
        var console = new DebugConsole();

        console.Execute("help speed");

        bool found = false;
        foreach (var entry in console.History)
        {
            if (entry.Text.Contains("speed") && entry.Text.Contains("game speed"))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected description of speed command in output");
    }

    [Fact]
    public void MemInfo_Command_ReturnsInfo()
    {
        var console = new DebugConsole();

        console.Execute("mem_info");

        bool found = false;
        foreach (var entry in console.History)
        {
            if (entry.Text.Contains("Memory Info"))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected memory info in output");
    }

    [Fact]
    public void Clear_Command_ClearsHistory()
    {
        var console = new DebugConsole();

        console.Log("Line 1");
        console.Log("Line 2");
        Assert.True(console.History.Count >= 2);

        console.Execute("clear");

        Assert.Empty(console.History);
    }

    [Fact]
    public void FpsToggle_Command()
    {
        var console = new DebugConsole();
        Assert.False(console.FpsOverlayVisible);

        console.Execute("fps");
        Assert.True(console.FpsOverlayVisible);

        console.Execute("fps");
        Assert.False(console.FpsOverlayVisible);
    }

    [Fact]
    public void SpawnBuilding_Command_FiresEvent()
    {
        var console = new DebugConsole();
        ushort rTypeId = 0;
        int rX = 0, rY = 0;
        console.BuildingSpawned += (typeId, x, y) => { rTypeId = typeId; rX = x; rY = y; };

        console.Execute("spawn_building 42 10 20");

        Assert.Equal(42, rTypeId);
        Assert.Equal(10, rX);
        Assert.Equal(20, rY);
    }

    [Fact]
    public void Era_Command_InvalidRange()
    {
        var console = new DebugConsole();
        int? received = null;
        console.EraChanged += e => received = e;

        console.Execute("era 0");
        Assert.Null(received);

        console.Execute("era 6");
        Assert.Null(received);
    }

    [Fact]
    public void Season_Command_ValidValues()
    {
        var console = new DebugConsole();
        var received = new List<string>();
        console.SeasonChanged += s => received.Add(s);

        console.Execute("season spring");
        console.Execute("season summer");
        console.Execute("season autumn");
        console.Execute("season winter");

        Assert.Equal(4, received.Count);
        Assert.Equal("spring", received[0]);
        Assert.Equal("summer", received[1]);
        Assert.Equal("autumn", received[2]);
        Assert.Equal("winter", received[3]);
    }

    [Fact]
    public void RegisterCommand_NullName_Throws()
    {
        var console = new DebugConsole();
        Assert.Throws<ArgumentNullException>(() =>
            console.RegisterCommand(null!, "desc", (Action<string[]>)(_ => { })));
    }

    [Fact]
    public void RegisterCommand_NullHandler_Throws()
    {
        var console = new DebugConsole();
        Assert.Throws<ArgumentNullException>(() =>
            console.RegisterCommand("cmd", "desc", (Action<string[]>)null!));
    }
}
