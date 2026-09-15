using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;
using FcmsPro.Core.Backup;
using FcmsPro.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FcmsPro.Avalonia.Views.Shell;

public partial class MainWindow : Window
{
    private readonly WindowStateService _windowStateService = App.Services.GetRequiredService<WindowStateService>();

    public MainWindow()
    {
        InitializeComponent();

        _windowStateService.Restore(this);
        Closing += OnClosing;

        // Auto-collapse the sidebar once the window gets too narrow for it
        // plus a usable content area (the actual "split screen" ask - half
        // of a 1920px display is 960px, half of a 1366px laptop is 683px;
        // the fixed 212px sidebar alone eats a third of that). Re-expands
        // once there's room again. A manual toggle (Ctrl+B, or the new
        // hamburger button) still works at any width - this only changes
        // the automatic default, never fights a width-driven change with
        // itself since it only flips state on an actual threshold crossing.
        PropertyChanged += OnWindowPropertyChanged;

        // Belt-and-suspenders against the title bar (and its
        // minimize/maximize/close buttons) landing off-screen - reported
        // live on a fresh install with no window-state.json yet, where the
        // window falls back to WindowStartupLocation="CenterScreen" with
        // the XAML-default size. Avalonia's Screens API isn't reliably
        // queryable in the constructor (before the native window exists),
        // so this runs on Opened instead, once the window actually has a
        // screen to check against.
        Opened += (_, _) => ClampToWorkingArea();

        // Dialogs (client add/edit form, confirm-delete, etc.) are centered
        // over this window and modal to it - DialogService needs a live
        // owner reference to do that.
        App.Services.GetRequiredService<DialogService>().OwnerWindow = this;

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // Native menu bar (macOS menu bar / Windows-Linux equivalents) is
        // populated once real menu actions (New Client, Export Backup, etc.)
        // exist in their module phases - NativeMenu.SetMenu(this, ...) is the
        // hook point, left as a TODO rather than an empty stub menu for now.
    }

    private const double SidebarAutoCollapseWidth = 820; // 212px sidebar + a workable ~600px content area

    private void OnWindowPropertyChanged(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (Bounds.Width <= 0) return; // not laid out yet - avoid a spurious collapse/expand flicker on startup

        var shouldCollapse = Bounds.Width < SidebarAutoCollapseWidth;
        if (vm.IsSidebarCollapsed != shouldCollapse)
            vm.IsSidebarCollapsed = shouldCollapse;
    }

    private bool _autoBackupAttempted;

    /// <summary>
    /// Saves window state immediately (cheap, synchronous), then - on the
    /// first Closing event only - pauses the actual close just long enough
    /// to attempt a silent rotating auto-backup (UiPreferences.
    /// AutoBackupOnCloseEnabled), before letting the window close for real.
    /// Uses a proper DI scope (CreateAsyncScope, same pattern as App.axaml.cs's
    /// ResolveStartupWindowAsync) rather than resolving the Scoped
    /// IUnitOfWork/BackupService straight from the root App.Services, since
    /// resolving a Scoped service from the root provider leaves it
    /// essentially un-disposed until process exit - harmless for a genuine
    /// one-shot shutdown task, but not a pattern worth introducing when the
    /// proper-scope version is just as easy and already proven elsewhere.
    /// </summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _windowStateService.Save(this);

        if (_autoBackupAttempted)
            return; // second time through (after the Close() call below) - let it actually close now

        e.Cancel = true;
        _autoBackupAttempted = true;

        try
        {
            await using var scope = App.Services.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var prefs = await uow.Settings.GetUiPreferencesAsync();

            if (prefs.AutoBackupOnCloseEnabled)
            {
                var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
                await backupService.WriteRotatingBackupAsync(
                    Data.FcmsPaths.GetBackupsDirectory(),
                    prefs.AutoBackupKeepCount);
            }
        }
        catch
        {
            // Never block app close over a failed backup attempt - and
            // BackupService.WriteRotatingBackupAsync already swallows its
            // own errors, so this catch only guards the Settings read above.
        }

        Close();
    }

    private void ClampToWorkingArea()
    {
        if (WindowState == WindowState.Maximized) return; // fills the screen by definition, nothing to clamp

        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        // WorkingArea is in device pixels; Position/Width/Height are DIPs -
        // convert through the screen's scaling factor rather than comparing
        // the two unit systems directly.
        var scaling = screen.Scaling <= 0 ? 1.0 : screen.Scaling;
        var workingArea = screen.WorkingArea;
        var waLeft = workingArea.X / scaling;
        var waTop = workingArea.Y / scaling;
        var waWidth = workingArea.Width / scaling;
        var waHeight = workingArea.Height / scaling;

        // Shrink first if the window is simply too big for this screen.
        if (Width > waWidth) Width = Math.Max(MinWidth, waWidth);
        if (Height > waHeight) Height = Math.Max(MinHeight, waHeight);

        // Then make sure the (possibly just-shrunk) window's top-left is
        // actually within the working area - this is the part that
        // directly guarantees the title bar itself is reachable.
        var x = Position.X / scaling;
        var y = Position.Y / scaling;
        var clampedX = Math.Clamp(x, waLeft, waLeft + waWidth - Width);
        var clampedY = Math.Clamp(y, waTop, waTop + waHeight - Height);

        if (Math.Abs(clampedX - x) > 0.5 || Math.Abs(clampedY - y) > 0.5)
            Position = new PixelPoint((int)(clampedX * scaling), (int)(clampedY * scaling));
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Suppress all shortcuts while focus is inside a text-entry control,
        // matching the PWA's input/textarea/select guard (Phase 1 audit §3).
        if (FocusManager?.GetFocusedElement() is TextBox or ComboBox or NumericUpDown or AutoCompleteBox)
            return;

        if (vm.KeySequence.HandleKeyDown(e.Key, e.KeyModifiers))
            e.Handled = true;
    }
}
