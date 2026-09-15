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

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// Shared create/edit form. Priority is a real persisted field on every
/// commission here (Phase 1 audit §4.1 default - the PWA only wired it into
/// one of two form variants; this scaffold standardizes on always collecting it).
/// </summary>
public partial class CommissionFormViewModel : ObservableObject
{
    private readonly CommissionService _commissionService;
    private readonly IUnitOfWork _uow;
    private readonly Commission? _existing;
    private readonly CommissionStatus _previousStatus;
    private Guid? _pendingClientId;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private string? _serviceType;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _clientNote;
    [ObservableProperty] private decimal? _price;
    [ObservableProperty] private decimal? _downPayment;
    // CalendarDatePicker.SelectedDate is typed DateTime?, not DateTimeOffset?
    // - binding it to a DateTimeOffset property throws InvalidCastException
    // at runtime (confirmed live: "Could not convert '...+08:00'
    // (DateTimeOffset) to Nullable`1[DateTime]"). Using DateTime here to
    // match the control's actual type.
    [ObservableProperty] private DateTime? _deadline;
    [ObservableProperty] private CommissionStatus _status = CommissionStatus.Pending;
    [ObservableProperty] private CommissionPriority _priority = CommissionPriority.Normal;
    [ObservableProperty] private RecurFrequency _recurFrequency = RecurFrequency.None;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<Client> AvailableClients { get; } = new();
    public IReadOnlyList<CommissionStatus> Statuses { get; } = Enum.GetValues<CommissionStatus>();
    public IReadOnlyList<CommissionPriority> Priorities { get; } = Enum.GetValues<CommissionPriority>();
    public IReadOnlyList<RecurFrequency> RecurFrequencies { get; } = Enum.GetValues<RecurFrequency>();

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Commission" : "New Commission";

    public event Action<Commission>? Saved;
    public event Action? Cancelled;

    public CommissionFormViewModel(CommissionService commissionService, IUnitOfWork uow, Commission? existing = null)
    {
        _commissionService = commissionService;
        _uow = uow;
        _existing = existing;
        _previousStatus = existing?.Status ?? CommissionStatus.Pending;

        _ = LoadClientsAsync();

        if (existing is not null)
        {
            Title = existing.Title;
            ServiceType = existing.ServiceType;
            Description = existing.Description;
            ClientNote = existing.ClientNote;
            Price = existing.Price;
            DownPayment = existing.DownPayment;
            Deadline = existing.Deadline.HasValue
                ? existing.Deadline.Value.ToDateTime(TimeOnly.MinValue)
                : null;
            Status = existing.Status;
            Priority = existing.Priority;
            RecurFrequency = existing.RecurFrequency;
        }
    }

    /// <summary>
    /// Pre-fills the form from a Quote or Template conversion draft (Phase 1
    /// audit §2.6/§2.9 - one-way, no-auto-save pattern: the caller builds a
    /// draft Commission via QuoteService/TemplateService and hands it here as
    /// if it were a normal new-commission form, just pre-populated).
    /// </summary>
    public CommissionFormViewModel(CommissionService commissionService, IUnitOfWork uow, Commission draft, bool isDraft)
        : this(commissionService, uow, existing: isDraft ? null : draft)
    {
        if (!isDraft) return;

        Title = draft.Title;
        ServiceType = draft.ServiceType;
        Description = draft.Description;
        Price = draft.Price;
        DownPayment = draft.DownPayment;
        Deadline = draft.Deadline.HasValue
            ? draft.Deadline.Value.ToDateTime(TimeOnly.MinValue)
            : null;
        // ClientId on the draft is set by the caller (e.g. from Quote.ClientId)
        // - resolved to a real Client object once LoadClientsAsync completes.
        _pendingClientId = draft.ClientId;
    }

    /// <summary>
    /// Lets a caller (e.g. this list ViewModel, when filtered to a specific
    /// client's profile) pre-select a client before or after the async client
    /// list has finished loading.
    /// </summary>
    public void PreselectClient(Guid clientId)
    {
        _pendingClientId = clientId;
        var match = AvailableClients.FirstOrDefault(c => c.Id == clientId);
        if (match is not null)
            SelectedClient = match;
    }

    private async Task LoadClientsAsync()
    {
        // Same fire-and-forget-from-constructor issue as
        // PaymentFormViewModel.LoadCommissionsAsync - any exception here was
        // previously swallowed entirely, leaving the Client dropdown
        // silently, permanently empty. See that method for the full
        // explanation.
        try
        {
            var clients = await _uow.Clients.GetAllAsync();
            AvailableClients.Clear();
            foreach (var c in clients.OrderBy(c => c.Name))
                AvailableClients.Add(c);

            if (_pendingClientId is { } id)
                SelectedClient = AvailableClients.FirstOrDefault(c => c.Id == id);
            else if (_existing is not null)
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
            var deadline = Deadline.HasValue ? DateOnly.FromDateTime(Deadline.Value) : (DateOnly?)null;

            if (_existing is not null)
            {
                _existing.Title = Title;
                _existing.ClientId = SelectedClient.Id;
                _existing.ServiceType = ServiceType;
                _existing.Description = Description;
                _existing.ClientNote = ClientNote;
                _existing.Price = Price ?? 0;
                _existing.DownPayment = DownPayment ?? 0;
                _existing.Deadline = deadline;
                _existing.Status = Status;
                _existing.Priority = Priority;
                _existing.RecurFrequency = RecurFrequency;

                await _commissionService.UpdateAsync(_existing, _previousStatus);
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = await _commissionService.CreateAsync(new Commission
                {
                    Title = Title,
                    ClientId = SelectedClient.Id,
                    ServiceType = ServiceType,
                    Description = Description,
                    ClientNote = ClientNote,
                    Price = Price ?? 0,
                    DownPayment = DownPayment ?? 0,
                    Deadline = deadline,
                    Status = Status,
                    Priority = Priority,
                    RecurFrequency = RecurFrequency
                });
                Saved?.Invoke(created);
            }
        }
        catch (CommissionValidationException ex)
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
