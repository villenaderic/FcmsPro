using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FcmsPro.Avalonia.Views.Tour;

/// <summary>
/// Full-window dimmer with an animated "spotlight" cut-out around the current
/// tour target: the hole glides to each new target, two expanding pulse rings
/// radiate from it, and a bouncing arrow points at it. Purely visual and not
/// hit-testable (TourOverlay's blocker layer swallows clicks).
/// </summary>
public class TourSpotlight : Control
{
    private static readonly Color FallbackAccent = Color.Parse("#6366F1");

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private Rect? _target;      // where the hole is heading (null = no hole)
    private Rect _current;      // where the hole currently is (animated)
    private bool _hasCurrent;

    public TourSpotlight()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Step();
    }

    /// <summary>Aims the spotlight at <paramref name="rect"/> (in this control's coordinates), or removes the hole when null.</summary>
    public void Aim(Rect? rect, bool snap = false)
    {
        _target = rect;
        if (rect is { } r && (!_hasCurrent || snap))
        {
            // First target (or forced): start from a collapsed rect at the
            // target's center so the hole "opens" instead of teleporting in.
            _current = snap ? r : new Rect(r.Center, new Size(0, 0));
            _hasCurrent = true;
        }
        else if (rect is null)
        {
            _hasCurrent = false;
        }
        InvalidateVisual();
    }

    public void Begin()
    {
        _clock.Restart();
        _timer.Start();
    }

    public void End()
    {
        _timer.Stop();
        _clock.Stop();
        _hasCurrent = false;
        _target = null;
    }

    private void Step()
    {
        if (_target is { } t && _hasCurrent)
        {
            // Critically-damped-ish ease toward the target each frame.
            const double k = 0.2;
            _current = new Rect(
                _current.X + (t.X - _current.X) * k,
                _current.Y + (t.Y - _current.Y) * k,
                _current.Width + (t.Width - _current.Width) * k,
                _current.Height + (t.Height - _current.Height) * k);
        }
        InvalidateVisual();
    }

    private Color AccentColor()
    {
        if (Application.Current is { } app
            && app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var value)
            && value is Color c)
            return c;
        return FallbackAccent;
    }

    public override void Render(DrawingContext context)
    {
        var full = new Rect(Bounds.Size);
        var dim = new SolidColorBrush(Color.FromArgb(178, 6, 8, 20));

        if (!_hasCurrent || _target is null)
        {
            context.DrawRectangle(dim, null, full);
            return;
        }

        var hole = _current.Inflate(8);
        var geometry = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(full),
            new RectangleGeometry(hole, 12, 12));
        context.DrawGeometry(dim, null, geometry);

        var accent = AccentColor();
        var seconds = _clock.Elapsed.TotalSeconds;

        // Solid accent outline.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(accent), 2), hole, 12, 12);

        // Two staggered expanding rings.
        for (var i = 0; i < 2; i++)
        {
            var t = (seconds / 1.6 + i * 0.5) % 1.0;           // 0..1 loop
            var grow = 4 + 20 * EaseOut(t);
            var alpha = (byte)(190 * (1 - t));
            var ring = hole.Inflate(grow);
            context.DrawRectangle(null,
                new Pen(new SolidColorBrush(Color.FromArgb(alpha, accent.R, accent.G, accent.B)), 2.5),
                ring, 12 + grow * 0.5, 12 + grow * 0.5);
        }

        DrawArrow(context, hole, accent, seconds, full);
    }

    private static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);

    private static void DrawArrow(DrawingContext context, Rect hole, Color accent, double seconds, Rect full)
    {
        var bounce = (Math.Sin(seconds * 6) + 1) * 0.5 * 7;     // 0..7 px
        var cy = hole.Center.Y;

        // Prefer pointing from the right (the sidebar targets sit on the left
        // edge); flip to the left if there's no room.
        var fromRight = hole.Right + 56 < full.Width;
        var tipX = fromRight ? hole.Right + 6 + bounce : hole.Left - 6 - bounce;
        var dir = fromRight ? 1 : -1;

        var points = new[]
        {
            new Point(tipX, cy),
            new Point(tipX + dir * 16, cy - 10),
            new Point(tipX + dir * 16, cy + 10),
        };

        context.DrawGeometry(
            new SolidColorBrush(accent),
            new Pen(Brushes.White, 1.5),
            new PolylineGeometry(points, true));
    }
}
