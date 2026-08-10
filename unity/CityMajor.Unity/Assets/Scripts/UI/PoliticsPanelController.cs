using System.Text;
using CityMajor.Sim;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace CityMajor.UI
{
    /// <summary>
    /// UGUI Politics panel — approval + council seats from sim snapshot.
    /// Toggle with G (toolbar button also available). Esc closes while open.
    /// </summary>
    public sealed class PoliticsPanelController : MonoBehaviour
    {
        CitySimBridge _sim;
        GameObject _root;
        Text _approval;
        Text _rating;
        Text _council;
        Text _meta;
        Image[] _seatPips;
        bool _open;

        public bool IsOpen => _open;

        public void Configure(CitySimBridge sim)
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnState;
            }

            _sim = sim;
            EnsureUi();

            if (_sim != null)
            {
                _sim.OnSnapshotChanged += OnSnapshot;
                _sim.OnStateChanged += OnState;
                Refresh();
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnSnapshotChanged -= OnSnapshot;
                _sim.OnStateChanged -= OnState;
            }
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.G))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.SetActive(open);
            if (open)
                Refresh();
        }

        void OnSnapshot(SimSnapshot _) => Refresh();
        void OnState(CitySimState _) => Refresh();

        void EnsureUi()
        {
            if (_root != null)
                return;

            var canvas = UguiPanelBuilder.EnsureOverlayCanvas(gameObject, "EconomyPoliticsCanvas", 200);
            var panel = UguiPanelBuilder.CreatePanel(
                canvas.transform,
                "PoliticsPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(240f, 20f),
                new Vector2(400f, 420f));
            _root = panel.gameObject;

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(panel, false);
            var headerRt = (RectTransform)header.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 48f);
            header.GetComponent<Image>().color = new Color(0.2f, 0.12f, 0.16f, 1f);

            var title = UguiPanelBuilder.AddText(
                header.transform, "Title", "Politics [G]", 18, new Color(0.95f, 0.7f, 0.75f),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(14f, 0f);
            titleRt.offsetMax = new Vector2(-48f, 0f);

            var close = UguiPanelBuilder.AddButton(header.transform, "Close", "✕", new Vector2(36f, 32f));
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            close.onClick.AddListener(() => SetOpen(false));

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(panel, false);
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(16f, 16f);
            bodyRt.offsetMax = new Vector2(-16f, -56f);

            UguiPanelBuilder.AddText(
                body.transform, "ApprovalLabel", "Mayor approval", 13, UguiPanelBuilder.Muted)
                .rectTransform.anchoredPosition = new Vector2(0f, -4f);

            _approval = UguiPanelBuilder.AddText(
                body.transform, "ApprovalValue", "—", 42, UguiPanelBuilder.Good,
                TextAnchor.UpperLeft, FontStyle.Bold);
            var approvalRt = (RectTransform)_approval.transform;
            approvalRt.anchorMin = new Vector2(0f, 1f);
            approvalRt.anchorMax = new Vector2(1f, 1f);
            approvalRt.pivot = new Vector2(0f, 1f);
            approvalRt.anchoredPosition = new Vector2(0f, -28f);
            approvalRt.sizeDelta = new Vector2(0f, 52f);

            _rating = UguiPanelBuilder.AddText(
                body.transform, "Rating", "", 16, UguiPanelBuilder.Muted);
            var ratingRt = (RectTransform)_rating.transform;
            ratingRt.anchorMin = new Vector2(0f, 1f);
            ratingRt.anchorMax = new Vector2(1f, 1f);
            ratingRt.pivot = new Vector2(0f, 1f);
            ratingRt.anchoredPosition = new Vector2(110f, -42f);
            ratingRt.sizeDelta = new Vector2(200f, 28f);

            UguiPanelBuilder.AddText(
                    body.transform, "CouncilLabel", "Council seats", 13, UguiPanelBuilder.Muted)
                .rectTransform.anchoredPosition = new Vector2(0f, -100f);

            var seatsRow = new GameObject("Seats", typeof(RectTransform));
            seatsRow.transform.SetParent(body.transform, false);
            var seatsRt = (RectTransform)seatsRow.transform;
            seatsRt.anchorMin = new Vector2(0f, 1f);
            seatsRt.anchorMax = new Vector2(1f, 1f);
            seatsRt.pivot = new Vector2(0f, 1f);
            seatsRt.anchoredPosition = new Vector2(0f, -124f);
            seatsRt.sizeDelta = new Vector2(0f, 36f);

            _seatPips = new Image[PoliticsSystem.CouncilSeatCount];
            for (var i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
            {
                var pipGo = new GameObject($"Seat{i}", typeof(RectTransform), typeof(Image));
                pipGo.transform.SetParent(seatsRow.transform, false);
                var pipRt = (RectTransform)pipGo.transform;
                pipRt.anchorMin = new Vector2(0f, 0.5f);
                pipRt.anchorMax = new Vector2(0f, 0.5f);
                pipRt.pivot = new Vector2(0f, 0.5f);
                pipRt.anchoredPosition = new Vector2(i * 38f, 0f);
                pipRt.sizeDelta = new Vector2(30f, 30f);
                _seatPips[i] = pipGo.GetComponent<Image>();
                _seatPips[i].color = UguiPanelBuilder.Muted;
            }

            _council = UguiPanelBuilder.AddText(
                body.transform, "CouncilDetail", "", 13, Color.white);
            var councilRt = (RectTransform)_council.transform;
            councilRt.anchorMin = new Vector2(0f, 0f);
            councilRt.anchorMax = new Vector2(1f, 1f);
            councilRt.offsetMin = new Vector2(0f, 48f);
            councilRt.offsetMax = new Vector2(0f, -170f);

            _meta = UguiPanelBuilder.AddText(
                body.transform, "Meta", "", 12, UguiPanelBuilder.Muted);
            var metaRt = (RectTransform)_meta.transform;
            metaRt.anchorMin = new Vector2(0f, 0f);
            metaRt.anchorMax = new Vector2(1f, 0f);
            metaRt.pivot = new Vector2(0f, 0f);
            metaRt.anchoredPosition = Vector2.zero;
            metaRt.sizeDelta = new Vector2(0f, 40f);

            _root.SetActive(false);
        }

        void Refresh()
        {
            if (!_open || _approval == null || _sim == null)
                return;

            var snap = _sim.LatestSnapshot;
            var approval = snap.ApprovalRating > 0f ? snap.ApprovalRating : _sim.State.Approval;
            var pct = Mathf.Clamp01(approval) * 100f;
            _approval.text = $"{pct:0}%";
            _approval.color = ApprovalColor(pct);
            _rating.text = ApprovalLabel(pct);

            var seats = _sim.GetCouncilSeats();
            var counts = new int[6];
            for (var i = 0; i < _seatPips.Length; i++)
            {
                byte faction = i < seats.Length ? seats[i] : (byte)255;
                if (faction < counts.Length)
                    counts[faction]++;
                _seatPips[i].color = UguiPanelBuilder.FactionColor(faction);
            }

            var sb = new StringBuilder(160);
            sb.AppendLine("Composition");
            for (byte f = 0; f < counts.Length; f++)
            {
                if (counts[f] <= 0)
                    continue;
                sb.Append("  • ")
                    .Append(UguiPanelBuilder.FactionName(f))
                    .Append("  ")
                    .Append(counts[f])
                    .Append(counts[f] == 1 ? " seat" : " seats")
                    .AppendLine();
            }

            if (seats.Length == 0)
                sb.AppendLine("  (council data pending)");

            _council.text = sb.ToString().TrimEnd();

            var election = snap.NextElectionYear > 0 ? snap.NextElectionYear.ToString() : "—";
            var laws = snap.ActiveLawCount > 0 ? snap.ActiveLawCount : _sim.State.ActiveLawCount;
            var happiness = snap.Happiness > 0f ? snap.Happiness : _sim.State.Happiness;
            _meta.text =
                $"Next election {election} · Active laws {laws} · Happiness {happiness * 100f:0}%";
        }

        static string ApprovalLabel(float pct)
        {
            if (pct >= 70f) return "Beloved";
            if (pct >= 50f) return "Popular";
            if (pct >= 35f) return "Tolerated";
            if (pct >= 20f) return "Unpopular";
            return "Despised";
        }

        static Color ApprovalColor(float pct)
        {
            if (pct >= 70f) return UguiPanelBuilder.Good;
            if (pct >= 50f) return new Color(0.6f, 0.9f, 0.3f);
            if (pct >= 35f) return new Color(0.9f, 0.9f, 0.25f);
            if (pct >= 20f) return UguiPanelBuilder.Warn;
            return UguiPanelBuilder.Bad;
        }
    }
}
