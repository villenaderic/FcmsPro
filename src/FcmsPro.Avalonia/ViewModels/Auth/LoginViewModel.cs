using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Auth;

/// <summary>
/// Login screen for the single admin account. Lockout behavior (PWA had a
/// `fcms_lock` localStorage flag after repeated failures) is ported here as
/// an in-memory counter + timestamp rather than a persisted flag, since a
/// desktop app closing doesn't need to "remember" a lockout across restarts
/// the way a browser tab reload did in the PWA - flagged as a design choice
/// to confirm during this module's full implementation pass, not a blocker
/// for scaffolding.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly AuthService _authService;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLockedOut;

    [ObservableProperty]
    private bool _isBusy;

    private int _failedAttempts;
    private DateTimeOffset? _lockedUntil;

    public event Action? LoginSucceeded;

    public LoginViewModel(AuthService authService) => _authService = authService;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;

        if (_lockedUntil.HasValue)
        {
            if (DateTimeOffset.UtcNow < _lockedUntil.Value)
            {
                IsLockedOut = true;
                ErrorMessage = $"Too many failed attempts. Try again in {(_lockedUntil.Value - DateTimeOffset.UtcNow).Minutes + 1} minute(s).";
                return;
            }

            // Lockout window expired.
            _lockedUntil = null;
            _failedAttempts = 0;
            IsLockedOut = false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your password.";
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _authService.VerifyLoginAsync(Password);
            if (ok)
            {
                _failedAttempts = 0;
                LoginSucceeded?.Invoke();
                return;
            }

            _failedAttempts++;
            if (_failedAttempts >= MaxAttempts)
            {
                _lockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                IsLockedOut = true;
                ErrorMessage = $"Too many failed attempts. Try again in {LockoutDuration.Minutes} minutes.";
            }
            else
            {
                ErrorMessage = $"Incorrect password. {MaxAttempts - _failedAttempts} attempt(s) remaining.";
            }
        }
        finally
        {
            IsBusy = false;
            Password = string.Empty;
        }
    }
}
