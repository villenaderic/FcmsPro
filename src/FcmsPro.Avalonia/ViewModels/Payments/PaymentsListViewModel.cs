using System;
using System.Collections.Generic;
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

namespace FcmsPro.Avalonia.ViewModels.Payments;

public partial class PaymentsListViewModel : ObservableObject, ICreatablePage
{
    private readonly PaymentService _paymentService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _filteredByClient;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PaymentRowViewModel> Rows { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in PaymentsListView.</summary>
    public bool HasNoResults => !IsLoading && Rows.Count == 0;

    public PaymentsListViewModel(PaymentService paymentService, IUnitOfWork uow, DialogService dialogService, NavigationService navigation)
    {
        _paymentService = paymentService;
        _uow = uow;
        _dialogService = dialogService;

        if (navigation.NavigationParameter is Guid clientId)
            _ = LoadForClientAsync(clientId);
        else
            _ = LoadAsync();
    }

    private async Task LoadForClientAsync(Guid clientId)
    {
        // Wrapped separately from LoadAsync's own try/catch below - if
        // GetByIdAsync itself throws, LoadAsync would never even be
        // reached, leaving IsLoading permanently true (the page looks stuck
        // loading forever) with no explanation. Falls through to a normal
        // (unfiltered) LoadAsync() on failure rather than leaving the page
        // in that stuck state.
        try
        {
            FilteredByClient = await _uow.Clients.GetByIdAsync(clientId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load client filter: {ex.Message}";
        }
        await LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // Batch-load commissions/clients once so each row's display name
            // resolves via dictionary lookup rather than a query per row
            // (Payment only stores soft-reference IDs, Phase 2 §2).
            var allPayments = await _uow.Payments.GetAllAsync();
            var allCommissions = await _uow.Commissions.GetAllAsync();
            var allClients = await _uow.Clients.GetAllAsync();

            var commissionsById = allCommissions.ToDictionary(c => c.Id);
            var clientsById = allClients.ToDictionary(c => c.Id);

            var filtered = allPayments.Where(p => !p.IsDeleted);

            if (FilteredByClient is not null)
                filtered = filtered.Where(p => p.ClientId == FilteredByClient.Id);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(p =>
                    (p.ReferenceNumber?.ToLower().Contains(q) ?? false) ||
                    (commissionsById.TryGetValue(p.CommissionId, out var c) && c.Title.ToLower().Contains(q)));
            }

            Rows.Clear();
            foreach (var p in filtered.OrderByDescending(p => p.CreatedAt))
            {
                var commissionTitle = commissionsById.TryGetValue(p.CommissionId, out var commission)
                    ? commission.Title
                    : "(commission deleted)";
                var clientName = clientsById.TryGetValue(p.ClientId, out var client)
                    ? client.Name
                    : "(client deleted)";
                Rows.Add(new PaymentRowViewModel(p, commissionTitle, clientName));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load payments: {ex.Message}";
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
        var formVm = new PaymentFormViewModel(_paymentService, _uow);
        var window = new Views.Payments.PaymentFormWindow { DataContext = formVm };

        (Payment, Receipt)? result = null;
        formVm.Saved += (p, r) => { result = (p, r); window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task RefundAsync(PaymentRowViewModel? row)
    {
        if (row is null) return;

        // Hard-deletes the payment, deletes its linked receipt, and
        // recalculates the commission's Remaining from all OTHER payments -
        // matches PWA behavior exactly (not a signed "refund" transaction
        // record), Phase 1 audit §2.3.
        var confirmed = await _dialogService.ConfirmAsync(
            "Refund This Payment?",
            $"This will move the payment of {row.Payment.Amount:C2} for \"{row.CommissionTitle}\" to the trash (recoverable for 30 days), delete its receipt, and restore the balance owed on that commission.",
            isDestructive: true,
            confirmLabel: "Refund");

        if (!confirmed) return;

        var commission = await _uow.Commissions.GetByIdAsync(row.Payment.CommissionId);
        if (commission is null)
        {
            // Commission was deleted after the payment was made (orphaned,
            // matches PWA's no-cascade client/commission delete behavior) -
            // the payment itself can still be safely removed, just with no
            // balance to recalculate.
            await _paymentService.BulkDeleteAsync(new[] { row.Payment });
        }
        else
        {
            await _paymentService.RefundAsync(row.Payment, commission);
        }

        await LoadAsync();
    }
}
