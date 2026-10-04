using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FcmsPro.Avalonia.Services;

/// <summary>
/// Pages reachable via the sidebar / "g"-prefixed keyboard shortcuts.
/// CommissionDetail is not sidebar-reachable - it's an internal routing
/// target only, used for "View" on a specific commission (distinct from
/// AppPage.Commissions itself, whose NavigationParameter is separately
/// overloaded as an optional "filter list to this client" Guid - reusing
/// the same page+Guid-typed-parameter combination for both meanings would
/// be ambiguous, hence the separate enum value).
/// </summary>
public enum AppPage
{
    Dashboard, Analytics, Clients, Commissions, Payments,
    Expenses, Settings, Logs, Backup, Goals,
    CommissionDetail, Trash
}

/// <summary>
/// Tracks which page is active in the main shell. The actual page ViewModel
/// swap/instantiation happens in MainWindowViewModel, which observes
/// CurrentPage - kept as a plain observable service so any ViewModel can
/// request navigation (e.g. "View Client" from a Commission row) without a
/// direct reference to MainWindowViewModel.
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private AppPage _currentPage = AppPage.Dashboard;

    /// <summary>Optional payload for deep-links, e.g. a Client Id when navigating to that client's profile.</summary>
    public object? NavigationParameter { get; private set; }

    /// <summary>
    /// Fires unconditionally on every NavigateTo call, regardless of whether
    /// CurrentPage's own value actually changed. This is the real bug fix
    /// for "View doesn't do anything when already on that page's list" -
    /// e.g. ClientsListViewModel.ViewProfile() navigates to AppPage.Clients
    /// (the SAME page it's already on) with a new NavigationParameter.
    /// [ObservableProperty]'s generated setter for CurrentPage skips raising
    /// PropertyChanged when the new value equals the old one, so
    /// MainWindowViewModel's page-reload logic - if it only listened to
    /// CurrentPage's PropertyChanged - would never re-run in that case, even
    /// though NavigationParameter was updated. Subscribing to this event
    /// instead sidesteps that equality-check optimization entirely.
    /// </summary>
    public event Action? NavigationRequested;

    public void NavigateTo(AppPage page, object? parameter = null)
    {
        NavigationParameter = parameter;
        CurrentPage = page;
        NavigationRequested?.Invoke();
    }

    /// <summary>Raised when something (e.g. Settings > Replay tour) wants the guided tour (re)started.</summary>
    public event Action? TourRequested;

    public void RequestTour() => TourRequested?.Invoke();
}

/// <summary>
/// Modal dialog host. Windows built for this service should be constructed
/// with a fully-formed ViewModel by the caller (the caller already has
/// access to the scoped services it needs, e.g. ClientService); this service
/// only owns showing/positioning the dialog and returning its result -
/// keeps DialogService itself free of any dependency on Core services.
/// </summary>
public class DialogService
{
    public Window? OwnerWindow { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message, bool isDestructive = false,
        string confirmLabel = "Confirm", string cancelLabel = "Cancel")
    {
        var vm = new ViewModels.Shared.ConfirmDialogViewModel(title, message, isDestructive, confirmLabel, cancelLabel);
        var window = new Views.Shared.ConfirmDialogWindow { DataContext = vm };
        var result = OwnerWindow is not null
            ? await window.ShowDialog<bool?>(OwnerWindow)
            : await ShowWithoutOwnerAsync<bool?>(window);
        return result == true;
    }

    /// <summary>
    /// Shows any pre-constructed dialog Window and awaits its result. The
    /// window's own code-behind is responsible for calling Close(result) on
    /// save/cancel (see ClientFormWindow for the pattern).
    /// </summary>
    public Task<TResult?> ShowAsync<TResult>(Window window) =>
        OwnerWindow is not null
            ? window.ShowDialog<TResult?>(OwnerWindow)
            : ShowWithoutOwnerAsync<TResult?>(window);

    /// <summary>
    /// Opens the native save-file dialog for the given suggested filename.
    /// Returns the chosen local path, or null if the user cancelled.
    /// </summary>
    public async Task<string?> SaveFileAsync(string suggestedFileName, string title = "Save File")
    {
        if (OwnerWindow is null) return null;

        var file = await OwnerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[] { FilePickerFileTypes.Pdf }
        });

        return file?.Path.LocalPath;
    }

    /// <summary>Opens the native open-file dialog. Returns the chosen local path, or null if cancelled.</summary>
    public async Task<string?> OpenFileAsync(string title = "Open File", params string[] extensions)
    {
        if (OwnerWindow is null) return null;

        var fileType = extensions.Length > 0
            ? new FilePickerFileType("Supported files") { Patterns = extensions.Select(e => $"*.{e}").ToArray() }
            : FilePickerFileTypes.All;

        var files = await OwnerWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { fileType }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private static Task<T> ShowWithoutOwnerAsync<T>(Window window)
    {
        // Fallback for the rare case a dialog is requested before OwnerWindow
        // has been set (shouldn't normally happen once MainWindow exists).
        var tcs = new System.Threading.Tasks.TaskCompletionSource<T>();
        window.Closed += (_, _) => tcs.TrySetResult(default!);
        window.Show();
        return tcs.Task;
    }
}

