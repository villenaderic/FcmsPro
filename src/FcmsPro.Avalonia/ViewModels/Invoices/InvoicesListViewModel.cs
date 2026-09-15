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
using FcmsPro.Core.Metrics;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;

namespace FcmsPro.Avalonia.ViewModels.Invoices;

/// <summary>
/// Wraps an Invoice with its EffectiveStatus (Overdue-by-date computed via
/// MetricsService, distinct from the literal stored Status) so the row
/// template can display it without duplicating the calculation - Phase 1
/// audit §4.3 / Phase 2 §2: this is the single shared implementation that
/// replaces the PWA's three separate copies of this logic.
/// </summary>
public class InvoiceRowViewModel
{
    public Invoice Invoice { get; }
    public string ClientName { get; }
    public InvoiceStatus EffectiveStatus { get; }

    public InvoiceRowViewModel(Invoice invoice, string clientName, DateOnly today)
    {
        Invoice = invoice;
        ClientName = clientName;
        EffectiveStatus = MetricsService.GetEffectiveStatus(invoice, today);
    }
}

public partial class InvoicesListViewModel : ObservableObject, ICreatablePage
{
    private readonly InvoiceService _invoiceService;
    private readonly IUnitOfWork _uow;
    private readonly IInvoiceRenderer _invoiceRenderer;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _filteredByClient;
    [ObservableProperty] private string? _statusMessage;

    public ObservableCollection<InvoiceRowViewModel> Rows { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in InvoicesListView.</summary>
    public bool HasNoResults => !IsLoading && Rows.Count == 0;

    public InvoicesListViewModel(InvoiceService invoiceService, IUnitOfWork uow, IInvoiceRenderer invoiceRenderer,
        AttachmentService attachmentService, DialogService dialogService, NavigationService navigation)
    {
        _invoiceService = invoiceService;
        _uow = uow;
        _invoiceRenderer = invoiceRenderer;
        _attachmentService = attachmentService;
        _dialogService = dialogService;

        if (navigation.NavigationParameter is Guid clientId)
            _ = LoadForClientAsync(clientId);
        else
            _ = LoadAsync();
    }

    private async Task LoadForClientAsync(Guid clientId)
    {
        try
        {
            FilteredByClient = await _uow.Clients.GetByIdAsync(clientId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load client filter: {ex.Message}";
        }
        await LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var allInvoices = await _uow.Invoices.GetAllAsync();
            var allClients = await _uow.Clients.GetAllAsync();
            var clientsById = allClients.ToDictionary(c => c.Id);
            var today = DateOnly.FromDateTime(DateTime.Today);

            var filtered = allInvoices.Where(i => !i.IsDeleted);
            if (FilteredByClient is not null)
                filtered = filtered.Where(i => i.ClientId == FilteredByClient.Id);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(q) ||
                    i.Description.ToLower().Contains(q));
            }

            Rows.Clear();
            foreach (var i in filtered.OrderByDescending(i => i.CreatedAt))
            {
                var clientName = clientsById.TryGetValue(i.ClientId, out var c) ? c.Name : "(client deleted)";
                Rows.Add(new InvoiceRowViewModel(i, clientName, today));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load invoices: {ex.Message}";
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
        var formVm = new InvoiceFormViewModel(_invoiceService, _uow, _attachmentService, _dialogService);
        await ShowFormAsync(formVm);
    }

    [RelayCommand]
    private async Task EditAsync(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        var formVm = new InvoiceFormViewModel(_invoiceService, _uow, _attachmentService, _dialogService, row.Invoice);
        await ShowFormAsync(formVm);
    }

    private async Task ShowFormAsync(InvoiceFormViewModel formVm)
    {
        var window = new Views.Invoices.InvoiceFormWindow { DataContext = formVm };
        Invoice? result = null;
        formVm.Saved += i => { result = i; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkPaidAsync(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        // No validation against actual Payment records - matches PWA
        // behavior, a known gap flagged in Phase 1 audit §2.5, preserved
        // here for parity rather than silently "fixed".
        try
        {
            await _invoiceService.MarkPaidAsync(row.Invoice);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not mark invoice as paid: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Invoice?", $"\"{row.Invoice.InvoiceNumber}\" will be permanently deleted.",
            isDestructive: true, confirmLabel: "Delete");
        if (!confirmed) return;

        await _invoiceService.DeleteAsync(row.Invoice);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DownloadPdfAsync(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        StatusMessage = null;

        var client = await _uow.Clients.GetByIdAsync(row.Invoice.ClientId);
        if (client is null)
        {
            StatusMessage = "Cannot generate PDF: the client for this invoice no longer exists.";
            return;
        }

        var path = await _dialogService.SaveFileAsync($"{row.Invoice.InvoiceNumber}.pdf", "Save Invoice PDF");
        if (path is null) return;

        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var bytes = await _invoiceRenderer.RenderPdfAsync(row.Invoice, client, businessSettings);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            StatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save PDF: {ex.Message}";
        }
    }
}
