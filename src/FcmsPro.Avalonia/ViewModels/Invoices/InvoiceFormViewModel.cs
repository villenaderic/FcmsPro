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

namespace FcmsPro.Avalonia.ViewModels.Invoices;

public partial class InvoiceFormViewModel : ObservableObject
{
    private readonly InvoiceService _invoiceService;
    private readonly IUnitOfWork _uow;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;
    private readonly Invoice? _existing;

    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private decimal? _subtotal;
    [ObservableProperty] private decimal? _discount;
    [ObservableProperty] private decimal? _tax;
    // CalendarDatePicker.SelectedDate is DateTime?, not DateTimeOffset? -
    // see CommissionFormViewModel.Deadline for the full explanation.
    [ObservableProperty] private DateTime _issueDate = DateTime.Now;
    [ObservableProperty] private DateTime _dueDate = DateTime.Now.AddDays(30);
    [ObservableProperty] private string? _poNumber;
    [ObservableProperty] private InvoiceStatus _status = InvoiceStatus.Draft;
    [ObservableProperty] private string? _terms;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Live preview, matches InvoiceService's own computation: max(0, Subtotal - Discount + Tax).</summary>
    public decimal Total => Math.Max(0, (Subtotal ?? 0) - (Discount ?? 0) + (Tax ?? 0));

    public ObservableCollection<Client> AvailableClients { get; } = new();
    public IReadOnlyList<InvoiceStatus> Statuses { get; } = Enum.GetValues<InvoiceStatus>();
    public ObservableCollection<InvoiceAttachmentItemViewModel> Attachments { get; } = new();

    public bool HasNoAttachments => Attachments.Count == 0;

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Invoice" : "New Invoice";

    public event Action<Invoice>? Saved;
    public event Action? Cancelled;

    public InvoiceFormViewModel(InvoiceService invoiceService, IUnitOfWork uow, AttachmentService attachmentService,
        DialogService dialogService, Invoice? existing = null)
    {
        _invoiceService = invoiceService;
        _uow = uow;
        _attachmentService = attachmentService;
        _dialogService = dialogService;
        _existing = existing;
        _ = LoadClientsAsync();

        if (existing is null)
        {
            // Defaults mirror the PWA form's behavior for a brand-new invoice
            // (Terms/Notes pre-filled from AppSettings) - loaded async below.
            // Attachments aren't loaded here either - a brand-new invoice has
            // no Id yet to hang an attachments folder off of, so the
            // Attachments section only appears once IsEditMode is true (see
            // InvoiceFormWindow.axaml). Save first, then reopen to attach.
            _ = LoadDefaultTermsAsync();
            return;
        }

        Description = existing.Description;
        Subtotal = existing.Subtotal;
        Discount = existing.Discount;
        Tax = existing.Tax;
        IssueDate = existing.IssueDate.ToDateTime(TimeOnly.MinValue);
        DueDate = existing.DueDate.ToDateTime(TimeOnly.MinValue);
        PoNumber = existing.PoNumber;
        Status = existing.Status;
        Terms = existing.Terms;
        Notes = existing.Notes;
        _ = LoadAttachmentsAsync();
    }

    private async Task LoadAttachmentsAsync()
    {
        if (_existing is null) return;
        try
        {
            var attachments = await _uow.InvoiceAttachments.GetByInvoiceIdAsync(_existing.Id);
            var dir = Data.FcmsPaths.GetInvoiceAttachmentsDirectory(_existing.Id);
            Attachments.Clear();
            foreach (var a in attachments)
                Attachments.Add(new InvoiceAttachmentItemViewModel(a, System.IO.Path.Combine(dir, a.StoredFileName)));
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
            var dir = Data.FcmsPaths.GetInvoiceAttachmentsDirectory(_existing.Id);
            await _attachmentService.AddForInvoiceAsync(_existing.Id, path, dir);
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
    private async Task RemoveAttachmentAsync(InvoiceAttachmentItemViewModel? item)
    {
        if (item is null || _existing is null) return;

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetInvoiceAttachmentsDirectory(_existing.Id);
            await _attachmentService.RemoveAsync(item.Attachment, dir);
            await LoadAttachmentsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove attachment: {ex.Message}";
        }
    }

    partial void OnSubtotalChanged(decimal? value) => OnPropertyChanged(nameof(Total));
    partial void OnDiscountChanged(decimal? value) => OnPropertyChanged(nameof(Total));
    partial void OnTaxChanged(decimal? value) => OnPropertyChanged(nameof(Total));

    private async Task LoadDefaultTermsAsync()
    {
        var settings = await _uow.Settings.GetAppSettingsAsync();
        Terms = settings.InvoiceTerms;
        Notes = settings.InvoiceNotes;
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
                _existing.Description = Description;
                _existing.Subtotal = Subtotal ?? 0;
                _existing.Discount = Discount ?? 0;
                _existing.Tax = Tax ?? 0;
                _existing.IssueDate = DateOnly.FromDateTime(IssueDate);
                _existing.DueDate = DateOnly.FromDateTime(DueDate);
                _existing.PoNumber = PoNumber;
                _existing.Status = Status;
                _existing.Terms = Terms;
                _existing.Notes = Notes;

                await _invoiceService.UpdateAsync(_existing);
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = await _invoiceService.CreateAsync(new Invoice
                {
                    ClientId = SelectedClient.Id,
                    Description = Description,
                    Subtotal = Subtotal ?? 0,
                    Discount = Discount ?? 0,
                    Tax = Tax ?? 0,
                    IssueDate = DateOnly.FromDateTime(IssueDate),
                    DueDate = DateOnly.FromDateTime(DueDate),
                    PoNumber = PoNumber,
                    Status = Status,
                    Terms = Terms,
                    Notes = Notes
                });
                Saved?.Invoke(created);
            }
        }
        catch (InvoiceValidationException ex)
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
