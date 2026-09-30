using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FcmsPro.Avalonia.Converters;

/// <summary>
/// Text color for a status pill. Every bucket uses white text except
/// Warning - white-on-#F59E0B amber has weak contrast, so that one bucket
/// gets a dark brown instead, matching the same dark-on-amber choice
/// already used for the due-soon/warning banners elsewhere in the app.
/// </summary>
public class StatusBadgeForegroundConverter : IValueConverter
{
    public static readonly StatusBadgeForegroundConverter Instance = new();

    private static readonly SolidColorBrush White = new(Colors.White);
    private static readonly SolidColorBrush DarkAmberText = new(Color.Parse("#451A03"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        StatusBadgeColors.GetBucket(value) == StatusBadgeBucket.Warning ? DarkAmberText : White;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
