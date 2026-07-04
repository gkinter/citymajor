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

    /// <summary>
    /// Browser-delivered delta may spike (tab throttling, GC pause). Clamp to a
    /// sane window so a single frame can't advance the sim past
    /// <see cref="MaxTickDtSeconds"/>; the caller catches up via the worker
    /// accumulator loop instead of a runaway multi-second step here.
    /// </summary>
    private const double MinTickDtSeconds = 0.001;
    private const double MaxTickDtSeconds = 0.25;

    [JSExport]
    public static double Tick(double dtSeconds)
    {
        if (_host is null) return 0;
        double dt = dtSeconds > 0 ? dtSeconds : 0.1;
        if (dt < MinTickDtSeconds) dt = MinTickDtSeconds;
        else if (dt > MaxTickDtSeconds) dt = MaxTickDtSeconds;
        _host.Tick(dt);
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
