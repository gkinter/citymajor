using System.Numerics;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using ImGuiNET;

namespace Forge.Game.UI;

/// <summary>
/// Financial overview panel: revenue/expense breakdown, tax sliders, loans, history graph.
/// Toggled with P key. Reads from SimSnapshot + BudgetSystem for detailed breakdowns.
/// Tax changes enqueued via CommandQueue.
/// </summary>
public sealed class BudgetPanel
{
    public bool IsOpen { get; set; }

    /// <summary>Reference to BudgetSystem for per-category breakdowns.</summary>
    public BudgetSystem? Budget { get; set; }

    /// <summary>Reference to CommandQueue for tax rate changes.</summary>
    public CommandQueue? Commands { get; set; }

    // Local copies of tax rates for slider state (avoid per-frame command spam)
    private float _propertyTaxSlider = 0.09f;
    private float _commercialTaxSlider = 0.10f;
    private float _industrialTaxSlider = 0.12f;
    private bool _slidersInitialized;

    // Income/expense history ring buffer (last 12 months)
    private const int HistorySize = 12;
    private readonly float[] _incomeHistory = new float[HistorySize];
    private readonly float[] _expenseHistory = new float[HistorySize];
    private int _historyIndex;
    private long _lastRecordedMonth;

    public void Draw(SimSnapshot snapshot)
    {
        if (!IsOpen) return;

        // Sync slider state from snapshot on first draw
        if (!_slidersInitialized)
        {
            _propertyTaxSlider = snapshot.PropertyTaxRate;
            _commercialTaxSlider = snapshot.CommercialTaxRate;
            _industrialTaxSlider = snapshot.IndustrialTaxRate;
            _slidersInitialized = true;
        }

        // Record monthly history
        RecordHistory(snapshot);

        var io = ImGui.GetIO();
        float panelWidth = 420;
        float panelHeight = 560;
        float x = (io.DisplaySize.X - panelWidth) * 0.5f;
        float y = 60;

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.FirstUseEver);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.14f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.12f, 0.15f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.15f, 0.2f, 0.3f, 1f));

        bool open = IsOpen;
        if (ImGui.Begin("Budget [P]", ref open))
        {
            IsOpen = open;

            if (ImGui.BeginTabBar("BudgetTabs"))
            {
                if (ImGui.BeginTabItem("Revenue"))
                {
                    DrawRevenueTab(snapshot);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Expenses"))
                {
                    DrawExpenseTab(snapshot);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Taxes"))
                {
                    DrawTaxTab(snapshot);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Loans"))
                {
                    DrawLoanTab(snapshot);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("History"))
                {
                    DrawHistoryTab(snapshot);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            // Net income summary at bottom
            ImGui.Separator();
            DrawNetIncomeSummary(snapshot);
        }
        else
        {
            IsOpen = open;
        }
        ImGui.End();

        ImGui.PopStyleColor(3);
    }

    private void DrawRevenueTab(SimSnapshot snapshot)
    {
        ImGui.Text("Monthly Revenue Breakdown");
        ImGui.Spacing();

        float maxValue = Math.Max(1f, Budget?.TotalRevenue ?? snapshot.MonthlyIncome);

        if (Budget != null)
        {
            DrawBudgetBar("Property Tax", Budget.PropertyTax, maxValue, new Vector4(0.3f, 0.7f, 0.3f, 1f));
            DrawBudgetBar("Commercial Tax", Budget.CommercialTax, maxValue, new Vector4(0.3f, 0.7f, 0.3f, 1f));
            DrawBudgetBar("Industrial Tax", Budget.IndustrialTax, maxValue, new Vector4(0.3f, 0.7f, 0.3f, 1f));
            DrawBudgetBar("Income Tax", Budget.IncomeTax, maxValue, new Vector4(0.4f, 0.7f, 0.4f, 1f));
            DrawBudgetBar("Sales Tax", Budget.SalesTax, maxValue, new Vector4(0.4f, 0.7f, 0.4f, 1f));
            DrawBudgetBar("Transit Fares", Budget.TransitFares, maxValue, new Vector4(0.3f, 0.6f, 0.8f, 1f));
            DrawBudgetBar("Parking Fees", Budget.ParkingFees, maxValue, new Vector4(0.3f, 0.6f, 0.8f, 1f));
            DrawBudgetBar("Trade Income", Budget.TradeIncome, maxValue, new Vector4(0.6f, 0.5f, 0.8f, 1f));
            DrawBudgetBar("Tourism", Budget.TourismIncome, maxValue, new Vector4(0.6f, 0.5f, 0.8f, 1f));
            DrawBudgetBar("Utility Sales", Budget.UtilitySales, maxValue, new Vector4(0.8f, 0.7f, 0.3f, 1f));
            DrawBudgetBar("Govt Grants", Budget.GovernmentGrants, maxValue, new Vector4(0.8f, 0.7f, 0.3f, 1f));

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 0.3f, 1f));
            ImGui.Text($"Total Revenue: ${Budget.TotalRevenue:N0}");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 0.3f, 1f));
            ImGui.Text($"Total Revenue: ${snapshot.MonthlyIncome:N0}");
            ImGui.PopStyleColor();
        }
    }

    private void DrawExpenseTab(SimSnapshot snapshot)
    {
        ImGui.Text("Monthly Expense Breakdown");
        ImGui.Spacing();

        float maxValue = Math.Max(1f, Budget?.TotalExpenses ?? snapshot.MonthlyExpenses);

        if (Budget != null)
        {
            DrawBudgetBar("Road Maintenance", Budget.RoadMaintenance, maxValue, new Vector4(0.9f, 0.4f, 0.3f, 1f));
            DrawBudgetBar("Transit Ops", Budget.TransitOperations, maxValue, new Vector4(0.9f, 0.4f, 0.3f, 1f));
            DrawBudgetBar("Power", Budget.PowerGeneration, maxValue, new Vector4(0.9f, 0.5f, 0.2f, 1f));
            DrawBudgetBar("Water/Sewage", Budget.WaterSewage, maxValue, new Vector4(0.9f, 0.5f, 0.2f, 1f));
            DrawBudgetBar("Fire Dept", Budget.FireDepartment, maxValue, new Vector4(0.8f, 0.3f, 0.3f, 1f));
            DrawBudgetBar("Police Dept", Budget.PoliceDepartment, maxValue, new Vector4(0.8f, 0.3f, 0.3f, 1f));
            DrawBudgetBar("Healthcare", Budget.Healthcare, maxValue, new Vector4(0.7f, 0.4f, 0.7f, 1f));
            DrawBudgetBar("Education", Budget.Education, maxValue, new Vector4(0.4f, 0.5f, 0.8f, 1f));
            DrawBudgetBar("Social Services", Budget.SocialServices, maxValue, new Vector4(0.6f, 0.4f, 0.6f, 1f));
            DrawBudgetBar("Waste Mgmt", Budget.WasteManagement, maxValue, new Vector4(0.6f, 0.5f, 0.3f, 1f));
            DrawBudgetBar("Debt Interest", Budget.DebtInterest, maxValue, new Vector4(0.9f, 0.2f, 0.2f, 1f));
            DrawBudgetBar("Administration", Budget.Administration, maxValue, new Vector4(0.5f, 0.5f, 0.5f, 1f));
            DrawBudgetBar("Research", Budget.ResearchFunding, maxValue, new Vector4(0.3f, 0.6f, 0.9f, 1f));
            DrawBudgetBar("Emergency Fund", Budget.EmergencyFund, maxValue, new Vector4(0.5f, 0.5f, 0.5f, 1f));

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.3f, 0.3f, 1f));
            ImGui.Text($"Total Expenses: ${Budget.TotalExpenses:N0}");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.3f, 0.3f, 1f));
            ImGui.Text($"Total Expenses: ${snapshot.MonthlyExpenses:N0}");
            ImGui.PopStyleColor();
        }
    }

    private void DrawTaxTab(SimSnapshot snapshot)
    {
        ImGui.Text("Tax Rate Adjustment");
        ImGui.Spacing();
        ImGui.TextWrapped("Drag sliders to adjust tax rates. Changes take effect next month.");
        ImGui.Spacing();

        bool changed = false;

        ImGui.PushItemWidth(-1);

        ImGui.Text($"Property Tax: {_propertyTaxSlider * 100f:F1}%");
        if (ImGui.SliderFloat("##PropertyTax", ref _propertyTaxSlider, 0f, 0.20f, "%.1f%%"))
            changed = true;

        ImGui.Spacing();
        ImGui.Text($"Commercial Tax: {_commercialTaxSlider * 100f:F1}%");
        if (ImGui.SliderFloat("##CommercialTax", ref _commercialTaxSlider, 0f, 0.20f, "%.1f%%"))
            changed = true;

        ImGui.Spacing();
        ImGui.Text($"Industrial Tax: {_industrialTaxSlider * 100f:F1}%");
        if (ImGui.SliderFloat("##IndustrialTax", ref _industrialTaxSlider, 0f, 0.20f, "%.1f%%"))
            changed = true;

        ImGui.PopItemWidth();

        // Only enqueue commands when the slider is released (not during drag)
        if (changed && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            SendTaxCommands();
        }

        // Also send on mouse release if we had changes
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && changed)
        {
            SendTaxCommands();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Tax happiness impact indicator
        float avgTax = (_propertyTaxSlider + _commercialTaxSlider + _industrialTaxSlider) / 3f;
        string impact;
        Vector4 impactColor;
        if (avgTax <= 0.08f)
        {
            impact = "Low taxes - citizens are happy, but revenue suffers";
            impactColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
        }
        else if (avgTax <= 0.12f)
        {
            impact = "Moderate taxes - balanced";
            impactColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
        }
        else
        {
            impact = "High taxes - more revenue, but citizens are unhappy";
            impactColor = new Vector4(0.9f, 0.3f, 0.3f, 1f);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, impactColor);
        ImGui.TextWrapped(impact);
        ImGui.PopStyleColor();
    }

    private void DrawLoanTab(SimSnapshot snapshot)
    {
        ImGui.Text("Loan Information");
        ImGui.Spacing();

        ImGui.Text($"Outstanding Balance: ${snapshot.LoanBalance:N0}");

        float interestRate = Budget?.LoanInterestRate ?? 0.05f;
        ImGui.Text($"Interest Rate: {interestRate * 100f:F1}%");

        float monthlyPayment = snapshot.LoanBalance * (interestRate / 12f);
        ImGui.Text($"Monthly Payment: ${monthlyPayment:N0}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Debt rating
        float debtRatio = snapshot.MonthlyIncome > 0
            ? (float)snapshot.LoanBalance / (snapshot.MonthlyIncome * 12f)
            : 10f;

        string rating;
        Vector4 ratingColor;
        if (debtRatio < 0.5f)
        {
            rating = "AAA - Excellent";
            ratingColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
        }
        else if (debtRatio < 1.0f)
        {
            rating = "A - Good";
            ratingColor = new Vector4(0.6f, 0.9f, 0.3f, 1f);
        }
        else if (debtRatio < 2.0f)
        {
            rating = "BB - Fair";
            ratingColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
        }
        else if (debtRatio < 3.0f)
        {
            rating = "C - Poor";
            ratingColor = new Vector4(0.9f, 0.5f, 0.2f, 1f);
        }
        else
        {
            rating = "D - Critical";
            ratingColor = new Vector4(0.9f, 0.2f, 0.2f, 1f);
        }

        ImGui.Text("Credit Rating:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ratingColor);
        ImGui.Text(rating);
        ImGui.PopStyleColor();

        ImGui.Text($"Debt-to-Income: {debtRatio:F2}x");

        if (Budget?.IsBankrupt == true)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.1f, 0.1f, 1f));
            ImGui.TextWrapped("!! BANKRUPTCY WARNING !! City is insolvent.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawHistoryTab(SimSnapshot snapshot)
    {
        ImGui.Text("12-Month Budget History");
        ImGui.Spacing();

        // Income history line
        ImGui.PushStyleColor(ImGuiCol.PlotLines, new Vector4(0.3f, 0.9f, 0.3f, 1f));
        ImGui.PlotLines("Income", ref _incomeHistory[0], HistorySize, _historyIndex,
            $"Income: ${_incomeHistory[(_historyIndex + HistorySize - 1) % HistorySize]:N0}",
            0f, GetHistoryMax(), new Vector2(-1, 80));
        ImGui.PopStyleColor();

        // Expense history line
        ImGui.PushStyleColor(ImGuiCol.PlotLines, new Vector4(0.9f, 0.3f, 0.3f, 1f));
        ImGui.PlotLines("Expenses", ref _expenseHistory[0], HistorySize, _historyIndex,
            $"Expenses: ${_expenseHistory[(_historyIndex + HistorySize - 1) % HistorySize]:N0}",
            0f, GetHistoryMax(), new Vector2(-1, 80));
        ImGui.PopStyleColor();

        ImGui.Spacing();

        // Legend
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 0.3f, 1f));
        ImGui.Text("[---] Income");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.3f, 0.3f, 1f));
        ImGui.Text("[---] Expenses");
        ImGui.PopStyleColor();
    }

    private void DrawNetIncomeSummary(SimSnapshot snapshot)
    {
        long netIncome = snapshot.MonthlyIncome - snapshot.MonthlyExpenses;
        bool positive = netIncome >= 0;

        ImGui.Text("Net Income:");
        ImGui.SameLine();

        Vector4 color = positive
            ? new Vector4(0.3f, 0.9f, 0.3f, 1f)
            : new Vector4(0.9f, 0.3f, 0.3f, 1f);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        string sign = positive ? "+" : "";
        ImGui.Text($"{sign}${netIncome:N0}/mo");
        ImGui.PopStyleColor();

        ImGui.SameLine(0, 20);
        ImGui.Text($"Treasury: ${snapshot.CityFunds:N0}");
    }

    private static void DrawBudgetBar(string label, float value, float maxValue, Vector4 color)
    {
        float fraction = maxValue > 0 ? value / maxValue : 0f;

        ImGui.Text($"{label}:");
        ImGui.SameLine(140);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(fraction, new Vector2(160, 16), $"${value:N0}");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.Text($"${value:N0}");
    }

    private void SendTaxCommands()
    {
        if (Commands == null) return;
        Commands.EnqueueSetTaxRate(0, _propertyTaxSlider);
        Commands.EnqueueSetTaxRate(1, _commercialTaxSlider);
        Commands.EnqueueSetTaxRate(2, _industrialTaxSlider);
    }

    private void RecordHistory(SimSnapshot snapshot)
    {
        // Use tick count as a proxy for month changes (approximate)
        long monthKey = snapshot.TickCount / 43200; // ~30 days * 1440 ticks/day
        if (monthKey != _lastRecordedMonth && monthKey > 0)
        {
            _incomeHistory[_historyIndex] = snapshot.MonthlyIncome;
            _expenseHistory[_historyIndex] = snapshot.MonthlyExpenses;
            _historyIndex = (_historyIndex + 1) % HistorySize;
            _lastRecordedMonth = monthKey;
        }
    }

    private float GetHistoryMax()
    {
        float max = 1f;
        for (int i = 0; i < HistorySize; i++)
        {
            if (_incomeHistory[i] > max) max = _incomeHistory[i];
            if (_expenseHistory[i] > max) max = _expenseHistory[i];
        }
        return max;
    }
}
