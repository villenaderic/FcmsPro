using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Expenses;

public partial class ExpenseFormViewModel : ObservableObject
{
    private readonly ExpenseService _expenseService;
    private readonly Expense? _existing;

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ExpenseCategory _category = ExpenseCategory.Other;
    [ObservableProperty] private decimal? _amount;
    // CalendarDatePicker.SelectedDate is DateTime?, not DateTimeOffset? -
    // see CommissionFormViewModel.Deadline for the full explanation.
    [ObservableProperty] private DateTime _date = DateTime.Now;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private RecurFrequency _recurFrequency = RecurFrequency.None;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public IReadOnlyList<ExpenseCategory> Categories { get; } = Enum.GetValues<ExpenseCategory>();
    public IReadOnlyList<RecurFrequency> RecurFrequencies { get; } = Enum.GetValues<RecurFrequency>();

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Expense" : "New Expense";

    public event Action<Expense>? Saved;
    public event Action? Cancelled;

    public ExpenseFormViewModel(ExpenseService expenseService, Expense? existing = null)
    {
        _expenseService = expenseService;
        _existing = existing;

        if (existing is null) return;

        Description = existing.Description;
        Category = existing.Category;
        Amount = existing.Amount;
        Date = existing.Date.ToDateTime(TimeOnly.MinValue);
        Notes = existing.Notes;
        RecurFrequency = existing.RecurFrequency;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            if (_existing is not null)
            {
                _existing.Description = Description;
                _existing.Category = Category;
                _existing.Amount = Amount ?? 0;
                _existing.Date = DateOnly.FromDateTime(Date);
                _existing.Notes = Notes;
                _existing.RecurFrequency = RecurFrequency;

                await _expenseService.UpdateAsync(_existing);
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = await _expenseService.CreateAsync(new Expense
                {
                    Description = Description,
                    Category = Category,
                    Amount = Amount ?? 0,
                    Date = DateOnly.FromDateTime(Date),
                    Notes = Notes,
                    RecurFrequency = RecurFrequency
                });
                Saved?.Invoke(created);
            }
        }
        catch (ExpenseValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
