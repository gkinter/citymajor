using System;
using System.Collections;
using System.Text;
using Forge.Engine.Simulation;
using UnityEngine;
using UnityEngine.Networking;

namespace CityMajor.Net
{
    /// <summary>
    /// POST client for /api/narrative/event with template fallback when offline or quota-blocked.
    /// </summary>
    public static class HeraldApiClient
    {
        public const string DefaultEndpoint = "http://localhost:3000/api/narrative/event";
        const string EndpointPrefKey = "citymajor.herald_api_url";

        public static string Endpoint
        {
            get => PlayerPrefs.GetString(EndpointPrefKey, DefaultEndpoint);
            set
            {
                PlayerPrefs.SetString(EndpointPrefKey, value);
                PlayerPrefs.Save();
            }
        }

        [Serializable]
        sealed class NarrativeContextJson
        {
            public string cityName;
            public string era;
            public float metricValue;
            public float goodsShortageIndex;
            public float utilityStressIndex;
            public float employmentRate;
            public float approval;
            public int cityFunds;
            public float residentialDemand;
            public float commercialDemand;
            public float industrialDemand;
            public int constructingBuildingCount;
            public float powerCoverageFraction;
            public float waterCoverageFraction;
        }

        [Serializable]
        sealed class NarrativeRequestJson
        {
            public string bucket;
            public NarrativeContextJson context;
        }

        [Serializable]
        sealed class NarrativeOptionJson
        {
            public string id;
            public string label;
            public string tradeoff;
        }

        [Serializable]
        sealed class NarrativeResponseJson
        {
            public string bucket;
            public string headline;
            public string body;
            public string source;
            public NarrativeOptionJson[] options;
            public int narrativeEventsRemaining;
            public string error;
        }

        public sealed class FetchResult
        {
            public bool UsedFallback;
            public string Error;
            public int? QuotaRemaining;
            public NarrativeTemplates.NarrativeEvent Event;
        }

        public static IEnumerator FetchEventCoroutine(
            SimSnapshot snap,
            CityMajor.Sim.CitySimState state,
            Action<FetchResult> onComplete)
        {
            var preview = NarrativeTemplates.FromSnapshot(snap, state);
            var bucketKey = NarrativeTemplates.BucketToApiKey(preview.Bucket);
            var healthcareCoverage = NarrativeTemplates.EstimateHealthcareCoverage(snap);

            var requestBody = new NarrativeRequestJson
            {
                bucket = bucketKey,
                context = new NarrativeContextJson
                {
                    metricValue = healthcareCoverage,
                    era = NarrativeTemplates.EraToApiString(snap.Era),
                    goodsShortageIndex = state.GoodsShortageIndex,
                    utilityStressIndex = state.UtilityStressIndex,
                    employmentRate = state.EmploymentRate,
                    approval = state.Approval,
                    cityFunds = state.Funds,
                    residentialDemand = state.DemandResidential,
                    commercialDemand = state.DemandCommercial,
                    industrialDemand = state.DemandIndustrial,
                    constructingBuildingCount = state.ConstructingBuildingCount,
                    powerCoverageFraction = state.PowerCoverageFraction,
                    waterCoverageFraction = state.WaterCoverageFraction,
                },
            };

            var json = JsonUtility.ToJson(requestBody);
            using var req = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 12;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(BuildFallback(snap, state, req.error));
                yield break;
            }

            NarrativeResponseJson parsed;
            try
            {
                parsed = JsonUtility.FromJson<NarrativeResponseJson>(req.downloadHandler.text);
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(BuildFallback(snap, state, ex.Message));
                yield break;
            }

            if (parsed == null || !string.IsNullOrEmpty(parsed.error) || string.IsNullOrEmpty(parsed.headline))
            {
                var message = parsed?.error ?? "Invalid narrative response";
                onComplete?.Invoke(BuildFallback(snap, state, message));
                yield break;
            }

            var evt = new NarrativeTemplates.NarrativeEvent
            {
                Bucket = NarrativeTemplates.ApiKeyToBucket(parsed.bucket ?? bucketKey),
                Headline = parsed.headline,
                Body = parsed.body ?? "",
                Source = string.IsNullOrEmpty(parsed.source) ? "template" : parsed.source,
            };

            if (parsed.options != null)
            {
                foreach (var opt in parsed.options)
                {
                    if (opt == null || string.IsNullOrEmpty(opt.id))
                        continue;
                    evt.Options.Add(new NarrativeTemplates.NarrativeOption
                    {
                        Id = opt.id,
                        Label = opt.label ?? opt.id,
                        Tradeoff = opt.tradeoff ?? "",
                    });
                }
            }

            onComplete?.Invoke(new FetchResult
            {
                Event = evt,
                UsedFallback = false,
                QuotaRemaining = parsed.narrativeEventsRemaining > 0 ? parsed.narrativeEventsRemaining : null,
            });
        }

        static FetchResult BuildFallback(SimSnapshot snap, CityMajor.Sim.CitySimState state, string error)
        {
            var evt = NarrativeTemplates.FromSnapshot(snap, state);
            return new FetchResult
            {
                Event = evt,
                UsedFallback = true,
                Error = error,
            };
        }
    }
}
