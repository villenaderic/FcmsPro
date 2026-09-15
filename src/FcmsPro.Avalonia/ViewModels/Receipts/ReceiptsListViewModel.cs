using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Receipts;

/// <summary>
/// Read-only - receipts are auto-generated only when a Payment is recorded
/// (Phase 1 audit §2.4), there's no manual create/edit form. This page just
/// lists them and offers a PDF export via the already-implemented
/// FcmsPro.Pdf.ReceiptRenderer. Receipt is already a denormalized snapshot
/// (ClientName/CommissionTitle/etc. stored directly on the entity), so no
/// dictionary-lookup join is needed here the way Payments needed one.
/// </summary>
public partial class ReceiptsListViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly IReceiptRenderer _receiptRenderer;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _filteredByClient;
    [ObservableProperty] private string? _statusMessage;

    public ObservableCollection<Receipt> Receipts { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in ReceiptsListView.</summary>
    public bool HasNoResults => !IsLoading && Receipts.Count == 0;

    public ReceiptsListViewModel(IUnitOfWork uow, IReceiptRenderer receiptRenderer, DialogService dialogService, NavigationService navigation)
    {
        _uow = uow;
        _receiptRenderer = receiptRenderer;
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
            var all = await _uow.Receipts.GetAllAsync();
            var filtered = all.AsEnumerable();

            if (FilteredByClient is not null)
                filtered = filtered.Where(r => r.ClientId == FilteredByClient.Id);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(q) ||
                    r.ClientName.ToLower().Contains(q) ||
                    r.CommissionTitle.ToLower().Contains(q));
            }

            Receipts.Clear();
            foreach (var r in filtered.OrderByDescending(r => r.CreatedAt))
                Receipts.Add(r);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load receipts: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    [RelayCommand]
    private async Task DownloadPdfAsync(Receipt? receipt)
    {
        if (receipt is null) return;

        StatusMessage = null;
        var path = await _dialogService.SaveFileAsync($"{receipt.ReceiptNumber}.pdf", "Save Receipt PDF");
        if (path is null) return; // user cancelled

        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var bytes = await _receiptRenderer.RenderPdfAsync(receipt, businessSettings);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            StatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save PDF: {ex.Message}";
        }
    }

    /// <summary>
    /// Narrow-column layout sized for an 80mm thermal roll printer, as an
    /// alternative to the standard A5 receipt above - same underlying
    /// Receipt data, different renderer (ReceiptRenderer.RenderThermalPdfAsync).
    /// Most thermal printer drivers accept a PDF and print it directly, so
    /// this is still a "save/print a PDF" flow rather than raw ESC/POS
    /// printer-protocol output.
    /// </summary>
    [RelayCommand]
    private async Task DownloadThermalPdfAsync(Receipt? receipt)
    {
        if (receipt is null) return;

        StatusMessage = null;
        var path = await _dialogService.SaveFileAsync($"{receipt.ReceiptNumber}-thermal.pdf", "Save Thermal Receipt PDF");
        if (path is null) return;

        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var bytes = await _receiptRenderer.RenderThermalPdfAsync(receipt, businessSettings);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            StatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save thermal PDF: {ex.Message}";
        }
    }
}
