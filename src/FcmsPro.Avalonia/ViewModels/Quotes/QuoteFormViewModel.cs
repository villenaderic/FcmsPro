using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Avalonia.Services;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Quotes;

public partial class QuoteFormViewModel : ObservableObject
{
    private readonly QuoteService _quoteService;
    private readonly IUnitOfWork _uow;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;
    private readonly Quote? _existing;

    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private string? _serviceType;
    [ObservableProperty] private string _scope = string.Empty;
    [ObservableProperty] private decimal? _total;
    [ObservableProperty] private decimal? _downPayment;
    [ObservableProperty] private decimal? _revisions = 2; // decimal? to match NumericUpDown.Value's type; cast to int on save (Quote.Revisions is int)
    // CalendarDatePicker.SelectedDate is DateTime?, not DateTimeOffset? -
    // see CommissionFormViewModel.Deadline for the full explanation.
    [ObservableProperty] private DateTime _issueDate = DateTime.Now;
    [ObservableProperty] private DateTime _validUntil = DateTime.Now.AddDays(7);
    [ObservableProperty] private string? _terms;
    [ObservableProperty] private QuoteStatus _status = QuoteStatus.Draft;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Client> AvailableClients { get; } = new();
    public IReadOnlyList<QuoteStatus> Statuses { get; } = Enum.GetValues<QuoteStatus>();
    public ObservableCollection<QuoteAttachmentItemViewModel> Attachments { get; } = new();

    public bool HasNoAttachments => Attachments.Count == 0;

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Quote" : "New Quote";

    public event Action<Quote>? Saved;
    public event Action? Cancelled;

    public QuoteFormViewModel(QuoteService quoteService, IUnitOfWork uow, AttachmentService attachmentService,
        DialogService dialogService, Quote? existing = null)
    {
        _quoteService = quoteService;
        _uow = uow;
        _attachmentService = attachmentService;
        _dialogService = dialogService;
        _existing = existing;
        _ = LoadClientsAsync();

        if (existing is null)
        {
            // Attachments aren't loaded here - a brand-new quote has no Id
            // yet to hang an attachments folder off of. See InvoiceFormViewModel's
            // identical constructor comment.
            _ = LoadDefaultTermsAsync();
            return;
        }

        ServiceType = existing.ServiceType;
        Scope = existing.Scope;
        Total = existing.Total;
        DownPayment = existing.DownPayment;
        Revisions = existing.Revisions;
        IssueDate = existing.IssueDate.ToDateTime(TimeOnly.MinValue);
        ValidUntil = existing.ValidUntil.ToDateTime(TimeOnly.MinValue);
        Terms = existing.Terms;
        Status = existing.Status;
        _ = LoadAttachmentsAsync();
    }

    private async Task LoadAttachmentsAsync()
    {
        if (_existing is null) return;
        try
        {
            var attachments = await _uow.QuoteAttachments.GetByQuoteIdAsync(_existing.Id);
            var dir = Data.FcmsPaths.GetQuoteAttachmentsDirectory(_existing.Id);
            Attachments.Clear();
            foreach (var a in attachments)
                Attachments.Add(new QuoteAttachmentItemViewModel(a, System.IO.Path.Combine(dir, a.StoredFileName)));
            OnPropertyChanged(nameof(HasNoAttachments));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load attachments: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddAttachmentAsync()
    {
        if (_existing is null) return;

        var path = await _dialogService.OpenFileAsync(
            "Select a file", "png", "jpg", "jpeg", "webp", "gif", "bmp", "pdf");
        if (path is null) return; // user cancelled

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetQuoteAttachmentsDirectory(_existing.Id);
            await _attachmentService.AddForQuoteAsync(_existing.Id, path, dir);
            await LoadAttachmentsAsync();
        }
        catch (AttachmentService.AttachmentValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not add attachment: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAttachmentAsync(QuoteAttachmentItemViewModel? item)
    {
        if (item is null || _existing is null) return;

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetQuoteAttachmentsDirectory(_existing.Id);
            await _attachmentService.RemoveAsync(item.Attachment, dir);
            await LoadAttachmentsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove attachment: {ex.Message}";
        }
    }

    private async Task LoadDefaultTermsAsync()
    {
        var settings = await _uow.Settings.GetAppSettingsAsync();
        Terms = settings.QuoteTerms;
    }

    private async Task LoadClientsAsync()
    {
        // Same fire-and-forget-from-constructor issue as
        // CommissionFormViewModel.LoadClientsAsync - see there for the full
        // explanation.
        try
        {
            var clients = await _uow.Clients.GetAllAsync();
            AvailableClients.Clear();
            foreach (var c in clients.OrderBy(c => c.Name))
                AvailableClients.Add(c);

            if (_existing is not null)
                SelectedClient = AvailableClients.FirstOrDefault(c => c.Id == _existing.ClientId);

            if (AvailableClients.Count == 0)
                ErrorMessage = "No clients yet - add one from the Clients page first.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load clients: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedClient is null)
        {
            ErrorMessage = "Please select a client.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_existing is not null)
            {
                _existing.ClientId = SelectedClient.Id;
                _existing.ServiceType = ServiceType;
                _existing.Scope = Scope;
                _existing.Total = Total ?? 0;
                _existing.DownPayment = DownPayment;
                _existing.Revisions = (int)(Revisions ?? 2);
                _existing.IssueDate = DateOnly.FromDateTime(IssueDate);
                _existing.ValidUntil = DateOnly.FromDateTime(ValidUntil);
                _existing.Terms = Terms;
                _existing.Status = Status;

                await _quoteService.UpdateAsync(_existing);
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = await _quoteService.CreateAsync(new Quote
                {
                    ClientId = SelectedClient.Id,
                    ServiceType = ServiceType,
                    Scope = Scope,
                    Total = Total ?? 0,
                    DownPayment = DownPayment,
                    Revisions = (int)(Revisions ?? 2),
                    IssueDate = DateOnly.FromDateTime(IssueDate),
                    ValidUntil = DateOnly.FromDateTime(ValidUntil),
                    Terms = Terms,
                    Status = Status
                });
                Saved?.Invoke(created);
            }
        }
        catch (QuoteValidationException ex)
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
