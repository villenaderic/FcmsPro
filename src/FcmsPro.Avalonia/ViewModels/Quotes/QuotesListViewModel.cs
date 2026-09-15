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

namespace FcmsPro.Avalonia.ViewModels.Quotes;

/// <summary>Wraps a Quote with its EffectiveStatus (Expired-by-date, computed via the shared MetricsService) - same pattern as InvoiceRowViewModel.</summary>
public class QuoteRowViewModel
{
    public Quote Quote { get; }
    public string ClientName { get; }
    public QuoteStatus EffectiveStatus { get; }

    public QuoteRowViewModel(Quote quote, string clientName, DateOnly today)
    {
        Quote = quote;
        ClientName = clientName;
        EffectiveStatus = MetricsService.GetEffectiveStatus(quote, today);
    }
}

public partial class QuotesListViewModel : ObservableObject, ICreatablePage
{
    private readonly QuoteService _quoteService;
    private readonly CommissionService _commissionService;
    private readonly IUnitOfWork _uow;
    private readonly IQuoteRenderer _quoteRenderer;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _filteredByClient;
    [ObservableProperty] private string? _statusMessage;

    public ObservableCollection<QuoteRowViewModel> Rows { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in QuotesListView.</summary>
    public bool HasNoResults => !IsLoading && Rows.Count == 0;

    public QuotesListViewModel(QuoteService quoteService, CommissionService commissionService, IUnitOfWork uow,
        IQuoteRenderer quoteRenderer, AttachmentService attachmentService, DialogService dialogService, NavigationService navigation)
    {
        _quoteService = quoteService;
        _commissionService = commissionService;
        _uow = uow;
        _quoteRenderer = quoteRenderer;
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
            var allQuotes = await _uow.Quotes.GetAllAsync();
            var allClients = await _uow.Clients.GetAllAsync();
            var clientsById = allClients.ToDictionary(c => c.Id);
            var today = DateOnly.FromDateTime(DateTime.Today);

            var filtered = allQuotes.Where(q => !q.IsDeleted);
            if (FilteredByClient is not null)
                filtered = filtered.Where(q => q.ClientId == FilteredByClient.Id);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(quote =>
                    quote.QuoteNumber.ToLower().Contains(q) ||
                    quote.Scope.ToLower().Contains(q));
            }

            Rows.Clear();
            foreach (var quote in filtered.OrderByDescending(q => q.CreatedAt))
            {
                var clientName = clientsById.TryGetValue(quote.ClientId, out var c) ? c.Name : "(client deleted)";
                Rows.Add(new QuoteRowViewModel(quote, clientName, today));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load quotes: {ex.Message}";
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
        var formVm = new QuoteFormViewModel(_quoteService, _uow, _attachmentService, _dialogService);
        await ShowFormAsync(formVm);
    }

    [RelayCommand]
    private async Task EditAsync(QuoteRowViewModel? row)
    {
        if (row is null) return;
        var formVm = new QuoteFormViewModel(_quoteService, _uow, _attachmentService, _dialogService, row.Quote);
        await ShowFormAsync(formVm);
    }

    private async Task ShowFormAsync(QuoteFormViewModel formVm)
    {
        var window = new Views.Quotes.QuoteFormWindow { DataContext = formVm };
        Quote? result = null;
        formVm.Saved += q => { result = q; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(QuoteRowViewModel? row)
    {
        if (row is null) return;
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Quote?", $"\"{row.Quote.QuoteNumber}\" will be permanently deleted.",
            isDestructive: true, confirmLabel: "Delete");
        if (!confirmed) return;

        await _quoteService.DeleteAsync(row.Quote);
        await LoadAsync();
    }

    /// <summary>
    /// One-way, manual conversion (Phase 1 audit §2.6 / Phase 2 §2 default):
    /// builds a pre-filled draft Commission and opens it in the normal
    /// Commission create form. Nothing is auto-saved and no back-reference to
    /// the quote is stored - the user still has to hit Save on the resulting
    /// form, same as the PWA's UX.
    /// </summary>
    [RelayCommand]
    private async Task ConvertToCommissionAsync(QuoteRowViewModel? row)
    {
        if (row is null) return;

        if (row.Quote.Status != QuoteStatus.Accepted)
        {
            var confirmed = await _dialogService.ConfirmAsync(
                "Mark as Accepted?",
                "Converting to a commission will mark this quote as Accepted. Continue?",
                confirmLabel: "Continue");
            if (!confirmed) return;
        }

        var draft = await _quoteService.BuildCommissionDraftFromQuoteAsync(row.Quote);
        await LoadAsync(); // reflect the (possibly just-set) Accepted status

        var formVm = new ViewModels.Commissions.CommissionFormViewModel(_commissionService, _uow, draft, isDraft: true);
        var window = new Views.Commissions.CommissionFormWindow { DataContext = formVm };

        formVm.Saved += _ => window.Close();
        formVm.Cancelled += window.Close;
        await _dialogService.ShowAsync<object>(window);
        StatusMessage = "If you saved the commission, you'll find it under Commissions.";
    }

    [RelayCommand]
    private async Task DownloadPdfAsync(QuoteRowViewModel? row)
    {
        if (row is null) return;
        StatusMessage = null;

        var client = await _uow.Clients.GetByIdAsync(row.Quote.ClientId);
        if (client is null)
        {
            StatusMessage = "Cannot generate PDF: the client for this quote no longer exists.";
            return;
        }

        var path = await _dialogService.SaveFileAsync($"{row.Quote.QuoteNumber}.pdf", "Save Quote PDF");
        if (path is null) return;

        try
        {
            var businessSettings = await _uow.Settings.GetAppSettingsAsync();
            var bytes = await _quoteRenderer.RenderPdfAsync(row.Quote, client, businessSettings);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            StatusMessage = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save PDF: {ex.Message}";
        }
    }
}
