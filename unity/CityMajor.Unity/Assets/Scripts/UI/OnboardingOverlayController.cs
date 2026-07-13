using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>First-run welcome overlay — dismissed via PlayerPrefs citymajor.onboarding.v1.</summary>
    public sealed class OnboardingOverlayController : MonoBehaviour
    {
        const string OverlayPath = "Assets/UI/OnboardingOverlay.uxml";
        const string PrefKey = "citymajor.onboarding.v1";

        UIDocument _document;
        VisualElement _root;
        Button _dismissBtn;
        bool _bound;

        public void Configure()
        {
            EnsureUi();

            if (PlayerPrefs.GetInt(PrefKey, 0) == 0)
                Show();
            else
                Hide();
        }

        void EnsureUi()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.visualTreeAsset == null)
            {
                var asset = UiAssetLoader.LoadUxml(OverlayPath);
                if (asset == null)
                {
                    Debug.LogError($"[CityMajor] Missing onboarding overlay at {OverlayPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 150;

            if (_bound)
                return;

            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("onboarding-root");
            _dismissBtn = docRoot?.Q<Button>("onboarding-dismiss");
            _dismissBtn?.RegisterCallback<ClickEvent>(_ => Dismiss());
            _bound = true;
        }

        void Show()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.Flex;
        }

        void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        void Dismiss()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            Hide();
        }
    }
}
