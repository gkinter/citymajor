using System;
using System.Text;

namespace CityMajor.Net
{
    /// <summary>v1.5 city share URL scaffold — spectator deep-link (not wired to backend).</summary>
    public static class CityShareStub
    {
        public const string BaseUrl = "https://citymajor.apps.softblaze.net/spectate";

        public static string BuildSpectatorLink(string cityName, long tick, byte[]? cmjrPreview = null)
        {
            var slug = Slugify(cityName);
            var hash = cmjrPreview is { Length: > 0 }
                ? Convert.ToHexString(cmjrPreview.AsSpan(0, Math.Min(8, cmjrPreview.Length))).ToLowerInvariant()
                : "local";

            return $"{BaseUrl}/{slug}?tick={tick}&h={hash}";
        }

        /// <summary>Short label for save-panel status after copy (keeps bar readable).</summary>
        public static string FormatStatusPreview(string url, int maxLen = 42)
        {
            if (string.IsNullOrEmpty(url))
                return "…";

            if (url.Length <= maxLen)
                return url;

            return url.Substring(0, maxLen - 1) + "…";
        }

        static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "city";

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else if (ch == ' ' || ch == '-')
                    sb.Append('-');
            }

            var s = sb.ToString().Trim('-');
            return string.IsNullOrEmpty(s) ? "city" : s;
        }
    }
}
