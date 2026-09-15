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
using FcmsPro.Avalonia.ViewModels.Shell;

namespace FcmsPro.Avalonia.ViewModels.Clients;

public partial class ClientsListViewModel : ObservableObject, ICreatablePage
{
    private readonly ClientService _clientService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;
    private readonly NavigationService _navigation;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<Client> Clients { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in ClientsListView.</summary>
    public bool HasNoResults => !IsLoading && Clients.Count == 0;

    public ClientsListViewModel(ClientService clientService, IUnitOfWork uow, DialogService dialogService, NavigationService navigation)
    {
        _clientService = clientService;
        _uow = uow;
        _dialogService = dialogService;
        _navigation = navigation;
        _ = LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var results = await _uow.Clients.SearchAsync(SearchQuery);
            Clients.Clear();
            foreach (var c in results.Where(c => !c.IsDeleted).OrderByDescending(c => c.DateAdded))
                Clients.Add(c);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load clients: {ex.Message}";
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
        var formVm = new ClientFormViewModel(_clientService);
        var window = new Views.Clients.ClientFormWindow { DataContext = formVm };

        Client? result = null;
        formVm.Saved += c => { result = c; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync(Client? client)
    {
        client ??= SelectedClient;
        if (client is null) return;

        var formVm = new ClientFormViewModel(_clientService, client);
        var window = new Views.Clients.ClientFormWindow { DataContext = formVm };

        Client? result = null;
        formVm.Saved += c => { result = c; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(Client? client)
    {
        client ??= SelectedClient;
        if (client is null) return;

        // Matches PWA behavior (Phase 1 audit §4.2): deleting a client does
        // NOT touch their commissions, which are left with a dangling
        // ClientId rather than being cascaded or blocked. The confirm copy
        // below says so explicitly, same as the PWA's dialog text.
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Client?",
            $"\"{client.Name}\" will be moved to the trash (recoverable for 30 days). Any of their commissions will remain but will no longer show a client.",
            isDestructive: true,
            confirmLabel: "Delete");

        if (!confirmed) return;

        await _clientService.DeleteAsync(client);
        await LoadAsync();
    }

    [RelayCommand]
    private void ViewProfile(Client? client)
    {
        client ??= SelectedClient;
        if (client is null) return;
        _navigation.NavigateTo(AppPage.Clients, client.Id);
        // NOTE: profile drill-down within the Clients page (vs. a full page
        // nav) is a reasonable alternative UX - left as a TODO for visual
        // design pass; the ViewModel-level plumbing (ClientProfileViewModel)
        // is ready either way.
    }
}
