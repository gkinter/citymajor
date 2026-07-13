using UnityEngine;

namespace CityMajor.Platform
{
    /// <summary>Steam UGC workshop scaffold — log-only until v2.5 Steamworks wiring (SB-4185).</summary>
    public static class SteamWorkshopStub
    {
        public static void PublishBlueprint(byte[] header, string title)
        {
            var len = header?.Length ?? 0;
            Debug.Log($"[CityMajor] SteamWorkshopStub.PublishBlueprint title=\"{title}\" headerBytes={len} (not uploaded — v2.5)");
        }

        public static void SubscribeToItem(ulong fileId)
        {
            Debug.Log($"[CityMajor] SteamWorkshopStub.SubscribeToItem fileId={fileId} (not subscribed — v2.5)");
        }
    }
}
