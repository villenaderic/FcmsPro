using System;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Settings;

/// <summary>
/// Covers AppSettings (business/document config) + UiPreferences (appearance)
/// + admin password change, replacing the PWA's localStorage-backed settings
/// module (Phase 1 audit §1). ServiceTypes/PaymentMethods are edited here as
/// comma-separated text rather than a full add/remove list UI - a reasonable
/// simplification for this pass; the underlying AppSettings fields are the
/// real JSON-array-backed source of truth either way, so upgrading to a
/// proper chip-list editor later is a View-only change.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly AuthService _authService;
    private readonly ThemeService _themeService;
    private readonly NavigationService _navigation;

    [ObservableProperty] private string? _businessName;
    [ObservableProperty] private string? _freelancerName;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _contactNumber;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _website;
    [ObservableProperty] private string? _tin;
    [ObservableProperty] private string _currencySymbol = "\u20b1";
    [ObservableProperty] private string? _serviceTypesCsv;
    [ObservableProperty] private string? _receiptFooter;
    [ObservableProperty] private string? _invoiceTerms;
    [ObservableProperty] private string? _invoiceNotes;
    [ObservableProperty] private string? _quoteTerms;

    [ObservableProperty] private bool _autoInvoiceOnDelivery;

    /// <summary>decimal? to match NumericUpDown.Value's type, not int - see
    /// TemplateFormViewModel.DeadlineDays for the same established pattern
    /// and why binding this directly as an int was already flagged and
    /// fixed as a real bug elsewhere in this app.</summary>
    [ObservableProperty] private decimal? _invoiceDueDays = 14;

    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private string _accentColor = "#6366F1";

    [ObservableProperty] private string? _newPassword;
    [ObservableProperty] private string? _confirmPassword;
    [ObservableProperty] private string? _passwordErrorMessage;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public string[] ThemeOptions { get; } = { "System", "Light", "Dark" };

    public SettingsViewModel(IUnitOfWork uow, AuthService authService, ThemeService themeService, NavigationService navigation)
    {
        _uow = uow;
        _authService = authService;
        _themeService = themeService;
        _navigation = navigation;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void ViewLogs() => _navigation.NavigateTo(AppPage.Logs);

    [RelayCommand]
    private void ViewTrash() => _navigation.NavigateTo(AppPage.Trash);

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = null;
        try
        {
            var settings = await _uow.Settings.GetAppSettingsAsync();
            BusinessName = settings.BusinessName;
            FreelancerName = settings.FreelancerName;
            Address = settings.Address;
            ContactNumber = settings.ContactNumber;
            Email = settings.Email;
            Website = settings.Website;
            Tin = settings.Tin;
            CurrencySymbol = settings.CurrencySymbol;
            ReceiptFooter = settings.ReceiptFooter;
            InvoiceTerms = settings.InvoiceTerms;
            InvoiceNotes = settings.InvoiceNotes;
            QuoteTerms = settings.QuoteTerms;
            AutoInvoiceOnDelivery = settings.AutoInvoiceOnDelivery;
            InvoiceDueDays = settings.InvoiceDueDays;
            ServiceTypesCsv = TryDeserializeCsv(settings.ServiceTypesJson);

            var prefs = await _uow.Settings.GetUiPreferencesAsync();
            Theme = prefs.Theme;
            AccentColor = prefs.AccentColor;
        }
        catch (Exception ex)
        {
            // Reuses StatusMessage (this ViewModel's established pattern for
            // both success and failure) rather than adding a second
            // ErrorMessage property - constructor calls this fire-and-forget
            // (a ViewModel constructor can't be async), so without this
            // catch a failure here would previously have been an unobserved
            // task exception, with the whole page just silently showing
            // blank/default values and no indication anything went wrong.
            StatusMessage = $"Could not load settings: {ex.Message}";
        }
    }

    private static string? TryDeserializeCsv(string json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<string[]>(json);
            return items is null ? null : string.Join(", ", items);
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task SaveBusinessInfoAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _uow.Settings.GetAppSettingsAsync();
            settings.BusinessName = BusinessName;
            settings.FreelancerName = FreelancerName;
            settings.Address = Address;
            settings.ContactNumber = ContactNumber;
            settings.Email = Email;
            settings.Website = Website;
            settings.Tin = Tin;
            settings.CurrencySymbol = CurrencySymbol;
            settings.ReceiptFooter = ReceiptFooter;
            settings.InvoiceTerms = InvoiceTerms;
            settings.InvoiceNotes = InvoiceNotes;
            settings.QuoteTerms = QuoteTerms;
            settings.AutoInvoiceOnDelivery = AutoInvoiceOnDelivery;
            settings.InvoiceDueDays = (int)Math.Max(0, InvoiceDueDays ?? 14);

            var serviceTypes = (ServiceTypesCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            settings.ServiceTypesJson = JsonSerializer.Serialize(serviceTypes);

            await _uow.Settings.SaveAppSettingsAsync(settings);
            await _uow.SaveChangesAsync();
            AppCulture.Apply(settings.CurrencySymbol);
            StatusMessage = "Business settings saved. Currency symbol changes apply to already-open pages the next time you navigate to them.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAppearanceAsync()
    {
        var prefs = await _uow.Settings.GetUiPreferencesAsync();
        prefs.Theme = Theme;
        prefs.AccentColor = AccentColor;
        await _uow.Settings.SaveUiPreferencesAsync(prefs);
        await _uow.SaveChangesAsync();

        _themeService.Apply(Theme, AccentColor);
        StatusMessage = "Appearance updated.";
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        PasswordErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
        {
            PasswordErrorMessage = "Passwords must match and not be empty.";
            return;
        }

        IsBusy = true;
        try
        {
            await _authService.ChangePasswordAsync(NewPassword);
            NewPassword = null;
            ConfirmPassword = null;
            StatusMessage = "Password updated.";
        }
        catch (Exception ex)
        {
            PasswordErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
