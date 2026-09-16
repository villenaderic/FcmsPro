using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Logs;

/// <summary>
/// Business-facing audit trail (Phase 1 audit §2.10) - distinct from the
/// developer-facing rotating diagnostic log file written via Serilog
/// (App.axaml.cs's ConfigureDiagnosticLogging), which lives in the OS log
/// directory and isn't shown in-app at all.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly NavigationService _navigation;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<AuditLog> Logs { get; } = new();

    public LogsViewModel(IUnitOfWork uow, NavigationService navigation)
    {
        _uow = uow;
        _navigation = navigation;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void BackToSettings() => _navigation.NavigateTo(AppPage.Settings);

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var all = await _uow.AuditLogs.GetAllAsync();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                filtered = filtered.Where(l => l.Message.ToLower().Contains(q));
            }

            Logs.Clear();
            foreach (var l in filtered.OrderByDescending(l => l.Timestamp).Take(500))
                Logs.Add(l);
        }
        catch (System.Exception ex)
        {
            ErrorMessage = $"Could not load logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
