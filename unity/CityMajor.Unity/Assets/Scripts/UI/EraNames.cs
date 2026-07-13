using UnityEngine;

namespace CityMajor.UI
{
    /// <summary>
    /// HUD era labels + badge palette — mirrors web/lib/era.ts (Frontier → Future).
    /// </summary>
    public static class EraNames
    {
        /// <summary>Unity v1 ships Modern-era assets; default badge when sim snapshot is absent.</summary>
        public const int DefaultEra = 3;

        static readonly string[] HudNames =
        {
            "Frontier",
            "Industrial",
            "Postwar",
            "Modern",
            "Future",
        };

        public readonly struct BadgePalette
        {
            public readonly Color Text;
            public readonly Color Background;
            public readonly Color Border;

            public BadgePalette(Color text, Color background, Color border)
            {
                Text = text;
                Background = background;
                Border = border;
            }
        }

        static readonly BadgePalette[] Palettes =
        {
            new(Hex("#E8D5C4"), Rgba(141, 110, 99, 0.28f), Rgba(141, 110, 99, 0.55f)),
            new(Hex("#FFCDD2"), Rgba(183, 28, 28, 0.28f), Rgba(198, 40, 40, 0.55f)),
            new(Hex("#ECEFF1"), Rgba(120, 144, 156, 0.28f), Rgba(144, 164, 174, 0.55f)),
            new(Hex("#BBDEFB"), Rgba(21, 101, 192, 0.28f), Rgba(66, 165, 245, 0.55f)),
            new(Hex("#E1BEE7"), Rgba(123, 31, 162, 0.28f), Rgba(171, 71, 188, 0.55f)),
        };

        static readonly BadgePalette FallbackPalette =
            new(Hex("#E8EEF8"), Rgba(120, 160, 220, 0.15f), Rgba(120, 160, 220, 0.35f));

        public static string HudEraName(int era)
        {
            if (era >= 0 && era < HudNames.Length)
                return HudNames[era];
            return "Unknown";
        }

        public static BadgePalette HudEraBadgePalette(int era)
        {
            if (era >= 0 && era < Palettes.Length)
                return Palettes[era];
            return FallbackPalette;
        }

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return Color.white;
        }

        static Color Rgba(byte r, byte g, byte b, float a) =>
            new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
