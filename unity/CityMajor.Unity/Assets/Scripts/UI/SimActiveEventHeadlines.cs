using Forge.SimWasm;

namespace CityMajor.UI
{
    /// <summary>
    /// Herald badge helpers for Cathedral P6 active EventSystem counts
    /// (housing_crisis / housing_shortage / approval_unrest preferencing lives in NarrativeTemplates).
    /// </summary>
    public static class SimActiveEventHeadlines
    {
        public static string FormatCountBadge(int count)
        {
            if (count <= 0)
                return null;
            return count.ToString();
        }

        public static string FormatCountLabel(int count)
        {
            if (count <= 0)
                return "0 active events";
            return count == 1 ? "1 active event" : $"{count} active events";
        }

        public static int ResolveCount(ActiveEventDto[] events) =>
            events?.Length ?? 0;

        public static bool TryGetPriorityEvent(ActiveEventDto[] events, out ActiveEventDto evt)
        {
            evt = CityMajor.Net.NarrativeTemplates.PreferHeraldEvent(events);
            return evt != null && !string.IsNullOrEmpty(evt.TypeId);
        }

        public static string FormatPriorityHeadline(ActiveEventDto evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.TypeId))
                return "";
            var name = CityMajor.Net.NarrativeTemplates.DisplayNameForEventType(evt.TypeId);
            return string.IsNullOrEmpty(name) ? evt.TypeId.Replace('_', ' ') : name;
        }
    }
}
