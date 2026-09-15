using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Metrics;

namespace FcmsPro.Avalonia.ViewModels.Goals;

/// <summary>
/// Collapsed to a single-row settings-style entity rather than a table
/// (Phase 2 §2 default) - no list, no per-item CRUD, just four optional
/// target numbers plus a live snapshot of progress this month/year via
/// MetricsService.
/// </summary>
public partial class GoalsViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly MetricsService _metricsService;

    [ObservableProperty] private decimal? _monthlyIncomeGoal;
    [ObservableProperty] private decimal? _yearlyIncomeGoal;
    [ObservableProperty] private decimal? _monthlyExpenseBudget;
    [ObservableProperty] private decimal? _yearlyExpenseBudget;

    [ObservableProperty] private decimal _thisMonthIncome;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;

    public GoalsViewModel(IUnitOfWork uow, MetricsService metricsService)
    {
        _uow = uow;
        _metricsService = metricsService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var goals = await _uow.Settings.GetGoalSettingsAsync();
            MonthlyIncomeGoal = goals.MonthlyIncomeGoal;
            YearlyIncomeGoal = goals.YearlyIncomeGoal;
            MonthlyExpenseBudget = goals.MonthlyExpenseBudget;
            YearlyExpenseBudget = goals.YearlyExpenseBudget;

            var kpis = await _metricsService.GetDashboardKpisAsync();
            ThisMonthIncome = kpis.ThisMonthIncome;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load goals: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _uow.Settings.SaveGoalSettingsAsync(new Core.Entities.GoalSettings
            {
                MonthlyIncomeGoal = MonthlyIncomeGoal,
                YearlyIncomeGoal = YearlyIncomeGoal,
                MonthlyExpenseBudget = MonthlyExpenseBudget,
                YearlyExpenseBudget = YearlyExpenseBudget
            });
            await _uow.SaveChangesAsync();
            StatusMessage = "Goals saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save goals: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
