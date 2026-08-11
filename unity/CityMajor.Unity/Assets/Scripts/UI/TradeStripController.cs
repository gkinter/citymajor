using CityMajor.Sim;
using Forge.Game.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityMajor.UI
{
    /// <summary>
    /// Trade routes strip — global market totals + SB-3728 regional NPC markers + create/cancel.
    /// Toggle with Y (T reserved for edge traffic overlay). Esc closes while open.
    /// </summary>
    public sealed class TradeStripController : MonoBehaviour
    {
        const string PanelPath = "Assets/UI/TradeStrip.uxml";

        static readonly Good[] Goods =
        {
            Good.Steel, Good.Coal, Good.IronOre, Good.Wheat, Good.Timber,
            Good.Fuel, Good.Electronics, Good.Food,
        };

        static readonly float[] Quantities = { 20f, 50f, 100f, 150f };
        static readonly int[] Durations = { 6, 12, 18, 24 };

        CitySimBridge _sim;
        UIDocument _document;
        VisualElement _root;
        VisualElement _routesRoot;
        VisualElement _mapMarkersRoot;
        Label _subtitle;
        Label _status;
        Label _employment;
        Label _income;
        Label _expense;
        Label _balance;
        Label _flow;
        Label _interzone;
        Label _demand;
        Label _regionalHint;
        Label _partnerLabel;
        Label _partnerProfile;
        Label _goodLabel;
        Label _qtyLabel;
        Label _monthsLabel;
        Label _createHint;
        Label _createFeedback;
        Button _dirBtn;
        Button _closeBtn;
        Button _createBtn;
        NpcPartnerRow[] _partners = System.Array.Empty<NpcPartnerRow>();
        bool _open;
        bool _export = true;
        int _partnerIdx;
        int _goodIdx;
        int _qtyIdx = 1;
        int _monthsIdx = 1;

        public bool IsOpen => _open;

        public void Configure(CitySimBridge sim)
        {
            _sim = sim;
            EnsureUi();
            _sim.OnStateChanged += OnState;
            OnState(_sim.State);
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.OnStateChanged -= OnState;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Y))
                SetOpen(!_open);

            if (_open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            if (_root != null)
                _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
                OnState(_sim.State);
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
                    Debug.LogError($"[CityMajor] Missing trade strip at {PanelPath}");
                    return;
                }

                _document.visualTreeAsset = asset;
            }

            _document.sortingOrder = 111;
            var docRoot = _document.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("trade-root");
            _subtitle = docRoot?.Q<Label>("trade-subtitle");
            _status = docRoot?.Q<Label>("trade-status");
            _employment = docRoot?.Q<Label>("trade-employment");
            _income = docRoot?.Q<Label>("trade-income");
            _expense = docRoot?.Q<Label>("trade-expense");
            _balance = docRoot?.Q<Label>("trade-balance");
            _flow = docRoot?.Q<Label>("trade-flow");
            _interzone = docRoot?.Q<Label>("trade-interzone");
            _demand = docRoot?.Q<Label>("trade-demand");
            _regionalHint = docRoot?.Q<Label>("trade-regional-hint");
            _mapMarkersRoot = docRoot?.Q<VisualElement>("trade-map-markers");
            _routesRoot = docRoot?.Q<VisualElement>("trade-routes");
            _partnerLabel = docRoot?.Q<Label>("trade-partner-label");
            _partnerProfile = docRoot?.Q<Label>("trade-partner-profile");
            _goodLabel = docRoot?.Q<Label>("trade-good-label");
            _qtyLabel = docRoot?.Q<Label>("trade-qty-label");
            _monthsLabel = docRoot?.Q<Label>("trade-months-label");
            _createHint = docRoot?.Q<Label>("trade-create-hint");
            _createFeedback = docRoot?.Q<Label>("trade-create-feedback");
            _dirBtn = docRoot?.Q<Button>("trade-dir-toggle");
            _closeBtn = docRoot?.Q<Button>("trade-close");
            _createBtn = docRoot?.Q<Button>("trade-create-btn");

            _closeBtn?.RegisterCallback<ClickEvent>(_ => SetOpen(false));
            _createBtn?.RegisterCallback<ClickEvent>(_ => TryCreate());
            _dirBtn?.RegisterCallback<ClickEvent>(_ =>
            {
                _export = !_export;
                RefreshCreateLabels();
            });

            BindCycle("trade-partner-prev", "trade-partner-next", () =>
            {
                EnsurePartners();
                if (_partners.Length == 0) return;
                _partnerIdx = (_partnerIdx + _partners.Length - 1) % _partners.Length;
                RefreshCreateLabels();
                RefreshRegionalMap();
            }, () =>
            {
                EnsurePartners();
                if (_partners.Length == 0) return;
                _partnerIdx = (_partnerIdx + 1) % _partners.Length;
                RefreshCreateLabels();
                RefreshRegionalMap();
            });
            BindCycle("trade-good-prev", "trade-good-next", () =>
            {
                _goodIdx = (_goodIdx + Goods.Length - 1) % Goods.Length;
                RefreshCreateLabels();
            }, () =>
            {
                _goodIdx = (_goodIdx + 1) % Goods.Length;
                RefreshCreateLabels();
            });
            BindCycle("trade-qty-prev", "trade-qty-next", () =>
            {
                _qtyIdx = (_qtyIdx + Quantities.Length - 1) % Quantities.Length;
                RefreshCreateLabels();
            }, () =>
            {
                _qtyIdx = (_qtyIdx + 1) % Quantities.Length;
                RefreshCreateLabels();
            });
            BindCycle("trade-months-prev", "trade-months-next", () =>
            {
                _monthsIdx = (_monthsIdx + Durations.Length - 1) % Durations.Length;
                RefreshCreateLabels();
            }, () =>
            {
                _monthsIdx = (_monthsIdx + 1) % Durations.Length;
                RefreshCreateLabels();
            });

            EnsurePartners();
            RefreshCreateLabels();
            RefreshRegionalMap();
        }

        void BindCycle(string prevName, string nextName, System.Action prev, System.Action next)
        {
            var docRoot = _document.rootVisualElement;
            docRoot?.Q<Button>(prevName)?.RegisterCallback<ClickEvent>(_ => prev());
            docRoot?.Q<Button>(nextName)?.RegisterCallback<ClickEvent>(_ => next());
        }

        void EnsurePartners()
        {
            if (_sim != null)
                _partners = _sim.GetNpcPartners();
            if (_partners == null || _partners.Length == 0)
            {
                // Catalog fallback when bridge is mid-boot
                var catalog = NpcRegionalPartners.All;
                _partners = new NpcPartnerRow[catalog.Count];
                for (var i = 0; i < catalog.Count; i++)
                {
                    var m = catalog[i];
                    _partners[i] = new NpcPartnerRow
                    {
                        Id = m.Id,
                        Name = m.Name,
                        RegionalX = m.RegionalX,
                        RegionalY = m.RegionalY,
                        ExportSpecialty = m.ExportSpecialty,
                        ImportDemand = m.ImportDemand,
                        FreightBaseMonths = NpcRegionalPartners.FreightBaseMonths(m.Id),
                    };
                }
            }

            if (_partnerIdx >= _partners.Length)
                _partnerIdx = 0;
        }

        void RefreshCreateLabels()
        {
            EnsurePartners();
            if (_partners.Length == 0)
                return;

            var partner = _partners[_partnerIdx];
            if (_partnerLabel != null)
                _partnerLabel.text = partner.Name;
            if (_partnerProfile != null)
            {
                _partnerProfile.text =
                    $"Sells {partner.ExportSpecialty} · wants {partner.ImportDemand} · " +
                    $"freight base {partner.FreightBaseMonths} mo";
            }
            if (_goodLabel != null)
                _goodLabel.text = Goods[_goodIdx].ToString();
            if (_qtyLabel != null)
                _qtyLabel.text = $"{Quantities[_qtyIdx]:0}/mo";
            if (_monthsLabel != null)
                _monthsLabel.text = $"{Durations[_monthsIdx]} mo";
            if (_dirBtn != null)
                _dirBtn.text = _export ? "Export +" : "Import −";
            if (_createHint != null)
            {
                _createHint.text =
                    $"Price uses global market; freight ≈ {partner.FreightBaseMonths}–" +
                    $"{Mathf.Min(5, partner.FreightBaseMonths + 2)} mo from regional distance + delay.";
            }
        }

        void RefreshRegionalMap()
        {
            if (_mapMarkersRoot == null)
                return;

            EnsurePartners();
            _mapMarkersRoot.Clear();

            var selectedId = _partners.Length > 0 ? _partners[_partnerIdx].Id : -1;
            for (var i = 0; i < _partners.Length; i++)
            {
                var p = _partners[i];
                var marker = new VisualElement();
                marker.AddToClassList("trade-regional-marker");
                if (p.Id == selectedId)
                    marker.AddToClassList("trade-regional-marker--selected");

                // Regional Y is north-up; UIToolkit bottom is south → left/bottom %.
                marker.style.left = Length.Percent(Mathf.Clamp01(p.RegionalX) * 100f);
                marker.style.bottom = Length.Percent(Mathf.Clamp01(p.RegionalY) * 100f);

                var caption = new Label(ShortName(p.Name));
                caption.AddToClassList("trade-regional-marker__label");
                marker.Add(caption);

                var idx = i;
                marker.RegisterCallback<ClickEvent>(_ =>
                {
                    _partnerIdx = idx;
                    RefreshCreateLabels();
                    RefreshRegionalMap();
                });
                _mapMarkersRoot.Add(marker);
            }

            if (_regionalHint != null)
            {
                _regionalHint.text =
                    $"{_partners.Length} NPC towns · player claim center · " +
                    $"catalog count {Mathf.Max(_partners.Length, _sim?.State.NpcPartnerCount ?? 0)}";
            }
        }

        void TryCreate()
        {
            if (_sim == null)
                return;

            if (!_sim.UsesForgeSimCore)
            {
                SetFeedback("SimCore offline — cannot create routes.");
                return;
            }

            EnsurePartners();
            if (_partners.Length == 0)
            {
                SetFeedback("No NPC partners in catalog.");
                return;
            }

            var partner = _partners[_partnerIdx];
            var good = Goods[_goodIdx];
            var qty = Quantities[_qtyIdx] * (_export ? 1f : -1f);
            var months = Durations[_monthsIdx];

            var ok = _sim.CreateBilateralTradeRoute(partner.Id, good, qty, agreedPrice: 0f, months);
            if (ok)
            {
                SetFeedback($"Created {(_export ? "export" : "import")} {good} ↔ {partner.Name}.");
                OnState(_sim.State);
            }
            else
            {
                var count = Mathf.Max(0, _sim.State.BilateralRouteCount);
                SetFeedback(count >= TradeSystem.MaxBilateralRoutes
                    ? $"At cap ({TradeSystem.MaxBilateralRoutes} bilateral routes)."
                    : "Create failed — unknown partner or bad volume.");
            }
        }

        void SetFeedback(string msg)
        {
            if (_createFeedback != null)
                _createFeedback.text = msg;
        }

        void OnState(CitySimState state)
        {
            if (_balance == null)
                return;

            EnsurePartners();
            var routes = Mathf.Max(0, state.BilateralRouteCount);
            var freight = Mathf.Max(0f, state.MeanFreightMonths);
            var npc = Mathf.Max(state.NpcPartnerCount, _partners.Length);
            if (_subtitle != null)
            {
                _subtitle.text = routes > 0
                    ? $"Regional {npc} NPC · {routes} bilateral · freight {freight:0.#} mo"
                    : $"Regional map · {npc} NPC partners · create contracts [Y]";
            }

            if (_status != null)
            {
                _status.text = routes > 0
                    ? $"Bilateral: {routes} route{(routes == 1 ? "" : "s")} · " +
                      $"notional {state.BilateralTradeValue:N0}/mo · mean freight {freight:0.#} mo"
                    : "No bilateral routes yet. Pick an NPC marker below — auto-trade still fills global gaps.";
            }

            var net = state.MonthlyIncome - state.MonthlyExpense;
            if (_employment != null)
            {
                var empPct = Mathf.RoundToInt(state.EmploymentRate * 100f);
                _employment.text = $"Employment: {empPct}%";
            }

            if (_income != null)
                _income.text = $"Income: +{state.MonthlyIncome:N0}/mo";
            if (_expense != null)
                _expense.text = $"Expenses: −{state.MonthlyExpense:N0}/mo";
            _balance.text = $"Net treasury flow: {(net >= 0 ? "+" : "")}{net:N0}/mo";

            if (_flow != null)
            {
                var tradeSign = state.TradeBalance >= 0 ? "+" : "";
                _flow.text =
                    $"Trade balance: {tradeSign}{state.TradeBalance:N0}/mo · " +
                    $"Exports +{state.MonthlyExportValue:N0} · Imports −{state.MonthlyImportCost:N0}";
            }

            if (_interzone != null)
            {
                _interzone.text =
                    $"Inter-zone volume: {state.InterZoneTradeVolume:N0}/day · " +
                    $"Friction ×{state.MeanInterZoneFriction:F2} · " +
                    $"Delivery {Mathf.RoundToInt(Mathf.Clamp01(state.MeanGoodsDeliveryDelay) * 100f)}% · " +
                    $"Transport {Mathf.RoundToInt(Mathf.Clamp01(state.GoodsTransportCostIndex) * 100f)}%";
            }

            if (_demand != null)
            {
                _demand.text =
                    $"RCI demand (export proxy): R {FormatDemand(state.DemandResidential)} · " +
                    $"C {FormatDemand(state.DemandCommercial)} · I {FormatDemand(state.DemandIndustrial)}";
            }

            if (_open)
            {
                RefreshCreateLabels();
                RefreshRegionalMap();
                RefreshRouteList();
            }
        }

        void RefreshRouteList()
        {
            if (_routesRoot == null || _sim == null)
                return;

            _routesRoot.Clear();
            var rows = _sim.GetBilateralRoutes();
            if (rows.Length == 0)
            {
                var empty = new Label("No partner contracts yet.");
                empty.AddToClassList("panel-note");
                _routesRoot.Add(empty);
                return;
            }

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var line = new VisualElement();
                line.AddToClassList("trade-route-row");

                var dir = row.Quantity >= 0 ? "Exp" : "Imp";
                var partner = PartnerName(row.PartnerCityId);
                var label = new Label(
                    $"{partner} · {row.GoodType} {dir} {Mathf.Abs(row.Quantity):0}/mo · " +
                    $"{row.AgreedPrice:0.#}¤ · {row.RemainingMonths}mo left · freight {row.FreightMonths}");
                label.AddToClassList("trade-route-row__name");
                line.Add(label);

                var cancelIdx = row.BilateralIndex;
                var cancel = new Button(() =>
                {
                    if (_sim.CancelBilateralTradeRoute(cancelIdx))
                    {
                        SetFeedback($"Cancelled route #{cancelIdx + 1}.");
                        OnState(_sim.State);
                    }
                    else
                        SetFeedback("Cancel failed.");
                })
                {
                    text = "Cancel",
                };
                cancel.AddToClassList("trade-route-cancel");
                line.Add(cancel);
                _routesRoot.Add(line);
            }
        }

        string PartnerName(int id)
        {
            EnsurePartners();
            for (var i = 0; i < _partners.Length; i++)
            {
                if (_partners[i].Id == id)
                    return _partners[i].Name;
            }
            return NpcRegionalPartners.NameOrFallback(id);
        }

        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "?";
            var space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        static string FormatDemand(float demand)
        {
            var pct = Mathf.RoundToInt(demand * 100f);
            return pct >= 0 ? $"+{pct}%" : $"{pct}%";
        }
    }
}
