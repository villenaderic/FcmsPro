using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// Detail page for a single commission - client info, price/balance
/// breakdown, and full payment history. Parity with ClientProfileViewModel's
/// "View" page, added because Commissions rows only had
/// Client/Edit/Duplicate/Delete actions with no drill-down detail view.
/// </summary>
public partial class CommissionProfileViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly NavigationService _navigation;
    private readonly AttachmentService _attachmentService;
    private readonly DialogService _dialogService;

    [ObservableProperty] private Commission? _commission;
    [ObservableProperty] private Client? _client;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<Payment> Payments { get; } = new();
    public ObservableCollection<AttachmentItemViewModel> Attachments { get; } = new();

    public bool HasNoAttachments => Attachments.Count == 0;

    public CommissionProfileViewModel(
        IUnitOfWork uow, NavigationService navigation, AttachmentService attachmentService, DialogService dialogService)
    {
        _uow = uow;
        _navigation = navigation;
        _attachmentService = attachmentService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task LoadAsync(Guid commissionId)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Commission = await _uow.Commissions.GetByIdAsync(commissionId);
            if (Commission is null) return;

            Client = await _uow.Clients.GetByIdAsync(Commission.ClientId);

            var payments = await _uow.Payments.GetByCommissionIdAsync(commissionId);
            Payments.Clear();
            foreach (var p in payments.OrderByDescending(p => p.CreatedAt))
                Payments.Add(p);

            var attachments = await _uow.CommissionAttachments.GetByCommissionIdAsync(commissionId);
            var dir = Data.FcmsPaths.GetAttachmentsDirectory(commissionId);
            Attachments.Clear();
            foreach (var a in attachments)
                Attachments.Add(new AttachmentItemViewModel(a, System.IO.Path.Combine(dir, a.StoredFileName)));
            OnPropertyChanged(nameof(HasNoAttachments));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load commission: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ViewClient()
    {
        if (Commission is not null)
            _navigation.NavigateTo(AppPage.Clients, Commission.ClientId);
    }

    [RelayCommand]
    private async Task AddAttachmentAsync()
    {
        if (Commission is null) return;

        var path = await _dialogService.OpenFileAsync(
            "Select an image", "png", "jpg", "jpeg", "webp", "gif", "bmp");
        if (path is null) return; // user cancelled

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetAttachmentsDirectory(Commission.Id);
            await _attachmentService.AddForCommissionAsync(Commission.Id, path, dir);
            await LoadAsync(Commission.Id);
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
    private async Task RemoveAttachmentAsync(AttachmentItemViewModel? item)
    {
        if (item is null || Commission is null) return;

        ErrorMessage = null;
        try
        {
            var dir = Data.FcmsPaths.GetAttachmentsDirectory(Commission.Id);
            await _attachmentService.RemoveAsync(item.Attachment, dir);
            await LoadAsync(Commission.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove attachment: {ex.Message}";
        }
    }
}

