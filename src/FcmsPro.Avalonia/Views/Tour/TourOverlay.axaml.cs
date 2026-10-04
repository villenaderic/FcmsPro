using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using FcmsPro.Avalonia.ViewModels.Tour;

namespace FcmsPro.Avalonia.Views.Tour;

/// <summary>
/// Hosts the guided tour above MainWindow. Listens to AppTourViewModel,
/// resolves each step's target control (via <see cref="TargetResolver"/>),
/// aims the spotlight at it and glides the step card next to it. Falls back
/// to a centered card for steps with no target, or whose target isn't
/// currently visible (e.g. sidebar collapsed in a narrow window).
/// </summary>
public partial class TourOverlay : UserControl
{
    private const double CardGap = 28;
    private const double EdgeMargin = 16;

    private AppTourViewModel? _vm;
    private Transitions? _cardTransitions;

    /// <summary>Maps a TourStep.TargetName to the live control in MainWindow.</summary>
    public Func<string, Control?>? TargetResolver { get; set; }

    public TourOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        SizeChanged += (_, _) =>
        {
            if (_vm?.IsActive == true) Refresh(animate: false);
        };
    }

    private void AttachViewModel()
    {
        if (_vm is not null)
        {
            _vm.StepChanged -= OnStepChanged;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as AppTourViewModel;
        if (_vm is null) return;

        _vm.StepChanged += OnStepChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
        ApplyActiveState();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppTourViewModel.IsActive))
            ApplyActiveState();
    }

    private void ApplyActiveState()
    {
        if (_vm is null) return;

        if (_vm.IsActive)
        {
            IsVisible = true;
            Spotlight.Begin();
            // Lay out first, then place the card without animating from (0,0).
            Dispatcher.UIThread.Post(() => Refresh(animate: false), DispatcherPriority.Loaded);
        }
        else
        {
            Card.Opacity = 0;
            Spotlight.End();
            IsVisible = false;
        }
    }

    private void OnStepChanged()
    {
        // Fade the card out, then re-aim and fade back in for a clean hand-off.
        Card.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (_vm?.IsActive == true) Refresh(animate: true);
        }, TimeSpan.FromMilliseconds(140));
    }

    private Rect? ResolveTargetRect()
    {
        var name = _vm?.Current.TargetName;
        if (name is null || TargetResolver?.Invoke(name) is not { } target) return null;
        if (!target.IsEffectivelyVisible || target.Bounds.Width <= 0 || target.Bounds.Height <= 0) return null;

        var origin = target.TranslatePoint(new Point(0, 0), this);
        return origin is { } p ? new Rect(p, target.Bounds.Size) : null;
    }

    private void Refresh(bool animate)
    {
        if (_vm is null || !IsVisible) return;

        var targetRect = ResolveTargetRect();
        Spotlight.Aim(targetRect);

        // When not animating (first show / resize), temporarily detach the
        // Margin transition so the card jumps straight to its slot.
        _cardTransitions ??= Card.Transitions;
        if (!animate) Card.Transitions = null;

        Card.Margin = ComputeCardMargin(targetRect);
        Card.Opacity = 1;

        if (!animate)
            Dispatcher.UIThread.Post(() => Card.Transitions = _cardTransitions, DispatcherPriority.Background);
    }

    private Thickness ComputeCardMargin(Rect? target)
    {
        var bounds = Bounds;
        Card.Measure(new Size(Card.Width, bounds.Height));
        var size = Card.DesiredSize;

        double x, y;
        if (target is { } t)
        {
            // Prefer to the right of the target, vertically centered on it.
            x = t.Right + CardGap + 20;
            y = t.Center.Y - size.Height / 2;

            if (x + size.Width > bounds.Width - EdgeMargin)
            {
                // No room on the right: put it below, or above if that fails.
                x = t.Center.X - size.Width / 2;
                y = t.Bottom + CardGap;
                if (y + size.Height > bounds.Height - EdgeMargin)
                    y = t.Top - CardGap - size.Height;
            }
        }
        else
        {
            x = (bounds.Width - size.Width) / 2;
            y = (bounds.Height - size.Height) / 2;
        }

        x = Math.Clamp(x, EdgeMargin, Math.Max(EdgeMargin, bounds.Width - size.Width - EdgeMargin));
        y = Math.Clamp(y, EdgeMargin, Math.Max(EdgeMargin, bounds.Height - size.Height - EdgeMargin));
        return new Thickness(x, y, 0, 0);
    }
}
