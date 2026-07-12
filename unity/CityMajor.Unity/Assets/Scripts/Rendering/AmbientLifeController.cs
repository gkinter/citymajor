using CityMajor.Sim;
using Forge.SimWasm;
using UnityEngine;

namespace CityMajor.Rendering
{
    /// <summary>Time-of-day ambient tint + optional night dimming (v1.5 scaffold).</summary>
    public sealed class AmbientLifeController : MonoBehaviour
    {
        [SerializeField] Light sunLight;
        [SerializeField] Color dayAmbient = new(0.55f, 0.62f, 0.72f);
        [SerializeField] Color nightAmbient = new(0.08f, 0.1f, 0.18f);
        [SerializeField] Color duskSun = new(1f, 0.72f, 0.45f);

        CitySimBridge _sim;
        Color _baseSunColor = Color.white;
        float _baseSunIntensity = 1f;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            if (sunLight == null)
                sunLight = FindSun();

            if (sunLight != null)
            {
                _baseSunColor = sunLight.color;
                _baseSunIntensity = sunLight.intensity;
            }

            _sim.OnStateChanged += OnState;
            OnState(_sim.State);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;
        }

        void OnState(CitySimState state)
        {
            var hour = state.TimeOfDay % 24f;
            var night = hour < 6f || hour >= 20f;
            var dusk = (hour >= 6f && hour < 8f) || (hour >= 18f && hour < 20f);

            RenderSettings.ambientLight = night ? nightAmbient : dayAmbient;

            if (sunLight == null)
                return;

            if (night)
            {
                sunLight.intensity = _baseSunIntensity * 0.15f;
                sunLight.color = nightAmbient;
            }
            else if (dusk)
            {
                sunLight.intensity = _baseSunIntensity * 0.65f;
                sunLight.color = Color.Lerp(duskSun, _baseSunColor, 0.5f);
            }
            else
            {
                sunLight.intensity = _baseSunIntensity;
                sunLight.color = _baseSunColor;
            }
        }

        static Light FindSun()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                    return l;
            }

            return null;
        }
    }
}
