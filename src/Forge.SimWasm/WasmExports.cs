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
        _host.Init(worldSize > 0 ? worldSize : 64);
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
    public static string GetStatus()
    {
        if (_host is null)
            return JsonSerializer.Serialize(new { initialized = false });

        return JsonSerializer.Serialize(new
        {
            initialized = _host.IsInitialized,
            tickCount = _host.TickCount,
            systems = new[]
            {
                "EconomySystem",
                "PopulationSystem",
                "TrafficSystem",
                "ServiceSystem",
                "ZoneGrowthSystem",
                "BudgetSystem",
                "PoliticsSystem",
                "CulturalDNASystem",
            },
            stubbed = new[]
            {
                "EventSystem (no events.json in WASM bundle)",
                "ResearchSystem (no tech_tree.json in WASM bundle)",
                "TradeSystem (not wired in spike)",
                "ProductionChain (not wired in spike)",
            },
        });
    }
}
