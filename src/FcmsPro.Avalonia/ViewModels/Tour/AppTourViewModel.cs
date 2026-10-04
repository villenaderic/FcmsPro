using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FcmsPro.Avalonia.ViewModels.Tour;

/// <param name="TargetName">x:Name of the control in MainWindow to spotlight, or null for a centered "no target" step.</param>
public record TourStep(string? TargetName, string Title, string Body);

/// <summary>
/// Drives the step-by-step guided tour shown over MainWindow after first-run
/// onboarding (and on demand from Settings / F1). Pure state - the animated
/// spotlight and card positioning live in Views/Tour/TourOverlay.
/// </summary>
public partial class AppTourViewModel : ObservableObject
{
    public IReadOnlyList<TourStep> Steps { get; } = new TourStep[]
    {
        new(null, "Welcome to FCMS Pro 👋",
            "Let's take a 1-minute tour of where everything lives. You can skip any time, or replay this later from Settings."),
        new("NavSearch", "Search everything",
            "Find any client, commission or expense instantly. Press / from anywhere to open it."),
        new("NavDashboard", "Dashboard",
            "Your at-a-glance home: income, pending balance, overdue and due-soon work, plus a 6-month income chart."),
        new("NavClients", "1. Start with a client",
            "Add the people you work for here. Each client gets a profile with their commissions, totals and attached files."),
        new("NavCommissions", "2. Track your commissions",
            "Create a commission for a client with price, deadline and status. Mark it Delivered and recurring jobs re-create themselves."),
        new("NavPayments", "3. Record income",
            "Log payments as they arrive. Balances update automatically, and refunds are recoverable from the Trash."),
        new("NavExpenses", "Expenses",
            "Keep tabs on software, supplies and subscriptions. Recurring expenses are added for you each period."),
        new("NavAnalytics", "Analytics & tax summary",
            "See revenue by month, top clients and spending by category, and export a yearly tax summary PDF."),
        new("NavGoals", "Goals",
            "Set monthly and yearly income targets and expense budgets, and watch your progress."),
        new("NavTheme", "Make it yours",
            "Flip between light and dark any time. More appearance options live in Settings."),
        new("NavBackup", "4. Back up your work",
            "Everything is stored only on this computer. Export a backup regularly and keep a copy somewhere safe."),
        new("NavSettings", "Settings",
            "Add your business details, currency and accent color. You can replay this tour from here whenever you like."),
        new(null, "You're all set 🎉",
            "Quick keys: press  g  then  d / c / w / p  to jump to a page, and  n  then  c / w / p / e  to create something new. Ctrl+B toggles the sidebar."),
    };

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private int _index;

    public TourStep Current => Steps[Math.Clamp(Index, 0, Steps.Count - 1)];
    public string Title => Current.Title;
    public string Body => Current.Body;
    public string StepLabel => $"STEP {Index + 1} OF {Steps.Count}";
    public double ProgressPercent => (Index + 1) * 100.0 / Steps.Count;
    public bool IsFirst => Index == 0;
    public bool IsLast => Index == Steps.Count - 1;
    public string NextLabel => IsLast ? "Finish" : (IsFirst ? "Let's go" : "Next");

    /// <summary>Raised whenever the visible step changes (including on Start) so the overlay can re-aim.</summary>
    public event Action? StepChanged;

    /// <summary>Raised when the tour ends, whether finished or skipped.</summary>
    public event Action? Closed;

    partial void OnIndexChanged(int value)
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(IsFirst));
        OnPropertyChanged(nameof(IsLast));
        OnPropertyChanged(nameof(NextLabel));
        if (IsActive) StepChanged?.Invoke();
    }

    public void Start()
    {
        Index = 0;
        // Index may already be 0 (replay) in which case OnIndexChanged won't fire.
        OnIndexChanged(0);
        IsActive = true;
        StepChanged?.Invoke();
    }

    [RelayCommand]
    private void Next()
    {
        if (!IsActive) return;
        if (IsLast) Finish();
        else Index++;
    }

    [RelayCommand]
    private void Back()
    {
        if (IsActive && !IsFirst) Index--;
    }

    [RelayCommand]
    private void Skip() => Finish();

    private void Finish()
    {
        IsActive = false;
        Closed?.Invoke();
    }
}
