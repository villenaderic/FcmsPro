using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Avalonia.Services;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Clients;

/// <summary>
/// Client detail page. CommissionCount/TotalPaid/OutstandingBalance are
/// always live-computed via ClientService.GetProfileStatsAsync - never
/// stored on the Client record, matching PWA behavior (Phase 1 audit §3).
/// </summary>
public partial class ClientProfileViewModel : ObservableObject
{
    private readonly ClientService _clientService;
    private readonly IUnitOfWork _uow;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;

    [ObservableProperty] private Client? _client;
    [ObservableProperty] private int _commissionCount;
    [ObservableProperty] private decimal _totalPaid;
    [ObservableProperty] private decimal _outstandingBalance;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<Commission> Commissions { get; } = new();
    public ObservableCollection<ClientAttachmentItemViewModel> Attachments { get; } = new();

    public bool HasNoAttachments => Attachments.Count == 0;

    public ClientProfileViewModel(ClientService clientService, IUnitOfWork uow, AttachmentService attachmentService, DialogService dialogService)
    {
        _clientService = clientService;
        _uow = uow;
        _attachmentService = attachmentService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task LoadAsync(Guid clientId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Client = await _uow.Clients.GetByIdAsync(clientId);
            if (Client is null) return;

            var stats = await _clientService.GetProfileStatsAsync(clientId);
            CommissionCount = stats.CommissionCount;
            TotalPaid = stats.TotalPaid;
            OutstandingBalance = stats.OutstandingBalance;

            var commissions = await _uow.Commissions.GetByClientIdAsync(clientId);
            Commissions.Clear();
            foreach (var c in commissions.OrderByDescending(c => c.DateAdded))
                Commissions.Add(c);

            var attachments = await _uow.ClientAttachments.GetByClientIdAsync(clientId);
            var dir = Data.FcmsPaths.GetClientAttachmentsDirectory(clientId);
            Attachments.Clear();
            foreach (var a in attachments)
                Attachments.Add(new ClientAttachmentItemViewModel(a, System.IO.Path.Combine(dir, a.StoredFileName)));
            OnPropertyChanged(nameof(HasNoAttachments));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddAttachmentAsync()
    {
        if (Client is null) return;

        var path = await _dialogService.OpenFileAsync(
            "Select a file", "png", "jpg", "jpeg", "webp", "gif", "bmp", "pdf");
        if (path is null) return; // user cancelled

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetClientAttachmentsDirectory(Client.Id);
            await _attachmentService.AddForClientAsync(Client.Id, path, dir);
            await LoadAsync(Client.Id);
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
    private async Task RemoveAttachmentAsync(ClientAttachmentItemViewModel? item)
    {
        if (item is null || Client is null) return;

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetClientAttachmentsDirectory(Client.Id);
            await _attachmentService.RemoveAsync(item.Attachment, dir);
            await LoadAsync(Client.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove attachment: {ex.Message}";
        }
    }
}
