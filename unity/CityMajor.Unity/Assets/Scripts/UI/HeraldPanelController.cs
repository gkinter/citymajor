using System.Collections;
using CityMajor.Net;
using CityMajor.Sim;
using Forge.SimWasm;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// The Daily Herald slide panel. Toggle with H. Prefers live SimHost priority events
    /// (housing_crisis / housing_shortage / approval_unrest), else narrative API / templates.
    /// </summary>
    public sealed class HeraldPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/HeraldPanel.uxml";

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        Label _title;
        Label _subtitle;
        Label _status;
        Label _bucket;
        Label _reason;
        Label _headline;
        Label _story;
        Label _eventCount;
        VisualElement _optionsRoot;
        Button _closeBtn;

        bool _open;
        Coroutine _fetchRoutine;
        int _linkedEventId = -1;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnStateChanged += OnStateChanged;
                OnStateChanged(_sim.State);
            }
        }

        void OnStateChanged(CitySimState _) => RefreshEventCountBadge();

        void RefreshEventCountBadge()
        {
            if (_eventCount == null)
                return;

            var count = SimActiveEventHeadlines.ResolveCount(_sim?.LatestActiveEvents);
            var badge = SimActiveEventHeadlines.FormatCountBadge(count);
            if (badge == null)
            {
                _eventCount.text = "";
                _eventCount.style.display = DisplayStyle.None;
                return;
            }

            _eventCount.text = SimActiveEventHeadlines.FormatCountLabel(count);
            _eventCount.style.display = DisplayStyle.Flex;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.H))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnStateChanged;

            if (_fetchRoutine != null)
                StopCoroutine(_fetchRoutine);
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(PanelPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing herald panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 115;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _root = root.Q<VisualElement>("herald-root");
            _title = root.Q<Label>("herald-title");
            _subtitle = root.Q<Label>("herald-subtitle");
            _status = root.Q<Label>("herald-status");
            _bucket = root.Q<Label>("herald-bucket");
            _reason = root.Q<Label>("herald-reason");
            _headline = root.Q<Label>("herald-headline");
            _story = root.Q<Label>("herald-story");
            _optionsRoot = root.Q<VisualElement>("herald-options");
            _closeBtn = root.Q<Button>("herald-close");
            _eventCount = root.Q<Label>("herald-event-count");

            if (_eventCount == null && _subtitle?.parent != null)
            {
                _eventCount = new Label { name = "herald-event-count" };
                _eventCount.AddToClassList("herald-event-count");
                _eventCount.style.display = DisplayStyle.None;
                _subtitle.parent.Add(_eventCount);
            }

            if (_closeBtn != null)
                _closeBtn.clicked += () => SetOpen(false);

            RefreshEventCountBadge();
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
                BeginFetch();
        }

        void BeginFetch()
        {
            if (_fetchRoutine != null)
                StopCoroutine(_fetchRoutine);

            _linkedEventId = -1;
            ShowLoading();

            var snap = _sim?.LatestSnapshot;
            var state = _sim?.State ?? default;
            var active = _sim?.LatestActiveEvents;

            if (SimActiveEventHeadlines.TryGetPriorityEvent(active, out var priority))
            {
                _linkedEventId = priority.EventId;
                ShowSimPriorityEvent(priority);
                return;
            }

            if (snap == null)
            {
                ShowError("Simulation snapshot unavailable.");
                return;
            }

            _fetchRoutine = StartCoroutine(HeraldApiClient.FetchEventCoroutine(snap, state, OnFetchComplete));
        }

        void ShowSimPriorityEvent(ActiveEventDto priority)
        {
            _fetchRoutine = null;
            RefreshEventCountBadge();

            var narrative = NarrativeTemplates.FromSimEvent(priority.TypeId);
            var headline = SimActiveEventHeadlines.FormatPriorityHeadline(priority);

            if (_title != null)
                _title.text = "The Daily Herald";

            if (_subtitle != null)
                _subtitle.text = SimActiveEventHeadlines.FormatCountLabel(
                    SimActiveEventHeadlines.ResolveCount(_sim?.LatestActiveEvents));

            if (_status != null)
            {
                _status.text = $"Live sim event · {priority.Phase}";
                _status.style.display = DisplayStyle.Flex;
            }

            if (_bucket != null)
            {
                _bucket.text = $"{priority.TypeId.Replace('_', ' ')} · sim";
                _bucket.style.display = DisplayStyle.Flex;
            }

            if (_reason != null)
            {
                _reason.text = $"Story driver: {priority.TypeId}";
                _reason.style.display = DisplayStyle.Flex;
            }

            if (_headline != null)
            {
                _headline.text = string.IsNullOrEmpty(headline) ? narrative.Headline : headline;
                _headline.style.display = DisplayStyle.Flex;
            }

            if (_story != null)
            {
                _story.text = narrative.Body;
                _story.style.display = DisplayStyle.Flex;
            }

            RebuildOptions(narrative);
        }

        void OnFetchComplete(HeraldApiClient.FetchResult result)
        {
            _fetchRoutine = null;

            if (result?.Event == null)
            {
                ShowError("Failed to load Herald story.");
                return;
            }

            var snap = _sim?.LatestSnapshot;
            var state = _sim?.State ?? default;
            var bucket = result.Event.Bucket;
            var goodsShortage = snap != null
                ? snap.GoodsShortageIndex
                : state.GoodsShortageIndex;
            var reason = snap != null
                ? NarrativeTemplates.ExplainBucket(
                    bucket,
                    snap.ApprovalRating * 100f,
                    snap.CityFunds,
                    goodsShortage)
                : "";

            if (_title != null)
                _title.text = "The Daily Herald";

            if (_subtitle != null)
            {
                var countLabel = SimActiveEventHeadlines.FormatCountLabel(
                    SimActiveEventHeadlines.ResolveCount(_sim?.LatestActiveEvents));
                if (result.QuotaRemaining.HasValue)
                    _subtitle.text = $"{countLabel} · narrative remaining today: {result.QuotaRemaining.Value}";
                else if (result.UsedFallback)
                {
                    var shortageLine = NarrativeTemplates.FormatGoodsShortageLine(goodsShortage);
                    _subtitle.text = $"{countLabel} · offline template · {shortageLine}";
                }
                else
                    _subtitle.text = $"{countLabel} · narrative remaining today: …";
            }

            RefreshEventCountBadge();

            if (_status != null)
            {
                if (result.UsedFallback && !string.IsNullOrEmpty(result.Error))
                    _status.text = $"Using local template ({result.Error}).";
                else
                    _status.text = "";
                _status.style.display = string.IsNullOrEmpty(_status.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_bucket != null)
            {
                var bucketLabel = NarrativeTemplates.BucketToApiKey(bucket).Replace('_', ' ');
                _bucket.text = $"{bucketLabel} · {result.Event.Source}";
                _bucket.style.display = DisplayStyle.Flex;
            }

            if (_reason != null)
            {
                _reason.text = string.IsNullOrEmpty(reason) ? "" : $"Story driver: {reason}";
                _reason.style.display = string.IsNullOrEmpty(_reason.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (_headline != null)
            {
                _headline.text = result.Event.Headline;
                _headline.style.display = DisplayStyle.Flex;
            }

            if (_story != null)
            {
                _story.text = result.Event.Body;
                _story.style.display = DisplayStyle.Flex;
            }

            RebuildOptions(result.Event);
        }

        void ShowLoading()
        {
            if (_subtitle != null)
                _subtitle.text = "narrative remaining today: …";
            if (_status != null)
            {
                _status.text = "Fetching today's lead story…";
                _status.style.display = DisplayStyle.Flex;
            }

            HideStoryChrome();
        }

        void ShowError(string message)
        {
            if (_status != null)
            {
                _status.text = message;
                _status.style.display = DisplayStyle.Flex;
            }

            HideStoryChrome();
        }

        void HideStoryChrome()
        {
            if (_bucket != null) _bucket.style.display = DisplayStyle.None;
            if (_reason != null) _reason.style.display = DisplayStyle.None;
            if (_headline != null) _headline.style.display = DisplayStyle.None;
            if (_story != null) _story.style.display = DisplayStyle.None;
            if (_optionsRoot != null) _optionsRoot.style.display = DisplayStyle.None;
        }

        void RebuildOptions(NarrativeTemplates.NarrativeEvent evt)
        {
            if (_optionsRoot == null)
                return;

            _optionsRoot.Clear();
            var heading = new Label { text = "Council options" };
            heading.AddToClassList("herald-options-heading");
            _optionsRoot.Add(heading);

            if (evt.Options == null || evt.Options.Count == 0)
            {
                _optionsRoot.style.display = DisplayStyle.None;
                return;
            }

            _optionsRoot.style.display = DisplayStyle.Flex;

            foreach (var opt in evt.Options)
            {
                var captured = opt;
                var btn = new Button(() => OnOptionSelected(captured.Id))
                {
                    text = "",
                };
                btn.AddToClassList("herald-option-btn");

                var label = new Label(captured.Label);
                label.AddToClassList("herald-option-label");
                btn.Add(label);

                if (!string.IsNullOrEmpty(captured.Tradeoff))
                {
                    var tradeoff = new Label(captured.Tradeoff);
                    tradeoff.AddToClassList("herald-option-tradeoff");
                    btn.Add(tradeoff);
                }

                _optionsRoot.Add(btn);
            }
        }

        void OnOptionSelected(string optionId)
        {
            if (_sim == null || string.IsNullOrEmpty(optionId))
                return;

            _sim.ApplyHeraldOption(optionId, _linkedEventId);
            SetOpen(false);
        }
    }
}
