using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Threading;

namespace FcmsPro.Avalonia.Services;

/// <summary>
/// Ports the PWA's two-key shortcut sequences: "g" + letter navigates,
/// "n" + letter opens a create form, with a 900ms timeout window between the
/// two keys (Phase 1 audit §3). Single-key shortcuts (/, ?, [, Shift+T, r) are
/// handled the same way, alongside the sequence buffer.
///
/// macOS-reserved-binding check (flagged in Phase 1 audit §3 as needing
/// verification once running on macOS): none of these obviously collide with
/// standard macOS chrome (Cmd+T/W/Q etc. all require Cmd, which none of these
/// shortcuts use), but this should still be manually verified on a real macOS
/// build before shipping, per the migration prompt's cross-platform QA pass.
/// </summary>
public class KeySequenceService
{
    private const int SequenceTimeoutMs = 900;

    private static readonly Dictionary<Key, AppPage> GoTargets = new()
    {
        [Key.D] = AppPage.Dashboard,
        [Key.A] = AppPage.Analytics,
        [Key.C] = AppPage.Clients,
        [Key.W] = AppPage.Commissions,
        [Key.P] = AppPage.Payments,
        [Key.R] = AppPage.Receipts,
        [Key.E] = AppPage.Expenses,
        [Key.I] = AppPage.Invoices,
        [Key.S] = AppPage.Settings,
        [Key.L] = AppPage.Logs,
        [Key.B] = AppPage.Backup,
        [Key.T] = AppPage.Templates,
        [Key.G] = AppPage.Goals,
        [Key.Q] = AppPage.Quotes,
    };

    /// <summary>Maps the second key of an "n" sequence to a create-form request, raised via CreateRequested.</summary>
    private static readonly Dictionary<Key, AppPage> NewTargets = new()
    {
        [Key.W] = AppPage.Commissions, // nw = new commission
        [Key.C] = AppPage.Clients,     // nc = new client
        [Key.P] = AppPage.Payments,    // np = new payment
        [Key.E] = AppPage.Expenses,    // ne = new expense
        [Key.I] = AppPage.Invoices,    // ni = new invoice
        [Key.Q] = AppPage.Quotes,      // nq = new quote
    };

    private readonly NavigationService _navigation;
    private Key? _pendingPrefix;
    private DispatcherTimer? _timeoutTimer;

    public event Action? FocusSearchRequested;
    public event Action? ToggleShortcutsOverlayRequested;
    public event Action? ToggleSidebarRequested;
    public event Action? ToggleThemeRequested;
    public event Action? RefreshCurrentPageRequested;
    public event Action<AppPage>? CreateRequested;

    public KeySequenceService(NavigationService navigation) => _navigation = navigation;

    /// <summary>
    /// Call from the main window's PreviewKeyDown. Returns true if the key was
    /// consumed as a shortcut. Caller is responsible for the "suppress while
    /// focus is inside an input/textarea/select"-equivalent check (i.e. don't
    /// call this while a TextBox/ComboBox/etc. has focus) - matches PWA behavior.
    /// </summary>
    public bool HandleKeyDown(Key key, KeyModifiers modifiers)
    {
        // Two-key sequence in progress?
        if (_pendingPrefix is { } prefix)
        {
            CancelPendingSequence();

            if (prefix == Key.G && GoTargets.TryGetValue(key, out var page))
            {
                _navigation.NavigateTo(page);
                return true;
            }
            if (prefix == Key.N && NewTargets.TryGetValue(key, out var createPage))
            {
                CreateRequested?.Invoke(createPage);
                return true;
            }
            // Unrecognized second key - fall through, sequence just resets.
            return false;
        }

        // Start of a possible two-key sequence.
        if (key == Key.G || key == Key.N)
        {
            _pendingPrefix = key;
            _timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SequenceTimeoutMs) };
            _timeoutTimer.Tick += (_, _) => CancelPendingSequence();
            _timeoutTimer.Start();
            return true;
        }

        // Single-key shortcuts.
        switch (key)
        {
            case Key.Oem2 or Key.Divide when modifiers == KeyModifiers.None: // "/"
                FocusSearchRequested?.Invoke();
                return true;
            case Key.OemQuestion or Key.Oem2 when modifiers == KeyModifiers.Shift: // "?"
                ToggleShortcutsOverlayRequested?.Invoke();
                return true;
            case Key.OemOpenBrackets:
                ToggleSidebarRequested?.Invoke();
                return true;
            case Key.T when modifiers == KeyModifiers.Shift:
                ToggleThemeRequested?.Invoke();
                return true;
            case Key.R when modifiers == KeyModifiers.None:
                RefreshCurrentPageRequested?.Invoke();
                return true;
            default:
                return false;
        }
    }

    private void CancelPendingSequence()
    {
        _pendingPrefix = null;
        _timeoutTimer?.Stop();
        _timeoutTimer = null;
    }
}
