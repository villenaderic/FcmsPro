using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Clients;

/// <summary>
/// Shared create/edit form. Hosted in a modal ClientFormWindow via
/// DialogService.ShowAsync&lt;Client?&gt;() - returns the saved Client on
/// success, or null on cancel.
/// </summary>
public partial class ClientFormViewModel : ObservableObject
{
    private readonly ClientService _clientService;
    private readonly Client? _existing;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ClientType _clientType = ClientType.Individual;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _social;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public bool IsEditMode => _existing is not null;
    public string HeaderText => IsEditMode ? "Edit Client" : "Add Client";
    public IReadOnlyList<ClientType> ClientTypes { get; } = Enum.GetValues<ClientType>();

    /// <summary>Raised with the saved Client on success; the hosting window closes itself with this as the dialog result.</summary>
    public event Action<Client>? Saved;
    public event Action? Cancelled;

    public ClientFormViewModel(ClientService clientService, Client? existing = null)
    {
        _clientService = clientService;
        _existing = existing;

        if (existing is not null)
        {
            Name = existing.Name;
            ClientType = existing.ClientType;
            Phone = existing.Phone;
            Email = existing.Email;
            Social = existing.Social;
            Notes = existing.Notes;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            if (_existing is not null)
            {
                _existing.Name = Name;
                _existing.ClientType = ClientType;
                _existing.Phone = Phone;
                _existing.Email = Email;
                _existing.Social = Social;
                _existing.Notes = Notes;
                await _clientService.UpdateAsync(_existing);
                Saved?.Invoke(_existing);
            }
            else
            {
                var created = await _clientService.CreateAsync(new Client
                {
                    Name = Name,
                    ClientType = ClientType,
                    Phone = Phone,
                    Email = Email,
                    Social = Social,
                    Notes = Notes
                });
                Saved?.Invoke(created);
            }
        }
        catch (ClientValidationException ex)
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