/// <summary>
/// Applies UiPreferences.Theme ("Light"/"Dark"/"System") to
/// Application.Current.RequestedThemeVariant, and AccentColor to the
/// SystemAccentColor resource referenced in App.axaml.
/// </summary>
/// <summary>
/// Every money value in the app is displayed via XAML bindings using the
/// standard "{0:C2}" composite format (e.g. Text="{Binding Total,
/// StringFormat='{}{0:C2}'}"), which formats using CultureInfo.CurrentCulture's
/// currency symbol - NOT the user-configurable AppSettings.CurrencySymbol.
/// On a machine with no real locale configured (or any locale that doesn't
/// match the user's chosen symbol) this silently shows the wrong symbol, or
/// the invariant-culture generic currency sign "\u00a4", instead of what the
/// user picked in Settings > Business Info.
///
/// Rather than touching all ~25 "{0:C2}" bindings across 14 views, this
/// applies the configured symbol to the process's current culture once at
/// startup (and again whenever Settings saves a change), so every existing
/// "C2"-formatted binding picks it up for free. The rest of the currency
/// formatting (grouping separators, decimal point, digit grouping) still
/// comes from the invariant culture, which is the same neutral behavior the
/// PDF renderers use (CurrencySymbol + value.ToString("N2")).
/// </summary>
public static class AppCulture
{
    public static void Apply(string? currencySymbol)
    {
        var symbol = string.IsNullOrWhiteSpace(currencySymbol) ? "\u20b1" : currencySymbol;

        var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.CurrencySymbol = symbol;
        // Invariant default is "$1.00"-style (symbol before, no space); keep
        // that layout so it matches the PDF renderers' "{symbol}{amount}" output.
        culture.NumberFormat.CurrencyPositivePattern = 0;
        culture.NumberFormat.CurrencyNegativePattern = 0;

        System.Globalization.CultureInfo.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
    }
}

public class ThemeService
{
    public void Apply(string theme, string accentColorHex)
    {
        if (Application.Current is null) return;

        Application.Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (Color.TryParse(accentColorHex, out var color))
            Application.Current.Resources["SystemAccentColor"] = color;
    }
}

/// <summary>
/// Persists window position/size/maximized-state to a small JSON file in the
/// app data folder (System.Text.Json, no extra package - Phase 2 §4) and
/// restores it on the next launch.
/// </summary>
public class WindowStateService
{
    private record SavedWindowState(double X, double Y, double Width, double Height, bool IsMaximized);

    private string FilePath => System.IO.Path.Combine(Data.FcmsPaths.GetAppDataDirectory(), "window-state.json");

    private double _normalX = double.NaN;
    private double _normalY = double.NaN;
    private double _normalWidth = 1280;
    private double _normalHeight = 720;

    public void Save(Window window)
    {
        try
        {
            var isMax = window.WindowState == WindowState.Maximized;
            
            if (!isMax)
            {
                _normalX = window.Position.X;
                _normalY = window.Position.Y;
                _normalWidth = window.Width;
                _normalHeight = window.Height;
            }

            var state = new SavedWindowState(
                _normalX, _normalY,
                _normalWidth, _normalHeight,
                isMax);
            System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
        }
        catch
        {
            // Non-critical - losing window position isn't worth surfacing an error to the user.
        }
    }

    public void Restore(Window window)
    {
        try
        {
            window.PositionChanged += (s, e) =>
            {
                if (window.WindowState == WindowState.Normal)
                {
                    _normalX = window.Position.X;
                    _normalY = window.Position.Y;
                }
            };

            window.PropertyChanged += (s, e) =>
            {
                if (window.WindowState == WindowState.Normal)
                {
                    if (e.Property == Window.WidthProperty || e.Property == Window.BoundsProperty)
                        _normalWidth = window.Width;
                    if (e.Property == Window.HeightProperty || e.Property == Window.BoundsProperty)
                        _normalHeight = window.Height;
                }
            };

            if (!System.IO.File.Exists(FilePath)) return;
            var state = JsonSerializer.Deserialize<SavedWindowState>(System.IO.File.ReadAllText(FilePath));
            if (state is null) return;

            _normalX = state.X;
            _normalY = state.Y;
            _normalWidth = state.Width;
            _normalHeight = state.Height;

            const int minCoordinate = -2000;
            const int maxCoordinate = 10000;
            var positionLooksSane = !double.IsNaN(_normalX) && !double.IsNaN(_normalY) &&
                _normalX > minCoordinate && _normalX < maxCoordinate &&
                _normalY > minCoordinate && _normalY < maxCoordinate;
            var sizeLooksSane =
                _normalWidth is >= 400 and < 10000 &&
                _normalHeight is >= 300 and < 10000;

            if (positionLooksSane && sizeLooksSane)
            {
                window.Position = new PixelPoint((int)_normalX, (int)_normalY);
                window.Width = _normalWidth;
                window.Height = _normalHeight;
            }

            if (state.IsMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }
        catch
        {
            // Fall back to Avalonia's default placement if the saved state is unreadable.
        }
    }
}
