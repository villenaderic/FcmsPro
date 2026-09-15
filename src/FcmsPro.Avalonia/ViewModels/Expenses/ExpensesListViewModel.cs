using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;

namespace FcmsPro.Avalonia.ViewModels.Expenses;

public partial class ExpensesListViewModel : ObservableObject, ICreatablePage
{
    private readonly ExpenseService _expenseService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<Expense> Expenses { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in ExpensesListView.</summary>
    public bool HasNoResults => !IsLoading && Expenses.Count == 0;

    /// <summary>Sum of the currently-loaded/filtered set - handy running total shown at the top of the page.</summary>
    public decimal FilteredTotal => Expenses.Sum(e => e.Amount);

    public ExpensesListViewModel(ExpenseService expenseService, IUnitOfWork uow, DialogService dialogService)
    {
        _expenseService = expenseService;
        _uow = uow;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Constructor calls this fire-and-forget (a ViewModel constructor
        // can't be async) - without a catch here, any failure (DB busy, a
        // stale tracked entity, anything) was previously an unobserved task
        // exception, and this page would just show "No expenses yet"
        // forever with zero indication anything went wrong. Same fix
        // pattern applied across every list-page ViewModel in the app.
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var all = await _uow.Expenses.GetAllAsync();
            var filtered = all.Where(e => !e.IsDeleted);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(e => e.Description.ToLower().Contains(q));
            }

            Expenses.Clear();
            foreach (var e in filtered.OrderByDescending(e => e.Date))
                Expenses.Add(e);

            OnPropertyChanged(nameof(FilteredTotal));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load expenses: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var formVm = new ExpenseFormViewModel(_expenseService);
        await ShowFormAsync(formVm);
    }

    [RelayCommand]
    private async Task EditAsync(Expense? expense)
    {
        if (expense is null) return;
        var formVm = new ExpenseFormViewModel(_expenseService, expense);
        await ShowFormAsync(formVm);
    }

    private async Task ShowFormAsync(ExpenseFormViewModel formVm)
    {
        var window = new Views.Expenses.ExpenseFormWindow { DataContext = formVm };
        Expense? result = null;
        formVm.Saved += e => { result = e; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(Expense? expense)
    {
        if (expense is null) return;
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Expense?", $"\"{expense.Description}\" will be permanently deleted.",
            isDestructive: true, confirmLabel: "Delete");
        if (!confirmed) return;

        await _expenseService.DeleteAsync(expense);
        await LoadAsync();
    }
}
