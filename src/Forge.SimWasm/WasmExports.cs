using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace Forge.SimWasm;

public static partial class Program
{
    private static WasmSimHost? _host;

    [JSExport]
    public static void Init(int worldSize)
    {
        _host = new WasmSimHost();
        _host.Init(worldSize > 0 ? worldSize : WasmConfig.DefaultWorldSize);
    }

    [JSExport]
    public static double Tick(double dtSeconds)
    {
        if (_host is null) return 0;
        _host.Tick(dtSeconds > 0 ? dtSeconds : 0.1);
        return _host.TickCount;
    }

    [JSExport]
    public static string GetRenderSnapshot()
    {
        return _host?.GetRenderSnapshotJson() ?? "{}";
    }

    [JSExport]
    public static void PaintZone(int x, int y, int zoneType)
    {
        _host?.PaintZone(x, y, (byte)zoneType);
    }

    [JSExport]
    public static void Bulldoze(int x, int y)
    {
        _host?.Bulldoze(x, y);
    }

    [JSExport]
    public static void PlaceRoad(int x, int y)
    {
        _host?.PlaceRoad(x, y);
    }

    [JSExport]
    public static string GetStatus()
    {
        if (_host is null)
            return JsonSerializer.Serialize(new { initialized = false });

        return JsonSerializer.Serialize(new
        {
            initialized = _host.IsInitialized,
            tick = _host.TickCount,
            tickCount = _host.TickCount,
            population = _host.Population,
            cityFunds = _host.CityFunds,
            era = _host.Era,
            eraName = WasmEraDeriver.EraName(_host.Era),
            researchPoints = _host.ResearchPoints,
            researchRate = _host.ResearchRate,
            tickIntervals = new
            {
                gameDaySeconds = WasmConfig.GameDayInterval,
                trafficStubSeconds = WasmConfig.TrafficStubInterval,
            },
            systems = new[]
            {
                "EconomySystem",
                "PopulationSystem",
                "ServiceSystem",
                "ZoneGrowthSystem",
                "BudgetSystem",
                "PoliticsSystem",
                "CulturalDNASystem",
            },
            stubbed = new[]
            {
                "TrafficSystem (WasmTrafficStub — no BPR assignment in browser)",
                "EventSystem (no events.json in WASM bundle)",
                "ResearchSystem (no tech_tree.json in WASM bundle)",
                "TradeSystem (not wired in spike)",
                "ProductionChain (not wired in spike)",
            },
        });
    }
}
