using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Platform
{
    /// <summary>Pushes mayor stats to Steam rich presence when SDK is live.</summary>
    public sealed class SteamRichPresenceController : MonoBehaviour
    {
        CitySimBridge _sim;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            _sim = sim;

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                OnStateChanged(_sim.State);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            if (SteamRuntime.IsReady)
                SteamRuntime.Backend.ClearRichPresence();
        }

        void OnStateChanged(CitySimState state)
        {
            if (!SteamRuntime.IsReady)
                return;

            var approval = Mathf.RoundToInt(Mathf.Clamp01(state.Approval) * 100f);
            var status = $"Pop {state.Population:N0} · {approval}% approval";
            SteamRuntime.Backend.SetRichPresence("steam_display", "#status");
            SteamRuntime.Backend.SetRichPresence("status", status);
        }
    }
}
