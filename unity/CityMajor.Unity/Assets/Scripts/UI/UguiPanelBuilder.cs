using UnityEngine;
using UnityEngine.UI;

namespace CityMajor.UI
{
    /// <summary>Runtime UGUI helpers for Cathedral Economy / Politics panels.</summary>
    internal static class UguiPanelBuilder
    {
        public static readonly Color PanelBg = new(0.07f, 0.08f, 0.12f, 0.94f);
        public static readonly Color HeaderBg = new(0.12f, 0.14f, 0.2f, 1f);
        public static readonly Color Accent = new(0.45f, 0.72f, 0.95f, 1f);
        public static readonly Color Warn = new(0.95f, 0.55f, 0.25f, 1f);
        public static readonly Color Good = new(0.35f, 0.85f, 0.45f, 1f);
        public static readonly Color Bad = new(0.92f, 0.32f, 0.32f, 1f);
        public static readonly Color Muted = new(0.7f, 0.74f, 0.82f, 1f);

        public static Font DefaultFont =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        public static Canvas EnsureOverlayCanvas(GameObject host, string canvasName, int sortOrder)
        {
            var existing = host.GetComponentInChildren<Canvas>(true);
            if (existing != null)
            {
                existing.sortingOrder = sortOrder;
                return existing;
            }

            var canvasGo = new GameObject(canvasName);
            canvasGo.transform.SetParent(host.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = PanelBg;
            return rt;
        }

        public static Text AddText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor align = TextAnchor.UpperLeft,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        public static Button AddButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.32f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelText = AddText(go.transform, "Label", label, 14, Color.white, TextAnchor.MiddleCenter);
            var labelRt = (RectTransform)labelText.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            return button;
        }

        public static string FormatGoodName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "—";
            return System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        public static string FormatUnits(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return "—";
            if (value >= 100f)
                return value.ToString("0");
            if (value >= 10f)
                return value.ToString("0.0");
            return value.ToString("0.00");
        }

        public static string FormatInventoryRate(float rate)
        {
            if (float.IsNaN(rate))
                return "—";
            var pct = Mathf.RoundToInt(Mathf.Clamp(rate, -1f, 1f) * 100f);
            return pct > 0 ? $"+{pct}%" : $"{pct}%";
        }

        public static string FactionName(byte factionId) => factionId switch
        {
            0 => "Business",
            1 => "Workers",
            2 => "Property",
            3 => "Intelligentsia",
            4 => "Religious",
            5 => "Newcomers",
            _ => $"#{factionId}",
        };

        public static Color FactionColor(byte factionId) => factionId switch
        {
            0 => new Color(0.9f, 0.7f, 0.2f),
            1 => new Color(0.35f, 0.65f, 0.95f),
            2 => new Color(0.35f, 0.85f, 0.4f),
            3 => new Color(0.75f, 0.45f, 0.95f),
            4 => new Color(0.95f, 0.55f, 0.25f),
            5 => new Color(0.4f, 0.85f, 0.85f),
            _ => Muted,
        };
    }
}
