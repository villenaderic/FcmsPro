using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Avalonia.Services;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.ViewModels.Clients;
using FcmsPro.Avalonia.ViewModels.Commissions;
using FcmsPro.Avalonia.ViewModels.Payments;
using FcmsPro.Avalonia.ViewModels.Receipts;
using FcmsPro.Avalonia.ViewModels.Invoices;
using FcmsPro.Avalonia.ViewModels.Quotes;
using FcmsPro.Avalonia.ViewModels.Expenses;
using FcmsPro.Avalonia.ViewModels.Goals;
using FcmsPro.Avalonia.ViewModels.Dashboard;
using FcmsPro.Avalonia.ViewModels.Analytics;
using FcmsPro.Avalonia.ViewModels.Settings;
using FcmsPro.Avalonia.ViewModels.Templates;
using FcmsPro.Avalonia.ViewModels.Logs;
using FcmsPro.Avalonia.ViewModels.Trash;
using FcmsPro.Avalonia.ViewModels.Backup;
using Microsoft.Extensions.DependencyInjection;

namespace FcmsPro.Avalonia.ViewModels.Shell;

/// <summary>
/// Root shell ViewModel: sidebar visibility, current page content, and the
/// wiring point for KeySequenceService events. Each navigation gets its own
/// DI scope (disposed when navigating away) so scoped Core services
/// (ClientService etc., which wrap a scoped IUnitOfWork/DbContext) get a
/// fresh instance per page rather than one shared for the app's whole
/// lifetime.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public NavigationService Navigation { get; }
    public KeySequenceService KeySequence { get; }
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private bool _isShortcutsOverlayOpen;

    [ObservableProperty]
    private object? _currentPageViewModel;

    private AsyncServiceScope? _currentPageScope;

    public MainWindowViewModel(NavigationService navigation, KeySequenceService keySequence, DialogService dialogService)
    {
        Navigation = navigation;
        KeySequence = keySequence;
        _dialogService = dialogService;

        // Subscribed to NavigationRequested (fires on every NavigateTo call)
        // rather than Navigation.PropertyChanged for CurrentPage specifically -
        // see the comment on NavigationRequested in NavigationService for why:
        // navigating to the SAME page enum value with a different
        // NavigationParameter (e.g. Clients -> Clients with a new client Id
        // for "View") never raises CurrentPage's own PropertyChanged event,
        // since [ObservableProperty]'s generated setter skips notification
        // when the new value equals the old one. This was the actual root
        // cause of the Clients "View" button silently doing nothing.
        Navigation.NavigationRequested += LoadCurrentPage;

        KeySequence.ToggleSidebarRequested += () => IsSidebarCollapsed = !IsSidebarCollapsed;
        KeySequence.ToggleShortcutsOverlayRequested += () => IsShortcutsOverlayOpen = !IsShortcutsOverlayOpen;
        KeySequence.CreateRequested += OnCreateRequested;
        KeySequence.FocusSearchRequested += () => _ = OpenGlobalSearchAsync();
        // ToggleThemeRequested / RefreshCurrentPageRequested are wired to
        // concrete UI behavior once those pages exist - the events already
        // fire correctly from KeySequenceService today, this is just where
        // the module phases hook in. FocusSearchRequested ("/") now opens
        // the global search window (see OpenGlobalSearchAsync below).

        LoadCurrentPage();
    }

    private void OnCreateRequested(AppPage page)
    {
        // Navigate to the target page first (if not already there), then
        // invoke its AddCommand once the page ViewModel is loaded. Matches
        // the PWA's "nw"/"nc"/etc. shortcuts, which both switch page AND open
        // the create form in one keystroke sequence (Phase 1 audit §3).
        if (Navigation.CurrentPage != page)
            Navigation.NavigateTo(page);

        if (CurrentPageViewModel is ICreatablePage creatable && creatable.AddCommand.CanExecute(null))
            creatable.AddCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void NavigateTo(AppPage page) => Navigation.NavigateTo(page);

    [RelayCommand]
    private async Task OpenGlobalSearchAsync()
    {
        // Own short-lived scope rather than reusing _currentPageScope: this
        // can be triggered from any page (global "/" shortcut), and the
        // current page's scope gets disposed on every navigation - a scope
        // borrowed from it would become invalid mid-search if the search
        // itself causes a navigation. GlobalSearchService/DialogService are
        // both cheap to resolve fresh here.
        await using var scope = App.Services.CreateAsyncScope();
        var searchService = scope.ServiceProvider.GetRequiredService<GlobalSearchService>();
        var vm = new ViewModels.Shared.GlobalSearchViewModel(searchService, Navigation);
        var window = new Views.Shared.GlobalSearchWindow { DataContext = vm };
        vm.CloseRequested += window.Close;

        await _dialogService.ShowAsync<object>(window);
    }

    private void LoadCurrentPage()
    {
        // CreateAsyncScope + async disposal rather than CreateScope + sync
        // Dispose(): IUnitOfWork's implementation (UnitOfWork) only
        // implements IAsyncDisposable, not IDisposable. Nearly every module
        // ViewModel resolved below depends on it (directly or via a Core
        // service), so this scope always ends up holding one - synchronously
        // disposing it threw "type only implements IAsyncDisposable" on
        // EVERY page navigation, not just startup. This method must stay
        // synchronous (called from the constructor and a PropertyChanged
        // handler), so the OLD scope's disposal is deferred to a
        // fire-and-forget async call instead, after the new scope's
        // ViewModel has already been resolved and assigned below.
        var previousScope = _currentPageScope;
        _currentPageScope = App.Services.CreateAsyncScope();
        var sp = _currentPageScope.Value.ServiceProvider;

        switch (Navigation.CurrentPage)
        {
            case AppPage.Dashboard:
                CurrentPageViewModel = sp.GetRequiredService<DashboardViewModel>();
                break;

            case AppPage.Analytics:
                CurrentPageViewModel = sp.GetRequiredService<AnalyticsViewModel>();
                break;

            case AppPage.Settings:
                CurrentPageViewModel = sp.GetRequiredService<SettingsViewModel>();
                break;

            case AppPage.Templates:
                CurrentPageViewModel = sp.GetRequiredService<TemplatesViewModel>();
                break;

            case AppPage.Logs:
                CurrentPageViewModel = sp.GetRequiredService<LogsViewModel>();
                break;

            case AppPage.Trash:
                CurrentPageViewModel = sp.GetRequiredService<TrashViewModel>();
                break;

            case AppPage.Backup:
                CurrentPageViewModel = sp.GetRequiredService<BackupViewModel>();
                break;

            case AppPage.Clients when Navigation.NavigationParameter is Guid clientId:
                var profileVm = sp.GetRequiredService<ClientProfileViewModel>();
                CurrentPageViewModel = profileVm;
                _ = profileVm.LoadAsync(clientId);
                break;

            case AppPage.Clients:
                CurrentPageViewModel = sp.GetRequiredService<ClientsListViewModel>();
                break;

            case AppPage.Commissions:
                CurrentPageViewModel = sp.GetRequiredService<CommissionsListViewModel>();
                break;

            case AppPage.CommissionDetail when Navigation.NavigationParameter is Guid commissionId:
                var commissionProfileVm = sp.GetRequiredService<CommissionProfileViewModel>();
                CurrentPageViewModel = commissionProfileVm;
                _ = commissionProfileVm.LoadAsync(commissionId);
                break;

            case AppPage.Payments:
                CurrentPageViewModel = sp.GetRequiredService<PaymentsListViewModel>();
                break;

            case AppPage.Receipts:
                CurrentPageViewModel = sp.GetRequiredService<ReceiptsListViewModel>();
                break;

            case AppPage.Invoices:
                CurrentPageViewModel = sp.GetRequiredService<InvoicesListViewModel>();
                break;

            case AppPage.Quotes:
                CurrentPageViewModel = sp.GetRequiredService<QuotesListViewModel>();
                break;

            case AppPage.Expenses:
                CurrentPageViewModel = sp.GetRequiredService<ExpensesListViewModel>();
                break;

            case AppPage.Goals:
                CurrentPageViewModel = sp.GetRequiredService<GoalsViewModel>();
                break;

            default:
                CurrentPageViewModel = new PlaceholderPageViewModel(Navigation.CurrentPage);
                break;
        }

        if (previousScope.HasValue)
            _ = DisposePreviousScopeAsync(previousScope.Value);
    }

    private static async Task DisposePreviousScopeAsync(AsyncServiceScope scope)
    {
        try
        {
            await scope.DisposeAsync();
        }
        catch
        {
            // Best-effort cleanup of the previous page's scoped services
            // (DbContext, UnitOfWork, etc.) - a failure here doesn't affect
            // the already-loaded new page, so it's swallowed rather than
            // surfaced to the user.
        }
    }
}
