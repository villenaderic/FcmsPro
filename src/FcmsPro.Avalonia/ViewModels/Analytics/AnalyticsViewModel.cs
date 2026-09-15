using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Metrics;
using FcmsPro.Avalonia.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace FcmsPro.Avalonia.ViewModels.Analytics;

public class NamedTotal
{
    public string Name { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

/// <summary>
/// Covers the PWA's revenue-by-month, top-5-clients-by-revenue, and
/// expense-by-category breakdowns (Phase 1 audit §3), all computed live on
/// each load from the same Commissions/Payments/Expenses data - no
/// persistence, matching PWA behavior.
///
/// NOTE on the LiveCharts2 integration below: this is the one binding
/// pattern in the whole scaffold I have the least confidence in, since I
/// can't verify LiveChartsCore.SkiaSharpView's exact API surface/version
/// compatibility without compiling. If RevenueSeries/XAxes don't build,
/// the fallback is to drop the CartesianChart from AnalyticsView.axaml and
/// present monthly revenue as a plain list (same pattern as the
/// TopClients/ExpensesByCategory lists below), which needs no chart library
/// at all - flag this back to me and I'll rework it.
/// </summary>
public partial class AnalyticsViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly TaxSummaryService _taxSummaryService;
    private readonly ITaxSummaryRenderer _taxSummaryRenderer;
    private readonly DialogService _dialogService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<NamedTotal> TopClients { get; } = new();
    public ObservableCollection<NamedTotal> ExpensesByCategory { get; } = new();

    public ISeries[] RevenueSeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[] RevenueXAxes { get; private set; } = Array.Empty<Axis>();

    // --- Tax summary ---------------------------------------------------
    public ObservableCollection<int> AvailableYears { get; } = new();
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private TaxSummary? _taxSummary;
    [ObservableProperty] private bool _isExportingTaxSummary;
    [ObservableProperty] private string? _taxSummaryStatusMessage;

    public AnalyticsViewModel(
        IUnitOfWork uow, TaxSummaryService taxSummaryService, ITaxSummaryRenderer taxSummaryRenderer, DialogService dialogService)
    {
        _uow = uow;
        _taxSummaryService = taxSummaryService;
        _taxSummaryRenderer = taxSummaryRenderer;
        _dialogService = dialogService;
        _ = LoadAsync();
        _ = LoadTaxYearsAsync();
    }

    private async Task LoadTaxYearsAsync()
    {
        try
        {
            var years = await _taxSummaryService.GetAvailableYearsAsync();
            AvailableYears.Clear();
            foreach (var y in years)
                AvailableYears.Add(y);

            SelectedYear = years.Count > 0 ? years[0] : DateTime.Today.Year;
            await LoadTaxSummaryAsync();
        }
        catch (Exception ex)
        {
            TaxSummaryStatusMessage = $"Could not load available years: {ex.Message}";
        }
    }

    partial void OnSelectedYearChanged(int value) => _ = LoadTaxSummaryAsync();

    private async Task LoadTaxSummaryAsync()
    {
        try
        {
            TaxSummary = await _taxSummaryService.GetSummaryAsync(SelectedYear);
        }
        catch (Exception ex)
        {
            TaxSummaryStatusMessage = $"Could not load tax summary: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportTaxSummaryAsync()
    {
        if (TaxSummary is null) return;

        TaxSummaryStatusMessage = null;
        var path = await _dialogService.SaveFileAsync($"tax-summary-{SelectedYear}.pdf", "Save Tax Summary PDF");
        if (path is null) return; // user cancelled

        IsExportingTaxSummary = true;
        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var bytes = await _taxSummaryRenderer.RenderPdfAsync(TaxSummary, businessSettings);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            TaxSummaryStatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            TaxSummaryStatusMessage = $"Could not save PDF: {ex.Message}";
        }
        finally
        {
            IsExportingTaxSummary = false;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var allCommissions = await _uow.Commissions.GetAllAsync();
            var allClients = await _uow.Clients.GetAllAsync();
            var allExpenses = await _uow.Expenses.GetAllAsync();
            var clientsById = allClients.ToDictionary(c => c.Id);

            var allPayments = new List<Core.Entities.Payment>();
            foreach (var c in allCommissions)
                allPayments.AddRange(await _uow.Payments.GetByCommissionIdAsync(c.Id));

            // Revenue by month, last 12 months.
            var months = Enumerable.Range(0, 12)
                .Select(i => DateTime.Today.AddMonths(-11 + i))
                .ToList();
            var monthLabels = months.Select(m => m.ToString("MMM")).ToArray();
            var monthValues = months
                .Select(m => (double)allPayments
                    .Where(p => p.Date.Year == m.Year && p.Date.Month == m.Month)
                    .Sum(p => p.Amount))
                .ToArray();

            RevenueSeries = new ISeries[]
            {
                new ColumnSeries<double> { Values = monthValues, Name = "Revenue" }
            };
            RevenueXAxes = new[] { new Axis { Labels = monthLabels } };
            OnPropertyChanged(nameof(RevenueSeries));
            OnPropertyChanged(nameof(RevenueXAxes));

            // Top 5 clients by revenue (sum of payments across all their commissions).
            var revenueByClient = allPayments
                .GroupBy(p => p.ClientId)
                .Select(g => new NamedTotal
                {
                    Name = clientsById.TryGetValue(g.Key, out var c) ? c.Name : "(client deleted)",
                    Total = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Total)
                .Take(5);

            TopClients.Clear();
            foreach (var t in revenueByClient)
                TopClients.Add(t);

            // Expense breakdown by category.
            var byCategory = allExpenses
                .GroupBy(e => e.Category)
                .Select(g => new NamedTotal { Name = g.Key.ToString(), Total = g.Sum(e => e.Amount) })
                .OrderByDescending(x => x.Total);

            ExpensesByCategory.Clear();
            foreach (var c in byCategory)
                ExpensesByCategory.Add(c);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load analytics: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
