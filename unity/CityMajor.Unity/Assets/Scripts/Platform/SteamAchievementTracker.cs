using System;
using System.Collections.Generic;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using UnityEngine;

namespace CityMajor.Platform
{
    /// <summary>Evaluates v1 achievements and unlocks via Steam when conditions are met.</summary>
    public sealed class SteamAchievementTracker : MonoBehaviour
    {
        public event Action<AchievementEntry> OnAchievementUnlocked;

        CitySimBridge _sim;
        readonly HashSet<string> _unlockedLocal = new();

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
            {
                _sim.OnStateChanged -= OnStateChanged;
                _sim.OnSnapshotChanged -= OnSnapshot;
            }

            _sim = sim;

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                _sim.OnSnapshotChanged += OnSnapshot;
                Evaluate(_sim.State, _sim.LatestSnapshot);
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnStateChanged -= OnStateChanged;
                _sim.OnSnapshotChanged -= OnSnapshot;
            }
        }

        void OnStateChanged(CitySimState state) =>
            Evaluate(state, _sim?.LatestSnapshot);

        void OnSnapshot(SimSnapshot snapshot) =>
            Evaluate(_sim?.State ?? default, snapshot);

        void Evaluate(CitySimState state, SimSnapshot snapshot)
        {
            var techs = _sim != null ? _sim.CountUnlockedTechs() : 0;
            var roads = AchievementEvaluator.CountRoadTiles(snapshot);

            foreach (var entry in AchievementCatalog.All())
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;

                if (_unlockedLocal.Contains(entry.id))
                    continue;

                if (!AchievementEvaluator.IsMet(entry, state, snapshot, techs, roads))
                    continue;

                _unlockedLocal.Add(entry.id);

                var apiName = string.IsNullOrEmpty(entry.steamApiName) ? entry.id : entry.steamApiName;
                if (SteamRuntime.IsReady)
                    SteamRuntime.Backend.TryUnlockAchievement(apiName);

                Debug.Log($"[CityMajor] Achievement unlocked: {entry.name} ({apiName})");
                OnAchievementUnlocked?.Invoke(entry);
            }
        }
    }
}
