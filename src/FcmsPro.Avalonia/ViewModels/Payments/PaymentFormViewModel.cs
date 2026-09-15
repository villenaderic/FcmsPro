using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Payments;

/// <summary>
/// Records a new payment. Only handles create - the PWA has no manual edit
/// form for payments either (a payment's amount/date etc. are effectively
/// immutable once recorded; correcting a mistake goes through Refund +
/// re-record, matching PWA behavior). Calls PaymentService.RecordPaymentAsync,
/// which atomically creates the Payment, decrements the Commission's
/// Remaining balance, and auto-generates the linked Receipt in one
/// transaction (Phase 1 audit §2.3/§2.4).
/// </summary>
public partial class PaymentFormViewModel : ObservableObject
{
    private readonly PaymentService _paymentService;
    private readonly IUnitOfWork _uow;

    [ObservableProperty] private Commission? _selectedCommission;
    [ObservableProperty] private decimal? _amount;
    [ObservableProperty] private PaymentMethod _method = PaymentMethod.Cash;
    // CalendarDatePicker.SelectedDate is DateTime?, not DateTimeOffset? -
    // see CommissionFormViewModel.Deadline for the full explanation.
    [ObservableProperty] private DateTime _date = DateTime.Now;
    [ObservableProperty] private string? _referenceNumber;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Commission> AvailableCommissions { get; } = new();
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    /// <summary>Convenience display value bound in the form so the user can see the balance before typing an amount.</summary>
    public decimal SelectedCommissionRemaining => SelectedCommission?.Remaining ?? 0m;

    public event Action<Payment, Receipt>? Saved;
    public event Action? Cancelled;

    public PaymentFormViewModel(PaymentService paymentService, IUnitOfWork uow, Guid? preselectedCommissionId = null)
    {
        _paymentService = paymentService;
        _uow = uow;
        _ = LoadCommissionsAsync(preselectedCommissionId);
    }

    partial void OnSelectedCommissionChanged(Commission? value) => OnPropertyChanged(nameof(SelectedCommissionRemaining));

    private async Task LoadCommissionsAsync(Guid? preselectedCommissionId)
    {
        // This runs "fire and forget" from the constructor (a Window can't
        // have an async constructor), which means any exception thrown in
        // here was previously swallowed entirely - it would only ever
        // surface as an "UnobservedTaskException" in the crash log, with
        // the dropdown just staying silently, permanently empty and no
        // indication to the user of what went wrong. Wrapping the whole
        // body so a real failure (DB busy, a stale context, etc.) at least
        // shows up as ErrorMessage on the form instead of vanishing.
        try
        {
            IsBusy = true;
            var all = await _uow.Commissions.GetAllAsync();
            // Only commissions with an outstanding balance are worth recording a
            // payment against - matches the PWA's payment amount validation,
            // which rejects amounts exceeding Remaining anyway.
            var payable = all.Where(c => c.Remaining > 0).OrderBy(c => c.Title);

            AvailableCommissions.Clear();
            foreach (var c in payable)
                AvailableCommissions.Add(c);

            if (preselectedCommissionId is { } id)
                SelectedCommission = AvailableCommissions.FirstOrDefault(c => c.Id == id);

            if (AvailableCommissions.Count == 0)
            {
                // Not necessarily a bug - it's also the correct, expected
                // state once every commission is fully paid - but a silent
                // empty dropdown with no explanation reads as broken, so
                // say why plainly instead of leaving it unexplained.
                ErrorMessage = all.Count == 0
                    ? "No commissions yet - add one from the Commissions page first."
                    : "No commissions have an outstanding balance to record a payment against.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load commissions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedCommission is null)
        {
            ErrorMessage = "Please select a commission.";
            return;
        }

        IsBusy = true;
        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var payment = new Payment
            {
                Amount = Amount ?? 0,
                Method = Method,
                Date = DateOnly.FromDateTime(Date),
                ReferenceNumber = ReferenceNumber,
                Notes = Notes
            };

            var (savedPayment, receipt) = await _paymentService.RecordPaymentAsync(payment, SelectedCommission, businessSettings);
            Saved?.Invoke(savedPayment, receipt);
        }
        catch (PaymentValidationException ex)
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
