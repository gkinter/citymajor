using System.Text.Json;
using System.Runtime.InteropServices.JavaScript;
namespace Forge.SimWasm;

public static partial class Program
{
    private static WasmSimHost? _host;

    [JSExport]
    public static void Init(int worldSize, int skipStarterCity = 0)
    {
        _host = new WasmSimHost();
        _host.Init(
            worldSize > 0 ? worldSize : WasmConfig.DefaultWorldSize,
            skipStarterCity != 0);
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
    public static bool PlaceBuilding(int tileX, int tileY, int typeId)
    {
        return _host?.PlaceBuilding(tileX, tileY, typeId) ?? false;
    }

    [JSExport]
    public static bool EnqueueResearch(int techId)
    {
        return _host?.EnqueueResearch(techId) ?? false;
    }

    [JSExport]
    public static bool SetLawActive(string lawId, bool active)
    {
        return _host?.SetLawActive(lawId, active) ?? false;
    }

    [JSExport]
    public static void AdjustBudget(double deltaFunds)
    {
        _host?.AdjustBudget((long)deltaFunds);
    }

    [JSExport]
    public static void ApplyApprovalDelta(float deltaPercent)
    {
        _host?.ApplyApprovalDelta(deltaPercent);
    }

    [JSExport]
    public static void BoostResearch(float points)
    {
        _host?.BoostResearch(points);
    }

    [JSExport]
    public static bool ResolveHeraldEvent(int eventId)
    {
        return _host?.ResolveHeraldEvent(eventId) ?? false;
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

    [JSExport]
    public static string ExportCmjr()
    {
        if (_host is null) return "";
        return Convert.ToBase64String(_host.ExportCmjrBytes());
    }

    [JSExport]
    public static bool LoadFromCmjr(string base64Cmjr)
    {
        return _host?.LoadFromCmjrBase64(base64Cmjr) ?? false;
    }
}
