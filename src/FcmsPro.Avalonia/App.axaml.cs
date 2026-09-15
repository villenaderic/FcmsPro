using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Core.Metrics;
using FcmsPro.Core.Backup;
using FcmsPro.Data;
using FcmsPro.Pdf;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;
using FcmsPro.Avalonia.ViewModels.Onboarding;
using FcmsPro.Avalonia.ViewModels.Auth;
using FcmsPro.Avalonia.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FcmsPro.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private readonly CancellationTokenSource _lifetimeCts = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        try
        {
            await InitializeAndShowStartupWindowAsync();
        }
        catch (Exception ex)
        {
            // This is the critical catch-all for this whole method: since it's
            // `async void`, an uncaught exception here does NOT propagate back
            // through a normal try/catch in Main() - it can silently exit the
            // process with no console output at all. Logging explicitly here,
            // to both Serilog and a guaranteed-plain-text crash file, then
            // rethrowing so Program.cs's AppDomain.UnhandledException handler
            // also gets a chance to record it.
            try { Log.Fatal(ex, "Unhandled exception during app startup"); } catch { /* Serilog may not be configured yet */ }
            try
            {
                var crashLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fcmspro-crash.log");
                System.IO.File.AppendAllText(crashLogPath, $"[{DateTime.Now:O}] App.OnFrameworkInitializationCompleted:\n{ex}\n\n");
            }
            catch { /* nothing further we can do if even this fails */ }

            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeAndShowStartupWindowAsync()
    {
        ConfigureDiagnosticLogging();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        SingleInstanceService.StartListening(_lifetimeCts.Token);

        var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

        // Because the caller (OnFrameworkInitializationCompleted) is `async
        // void`, Avalonia's own startup sequence does NOT wait for it to
        // finish - it returns control to the framework at our first `await`
        // below. With the default ShutdownMode.OnMainWindowClose and no
        // MainWindow assigned yet, the framework can (and did, in testing)
        // conclude there's nothing keeping it alive and shut down immediately
        // - no window, no error, the process just exits. Switching to
        // OnExplicitShutdown here prevents that until we've actually
        // assigned a MainWindow below.
        if (desktop is not null)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // First-run DB init: apply migrations, WAL mode, seed defaults.
        // Any failure here is a hard-stop condition (can't run without a DB),
        // so it's logged and surfaced before any window opens.
        try
        {
            // CreateAsyncScope + await using rather than CreateScope + using:
            // IUnitOfWork's implementation (UnitOfWork) only implements
            // IAsyncDisposable, not IDisposable - synchronously disposing a
            // scope that resolved it throws InvalidOperationException
            // ("type only implements IAsyncDisposable"). This particular
            // scope only resolves FcmsDbContext directly, not IUnitOfWork,
            // so it likely wouldn't have hit the bug here - fixed anyway for
            // consistency and to close off the risk if that ever changes.
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FcmsDbContext>();
            await FcmsPro.Data.Seed.DatabaseInitializer.InitializeAsync(db);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database initialization failed");
            // TODO (module phase): show a native error dialog here instead of
            // silently continuing - Avalonia dialog wiring lands with the
            // first-run flow implementation.
        }

        if (desktop is not null)
        {
            desktop.MainWindow = await ResolveStartupWindowAsync();
            desktop.MainWindow.Show();

            // Hand shutdown behavior back to the normal
            // close-the-main-window policy now that a window actually exists.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            SingleInstanceService.ActivationRequested += () =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (desktop.MainWindow is { } w)
                    {
                        w.WindowState = global::Avalonia.Controls.WindowState.Normal;
                        w.Activate();
                    }
                });
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                _lifetimeCts.Cancel();
                Log.CloseAndFlush();
            };
        }
    }

    /// <summary>
    /// Routes to the correct first window based on app state:
    /// Terms not accepted -> Onboarding flow (Welcome -> Terms -> Data Location -> Admin Setup -> Ready)
    /// Terms accepted but no admin account -> Admin Setup (shouldn't normally happen, defensive)
    /// Admin account exists -> Login
    /// (Login success hands off to the MainWindow shell.)
    /// </summary>
    private async Task<global::Avalonia.Controls.Window> ResolveStartupWindowAsync()
    {
        // CreateAsyncScope + await using: this scope resolves IUnitOfWork
        // (via AuthService and directly), and UnitOfWork only implements
        // IAsyncDisposable - synchronously disposing this scope threw
        // "type only implements IAsyncDisposable. Use DisposeAsync to
        // dispose the container." on every single startup. This was the
        // actual cause of the app silently exiting with no window.
        await using var scope = Services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Re-apply persisted appearance (theme/accent) and currency-symbol
        // culture on every launch. Previously these were only ever applied
        // in-memory when the user hit Save on the Settings page - saved to
        // the DB correctly, but silently reverting to FluentTheme defaults
        // (light-variant look, "\u00a4" generic currency sign instead of
        // the user's chosen symbol) every time the app was relaunched, even
        // though Settings itself would still show the "saved" values.
        try
        {
            var settings = await uow.Settings.GetAppSettingsAsync();
            AppCulture.Apply(settings.CurrencySymbol);

            var prefs = await uow.Settings.GetUiPreferencesAsync();
            scope.ServiceProvider.GetRequiredService<ThemeService>().Apply(prefs.Theme, prefs.AccentColor);
        }
        catch (Exception ex)
        {
            // Non-fatal: worst case the app opens with default theme/currency
            // formatting for this one session rather than failing to start.
            Log.Warning(ex, "Failed to apply persisted appearance/currency settings at startup");
        }

        // Permanently remove anything that's been sitting in the trash
        // longer than 30 days (TrashService.PurgeExpiredAsync already
        // swallows its own errors internally - never worth blocking
        // startup over a failed purge).
        await scope.ServiceProvider.GetRequiredService<TrashService>().PurgeExpiredAsync();

        // Spawn any recurring expenses (software subscriptions, retainers,
        // etc.) whose next scheduled occurrence has arrived since the app
        // was last opened. Same "swallow errors, never block startup" policy
        // as the trash purge above - see ExpenseService.ProcessDueRecurrencesAsync.
        await scope.ServiceProvider.GetRequiredService<ExpenseService>().ProcessDueRecurrencesAsync();

        var terms = await uow.Terms.GetLatestAsync();
        var hasAcceptedCurrentTerms = terms is { Accepted: true } && terms.TermsVersion == OnboardingConstants.CurrentTermsVersion;

        if (!hasAcceptedCurrentTerms)
        {
            var onboardingVm = Services.GetRequiredService<OnboardingFlowViewModel>();
            return new Views.Onboarding.OnboardingWindow { DataContext = onboardingVm };
        }

        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        if (!await authService.IsAdminAccountSetUpAsync())
        {
            // Defensive path: terms were accepted in a prior run but no admin
            // account exists (e.g. app was closed mid-onboarding). Reuse the
            // same wizard, jumped forward to the Admin Setup step.
            var resumeVm = Services.GetRequiredService<OnboardingFlowViewModel>();
            resumeVm.CurrentStep = OnboardingStep.AdminSetup;
            return new Views.Onboarding.OnboardingWindow { DataContext = resumeVm };
        }

        var loginVm = Services.GetRequiredService<LoginViewModel>();
        return new Views.Auth.LoginWindow { DataContext = loginVm };
    }

    private static void ConfigureDiagnosticLogging()
    {
        // Developer-facing rotating diagnostic log - distinct from the
        // business-facing AuditLog table (Phase 1 audit §2.10). Rolls daily,
        // keeps 14 days, written to the OS-appropriate log directory.
        var logPath = System.IO.Path.Combine(FcmsPaths.GetLogsDirectory(), "fcms-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        Log.Information("FcmsPro starting up");
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Data layer
        services.AddDbContext<FcmsDbContext>(opt =>
            opt.UseSqlite($"Data Source={FcmsPaths.GetDatabasePath()};Cache=Shared"));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Core services
        services.AddScoped<ClientService>();
        services.AddScoped<CommissionService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<GlobalSearchService>();
        services.AddScoped<TemplateService>();
        services.AddScoped<AuthService>();
        services.AddScoped<MetricsService>();
        services.AddScoped<TaxSummaryService>();
        services.AddScoped<TrashService>();
        services.AddScoped<BackupService>();

        // PDF
        services.AddScoped<IReceiptRenderer, ReceiptRenderer>();
        services.AddScoped<IInvoiceRenderer, InvoiceRenderer>();
        services.AddScoped<IQuoteRenderer, QuoteRenderer>();
        services.AddScoped<ITaxSummaryRenderer, TaxSummaryRenderer>();

        // UI-only services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<WindowStateService>();
        services.AddSingleton<KeySequenceService>();

        // ViewModels (Transient - fresh state each navigation)
        services.AddTransient<OnboardingFlowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainWindowViewModel>();

        // Clients module (Phase 4)
        services.AddTransient<ViewModels.Clients.ClientsListViewModel>();
        services.AddTransient<ViewModels.Clients.ClientProfileViewModel>();
        // ClientFormViewModel is constructed directly (not resolved via DI)
        // by ClientsListViewModel, since it needs a runtime `existing: Client?`
        // constructor argument that doesn't fit the container's parameterless
        // resolution model - it still gets a properly-scoped ClientService
        // passed in from the caller, which itself came from DI.

        // Commissions module (Phase 4)
        services.AddTransient<ViewModels.Commissions.CommissionsListViewModel>();
        services.AddTransient<ViewModels.Commissions.CommissionProfileViewModel>();
        // CommissionFormViewModel is constructed directly by
        // CommissionsListViewModel for the same reason as ClientFormViewModel above.

        // Payments module (Phase 4)
        services.AddTransient<ViewModels.Payments.PaymentsListViewModel>();
        // PaymentFormViewModel is constructed directly by PaymentsListViewModel,
        // same pattern as the other module forms above.

        // Receipts module (Phase 4) - read-only, no form ViewModel needed.
        services.AddTransient<ViewModels.Receipts.ReceiptsListViewModel>();

        // Invoices module (Phase 4)
        services.AddTransient<ViewModels.Invoices.InvoicesListViewModel>();
        // InvoiceFormViewModel is constructed directly by InvoicesListViewModel,
        // same pattern as the other module forms above.

        // Quotes module (Phase 4)
        services.AddTransient<ViewModels.Quotes.QuotesListViewModel>();
        // QuoteFormViewModel is constructed directly by QuotesListViewModel,
        // same pattern as the other module forms above.

        // Expenses module (Phase 4)
        services.AddTransient<ViewModels.Expenses.ExpensesListViewModel>();
        // ExpenseFormViewModel is constructed directly by ExpensesListViewModel.

        // Goals module (Phase 4) - single-row settings-style page, no list.
        services.AddTransient<ViewModels.Goals.GoalsViewModel>();

        // Dashboard / Analytics (Phase 4)
        services.AddTransient<ViewModels.Dashboard.DashboardViewModel>();
        services.AddTransient<ViewModels.Analytics.AnalyticsViewModel>();

        // Settings (Phase 4)
        services.AddTransient<ViewModels.Settings.SettingsViewModel>();

        // Templates (Phase 4)
        services.AddTransient<ViewModels.Templates.TemplatesViewModel>();
        // TemplateFormViewModel is constructed directly by TemplatesViewModel.

        // Logs (Phase 4) - read-only.
        services.AddTransient<ViewModels.Logs.LogsViewModel>();
        services.AddTransient<ViewModels.Trash.TrashViewModel>();

        // Backup (Phase 4)
        services.AddTransient<ViewModels.Backup.BackupViewModel>();
    }
}
