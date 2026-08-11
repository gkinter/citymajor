using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>Top-center ephemeral status toast (road rejection, tool hints).</summary>
    public sealed class StatusToastController : MonoBehaviour
    {
        const string ToastPath = "Assets/UI/StatusToast.uxml";
        const float DefaultSeconds = 2.4f;

        UIDocument _document;
        VisualElement _root;
        Label _label;
        float _hideAt = -1f;

        static StatusToastController _instance;

        public static StatusToastController Instance => _instance;

        void Awake()
        {
            _instance = this;
            EnsureUi();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            if (_root == null)
                return;

            if (_root.style.display == DisplayStyle.Flex && Time.unscaledTime >= _hideAt)
                _root.style.display = DisplayStyle.None;
        }

        public void Show(string message, float seconds = DefaultSeconds)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EnsureUi();
            if (_label == null || _root == null)
                return;

            _label.text = message;
            _root.style.display = DisplayStyle.Flex;
            _hideAt = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        }

        public static void ShowGlobal(string message, float seconds = DefaultSeconds)
        {
            if (_instance != null)
                _instance.Show(message, seconds);
            else
                Debug.Log($"[CityMajor] {message}");
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
                    Debug.LogError($"[CityMajor] Missing status toast at {ToastPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 125;
            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("status-toast-root");
            _label = docRoot?.Q<Label>("status-toast-label");
        }
    }
}
