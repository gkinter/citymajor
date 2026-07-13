using System.Collections.Generic;
using CityMajor.Platform;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Top-center achievement popup queue (offline + Steam).</summary>
    public sealed class AchievementToastController : MonoBehaviour
    {
        const string ToastPath = "Assets/UI/AchievementToast.uxml";
        const float DisplaySeconds = 3.5f;

        UIDocument _document;
        VisualElement _root;
        Label _title;
        Label _name;
        Label _desc;

        readonly Queue<AchievementEntry> _queue = new();
        float _hideAt = -1f;

        public void BindTracker(SteamAchievementTracker tracker)
        {
            if (tracker != null)
                tracker.OnAchievementUnlocked += Enqueue;
        }

        void OnDestroy()
        {
            // Tracker lifetime matches bootstrap root; no unsubscribe needed if same root.
        }

        void Awake() => EnsureUi();

        void Update()
        {
            if (_root == null)
                return;

            if (_root.style.display == DisplayStyle.Flex && Time.unscaledTime >= _hideAt)
            {
                if (_queue.Count > 0)
                    ShowNext();
                else
                    _root.style.display = DisplayStyle.None;
            }
            else if (_root.style.display == DisplayStyle.None && _queue.Count > 0)
            {
                ShowNext();
            }
        }

        void Enqueue(AchievementEntry entry)
        {
            if (entry == null)
                return;

            _queue.Enqueue(entry);
        }

        void ShowNext()
        {
            if (_queue.Count == 0)
                return;

            EnsureUi();
            var entry = _queue.Dequeue();
            _title.text = entry.hidden ? "Secret Achievement" : "Achievement Unlocked";
            _name.text = entry.name ?? entry.id;
            _desc.text = entry.description ?? "";
            _root.style.display = DisplayStyle.Flex;
            _hideAt = Time.unscaledTime + DisplaySeconds;
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(ToastPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing achievement toast at {ToastPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 130;
            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("achievement-toast-root");
            _title = docRoot?.Q<Label>("achievement-toast-title");
            _name = docRoot?.Q<Label>("achievement-toast-name");
            _desc = docRoot?.Q<Label>("achievement-toast-desc");
        }
    }
}
