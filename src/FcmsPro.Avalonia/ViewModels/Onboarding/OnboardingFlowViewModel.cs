using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Avalonia.ViewModels.Onboarding;

public static class OnboardingConstants
{
    /// <summary>
    /// Bump this to force re-prompting the terms acceptance screen on next
    /// launch (TermsAcceptance rows are versioned - Phase 2 §2).
    /// </summary>
    public const string CurrentTermsVersion = "1.0";
}

public enum OnboardingStep { Welcome, Terms, DataLocation, AdminSetup, Ready }

/// <summary>
/// Wizard shell for the first-run flow: Welcome -> Terms -> Data Location ->
/// Admin Setup -> Ready. Each step is a lightweight child ViewModel; this class
/// just tracks position and persists the terminal state (terms acceptance,
/// admin account) via IUnitOfWork/AuthService.
/// </summary>
public partial class OnboardingFlowViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly Core.Services.AuthService _authService;

    [ObservableProperty]
    private OnboardingStep _currentStep = OnboardingStep.Welcome;

    // Computed per-step visibility flags. Avalonia's ContentControl.DataTemplates
    // matches by the CLR TYPE of the bound value, not by value equality - binding
    // directly to the CurrentStep enum and expecting different templates per
    // enum member does NOT work, since every step is the same OnboardingStep
    // type. These bool properties (kept in sync via OnCurrentStepChanged below)
    // are what the view actually switches on instead.
    public bool IsWelcomeStep => CurrentStep == OnboardingStep.Welcome;
    public bool IsTermsStep => CurrentStep == OnboardingStep.Terms;
    public bool IsDataLocationStep => CurrentStep == OnboardingStep.DataLocation;
    public bool IsAdminSetupStep => CurrentStep == OnboardingStep.AdminSetup;
    public bool IsReadyStep => CurrentStep == OnboardingStep.Ready;

    partial void OnCurrentStepChanged(OnboardingStep value)
    {
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsTermsStep));
        OnPropertyChanged(nameof(IsDataLocationStep));
        OnPropertyChanged(nameof(IsAdminSetupStep));
        OnPropertyChanged(nameof(IsReadyStep));
    }

    [ObservableProperty]
    private bool _termsAccepted;

    [ObservableProperty]
    private string? _selectedDataDirectory;

    [ObservableProperty]
    private string _adminUsername = string.Empty;

    [ObservableProperty]
    private string _adminPassword = string.Empty;

    [ObservableProperty]
    private string _adminPasswordConfirm = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public OnboardingFlowViewModel(IUnitOfWork uow, Core.Services.AuthService authService)
    {
        _uow = uow;
        _authService = authService;
        SelectedDataDirectory = Data.FcmsPaths.GetAppDataDirectory();
    }

    [RelayCommand]
    private void GoToTerms() => CurrentStep = OnboardingStep.Terms;

    [RelayCommand]
    private async Task AcceptTermsAsync()
    {
        if (!TermsAccepted)
        {
            ErrorMessage = "Please check the box to confirm you've read and accept the terms.";
            return;
        }

        await _uow.Terms.AddAsync(new TermsAcceptance
        {
            TermsVersion = OnboardingConstants.CurrentTermsVersion,
            Accepted = true,
            AcceptedAt = DateTimeOffset.UtcNow
        });
        await _uow.SaveChangesAsync();
        CurrentStep = OnboardingStep.DataLocation;
    }

    [RelayCommand]
    private void ConfirmDataLocation() => CurrentStep = OnboardingStep.AdminSetup;

    [RelayCommand]
    private async Task CreateAdminAccountAsync()
    {
        ErrorMessage = null;

        if (AdminPassword != AdminPasswordConfirm)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        try
        {
            await _authService.SetupAdminAccountAsync(AdminUsername, AdminPassword);
            CurrentStep = OnboardingStep.Ready;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        CurrentStep = CurrentStep switch
        {
            OnboardingStep.Terms => OnboardingStep.Welcome,
            OnboardingStep.DataLocation => OnboardingStep.Terms,
            OnboardingStep.AdminSetup => OnboardingStep.DataLocation,
            _ => CurrentStep
        };
    }

    /// <summary>Raised by the view to close the onboarding window and open the main shell after "Ready" is dismissed.</summary>
    public event Action? OnboardingCompleted;

    [RelayCommand]
    private void Finish() => OnboardingCompleted?.Invoke();
}
