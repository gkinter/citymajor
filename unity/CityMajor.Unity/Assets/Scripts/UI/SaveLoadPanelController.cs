using System;
using System.IO;
using CityMajor.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Compact bottom-right save/load bar. Writes CMJR v1 to persistentDataPath/citymajor.cmjr.
    /// </summary>
    public sealed class SaveLoadPanelController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/SaveLoadPanel.uxml";
        const string DefaultCityName = "CityMajor";

        CitySimBridge _sim;
        UIDocument _document;
        Label _status;
        Button _saveBtn;
        Button _loadBtn;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            SetStatus($"→ {Path.GetFileName(_sim?.SaveFilePath ?? CitySimBridge.DefaultSaveFileName)}");
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
                    Debug.LogError($"[CityMajor] Missing save/load panel at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 95;
            BindElements();
        }

        void BindElements()
        {
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            _status = root.Q<Label>("save-load-status");
            _saveBtn = root.Q<Button>("save-btn");
            _loadBtn = root.Q<Button>("load-btn");

            if (_saveBtn != null)
                _saveBtn.clicked += OnSaveClicked;
            if (_loadBtn != null)
                _loadBtn.clicked += OnLoadClicked;
        }

        void OnDestroy()
        {
            if (_saveBtn != null)
                _saveBtn.clicked -= OnSaveClicked;
            if (_loadBtn != null)
                _loadBtn.clicked -= OnLoadClicked;
        }

        void OnSaveClicked()
        {
            if (_sim == null || !_sim.UsesForgeSimCore)
            {
                SetStatus("SimCore required");
                return;
            }

            try
            {
                var bytes = _sim.ExportSave(DefaultCityName);
                if (bytes == null || bytes.Length == 0)
                {
                    SetStatus("Export failed");
                    return;
                }

                var path = _sim.SaveFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
                SetStatus($"Saved {bytes.Length / 1024} KB");
                Debug.Log($"[CityMajor] CMJR save written to {path}");
            }
            catch (Exception ex)
            {
                SetStatus("Save error");
                Debug.LogWarning($"[CityMajor] Save failed: {ex.Message}");
            }
        }

        void OnLoadClicked()
        {
            if (_sim == null || !_sim.UsesForgeSimCore)
            {
                SetStatus("SimCore required");
                return;
            }

            var path = _sim.SaveFilePath;
            if (!File.Exists(path))
            {
                SetStatus("No save file");
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (!_sim.LoadSave(bytes))
                {
                    SetStatus("Invalid CMJR");
                    return;
                }

                SetStatus("Loaded");
                Debug.Log($"[CityMajor] CMJR save loaded from {path}");
            }
            catch (Exception ex)
            {
                SetStatus("Load error");
                Debug.LogWarning($"[CityMajor] Load failed: {ex.Message}");
            }
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }
    }
}
