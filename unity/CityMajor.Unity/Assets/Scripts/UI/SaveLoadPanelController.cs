using System;
using System.IO;
using CityMajor.Net;
using CityMajor.Platform;
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
        Label _cloudStatus;
        Button _saveBtn;
        Button _loadBtn;
        Button _shareBtn;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            SetStatus($"→ {Path.GetFileName(_sim?.SaveFilePath ?? CitySimBridge.DefaultSaveFileName)}");
            RefreshCloudStatus();
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
            _cloudStatus = root.Q<Label>("save-load-cloud-status");
            _saveBtn = root.Q<Button>("save-btn");
            _loadBtn = root.Q<Button>("load-btn");
            _shareBtn = root.Q<Button>("share-btn");

            RefreshCloudStatus();

            if (_saveBtn != null)
                _saveBtn.clicked += OnSaveClicked;
            if (_loadBtn != null)
                _loadBtn.clicked += OnLoadClicked;
            if (_shareBtn != null)
                _shareBtn.clicked += OnShareClicked;
        }

        void OnDestroy()
        {
            if (_saveBtn != null)
                _saveBtn.clicked -= OnSaveClicked;
            if (_loadBtn != null)
                _loadBtn.clicked -= OnLoadClicked;
            if (_shareBtn != null)
                _shareBtn.clicked -= OnShareClicked;
        }

        void OnShareClicked()
        {
            if (_sim == null)
                return;

            var tick = _sim.LatestSnapshot?.TickCount ?? 0;
            byte[] preview = null;
            if (_sim.UsesForgeSimCore)
            {
                try
                {
                    preview = _sim.ExportSave(DefaultCityName);
                }
                catch
                {
                    // Spectator link still works without preview hash.
                }
            }

            var url = CityShareStub.BuildSpectatorLink(DefaultCityName, tick, preview);
            GUIUtility.systemCopyBuffer = url;
            SetStatus($"Link copied · {CityShareStub.FormatStatusPreview(url)}");
            Debug.Log($"[CityMajor] Spectator stub URL: {url}");
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

                if (SteamRuntime.IsReady)
                {
                    var uploaded = SteamCloudSave.TryUpload(bytes);
                    SetCloudStatus(uploaded ? "Cloud: uploaded" : "Cloud: upload failed");
                }
                else
                    RefreshCloudStatus();
                AchievementProgress.NotifySaved();
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
            byte[] bytes = null;

            if (File.Exists(path))
            {
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    SetStatus("Load error");
                    Debug.LogWarning($"[CityMajor] Local load failed: {ex.Message}");
                    return;
                }
            }
            else if (SteamCloudSave.TryDownload(out var cloudBytes))
            {
                bytes = cloudBytes;
                Debug.Log("[CityMajor] CMJR loaded from Steam Cloud.");
            }
            else
            {
                SetStatus("No save file");
                return;
            }

            try
            {
                if (!_sim.LoadSave(bytes))
                {
                    SetStatus("Invalid CMJR");
                    return;
                }

                SetStatus(File.Exists(path) ? "Loaded" : "Loaded (cloud)");
                RefreshCloudStatus();
                if (File.Exists(path))
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

        void SetCloudStatus(string message)
        {
            if (_cloudStatus != null)
                _cloudStatus.text = message;
        }

        void RefreshCloudStatus()
        {
            if (!SteamRuntime.IsReady)
            {
                SetCloudStatus("Cloud: Steam offline");
                return;
            }

            SetCloudStatus(SteamCloudSave.HasCloudSave() ? "Cloud: uploaded" : "Cloud: ready");
        }
    }
}
