using CityMajor.Sim;
using UnityEngine;

namespace CityMajor.Audio
{
    /// <summary>Ambient bed + traffic hum scaled by rush multiplier (v1.5 audio scaffold).</summary>
    public sealed class AmbientAudioController : MonoBehaviour
    {
        [SerializeField] float baseVolume = 0.12f;
        [SerializeField] float rushVolumeBoost = 0.18f;

        CitySimBridge _sim;
        AudioSource _cityBed;
        AudioSource _trafficHum;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureSources();
            _sim.OnStateChanged += OnState;
            OnState(_sim.State);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;
        }

        void EnsureSources()
        {
            if (_cityBed == null)
            {
                _cityBed = gameObject.AddComponent<AudioSource>();
                _cityBed.loop = true;
                _cityBed.playOnAwake = false;
                _cityBed.spatialBlend = 0f;
                _cityBed.volume = 0f;
            }

            if (_trafficHum == null)
            {
                var go = new GameObject("TrafficHum");
                go.transform.SetParent(transform, false);
                _trafficHum = go.AddComponent<AudioSource>();
                _trafficHum.loop = true;
                _trafficHum.playOnAwake = false;
                _trafficHum.spatialBlend = 0f;
                _trafficHum.volume = 0f;
            }
        }

        void OnState(CitySimState state)
        {
            var rush = Mathf.Clamp(state.RushMultiplier, 0.5f, 3f);
            if (_cityBed != null)
                _cityBed.volume = baseVolume;

            if (_trafficHum != null)
                _trafficHum.volume = baseVolume + rushVolumeBoost * (rush - 1f);

            // Clips assigned in editor later — scaffold only sets volumes.
        }
    }
}
