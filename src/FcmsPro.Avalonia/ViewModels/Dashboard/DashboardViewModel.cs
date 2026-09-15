using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Metrics;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Dashboard;

/// <summary>
/// KPI tiles only - computed live on each load via the shared MetricsService
/// (Phase 1 audit §3: single source of truth for overdue/KPI math, replacing
/// the PWA's separate Dashboard/Commissions-kanban/Invoices-table
/// implementations of the same logic). No caching/materialized values,
/// matching PWA behavior; worth an index/perf check at 100k+ rows per the
/// original migration prompt's Performance Verification section.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly MetricsService _metricsService;
    private readonly NavigationService _navigation;

    [ObservableProperty] private decimal _totalIncome;
    [ObservableProperty] private decimal _thisMonthIncome;
    [ObservableProperty] private decimal _pendingBalance;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _completionRatePercent;
    [ObservableProperty] private int _overdueCount;
    [ObservableProperty] private int _overdueInvoiceCount;
    [ObservableProperty] private int _dueSoonCount;
    [ObservableProperty] private int _dueSoonInvoiceCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public bool HasOverdue => OverdueCount > 0 || OverdueInvoiceCount > 0;
    public bool HasDueSoon => DueSoonCount > 0 || DueSoonInvoiceCount > 0;

    /// <summary>
    /// Composes a natural-language summary of both overdue counts for the
    /// dashboard warning banner - e.g. "3 commissions and 2 invoices are
    /// overdue", or just one half of that if the other is zero. Dashboard is
    /// AppPage.Dashboard, the app's default landing page (NavigationService's
    /// CurrentPage starts there), so this banner is effectively the closest
    /// thing to a startup notification without needing real OS-level toast
    /// integration.
    /// </summary>
    public string OverdueSummaryText
    {
        get
        {
            var parts = new List<string>();
            if (OverdueCount > 0)
                parts.Add(OverdueCount == 1 ? "1 commission" : $"{OverdueCount} commissions");
            if (OverdueInvoiceCount > 0)
                parts.Add(OverdueInvoiceCount == 1 ? "1 invoice" : $"{OverdueInvoiceCount} invoices");

            return parts.Count switch
            {
                0 => string.Empty,
                1 => $"{parts[0]} {(parts[0].StartsWith("1 ") ? "is" : "are")} overdue",
                _ => $"{parts[0]} and {parts[1]} are overdue"
            };
        }
    }

    public DashboardViewModel(MetricsService metricsService, NavigationService navigation)
    {
        _metricsService = metricsService;
        _navigation = navigation;
        _ = LoadAsync();
    }

    /// <summary>
    /// Mirrors OverdueSummaryText above, but for the "still time to act"
    /// banner - e.g. "2 commissions and 1 invoice are due soon". Only ever
    /// covers items that aren't ALSO overdue (MetricsService.IsCommissionDueSoon/
    /// IsInvoiceDueSoon are mutually exclusive with the Is*Overdue checks), so
    /// the two banners never double-count the same item.
    /// </summary>
    public string DueSoonSummaryText
    {
        get
        {
            var parts = new List<string>();
            if (DueSoonCount > 0)
                parts.Add(DueSoonCount == 1 ? "1 commission" : $"{DueSoonCount} commissions");
            if (DueSoonInvoiceCount > 0)
                parts.Add(DueSoonInvoiceCount == 1 ? "1 invoice" : $"{DueSoonInvoiceCount} invoices");

            return parts.Count switch
            {
                0 => string.Empty,
                1 => $"{parts[0]} {(parts[0].StartsWith("1 ") ? "is" : "are")} due within {MetricsService.DueSoonWindowDays} days",
                _ => $"{parts[0]} and {parts[1]} are due within {MetricsService.DueSoonWindowDays} days"
            };
        }
    }

    [RelayCommand]
    private void GoToDueSoonItems() => _navigation.NavigateTo(
        DueSoonCount > 0 ? AppPage.Commissions : AppPage.Invoices);

    [RelayCommand]
    private void GoToOverdueItems() => _navigation.NavigateTo(
        OverdueCount > 0 ? AppPage.Commissions : AppPage.Invoices);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var kpis = await _metricsService.GetDashboardKpisAsync();
            TotalIncome = kpis.TotalIncome;
            ThisMonthIncome = kpis.ThisMonthIncome;
            PendingBalance = kpis.PendingBalance;
            NetProfit = kpis.NetProfit;
            CompletionRatePercent = kpis.CompletionRatePercent;
            OverdueCount = kpis.OverdueCount;
            OverdueInvoiceCount = kpis.OverdueInvoiceCount;
            DueSoonCount = kpis.DueSoonCount;
            DueSoonInvoiceCount = kpis.DueSoonInvoiceCount;
            OnPropertyChanged(nameof(HasOverdue));
            OnPropertyChanged(nameof(OverdueSummaryText));
            OnPropertyChanged(nameof(HasDueSoon));
            OnPropertyChanged(nameof(DueSoonSummaryText));
        }
        catch (System.Exception ex)
        {
            ErrorMessage = $"Could not load dashboard data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
