using System.Text.Json;
using System.Runtime.InteropServices.JavaScript;
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
    public static bool EnqueueResearch(int techId)
    {
        return _host?.EnqueueResearch(techId) ?? false;
    }

    [JSExport]
    public static string GetStatus()
    {
        return _host?.GetStatusJson()
            ?? JsonSerializer.Serialize(new WasmStatusDto(), JsonContext.Default.WasmStatusDto);
    }

    [JSExport]
    public static bool LoadSnapshot(string snapshotJson)
    {
        return _host?.LoadSnapshotFromJson(snapshotJson) ?? false;
    }
}
