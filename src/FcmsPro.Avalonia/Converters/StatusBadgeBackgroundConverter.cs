using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FcmsPro.Avalonia.Converters;

/// <summary>Background brush for a status pill - see StatusBadgeColors for the status-to-bucket mapping.</summary>
public class StatusBadgeBackgroundConverter : IValueConverter
{
    public static readonly StatusBadgeBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        StatusBadgeColors.GetBucket(value) switch
        {
            StatusBadgeBucket.Info => new SolidColorBrush(Color.Parse("#3B82F6")),
            StatusBadgeBucket.Warning => new SolidColorBrush(Color.Parse("#F59E0B")),
            StatusBadgeBucket.Success => new SolidColorBrush(Color.Parse("#10B981")),
            StatusBadgeBucket.Danger => new SolidColorBrush(Color.Parse("#EF4444")),
            _ => new SolidColorBrush(Color.Parse("#6B7280")), // Neutral
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
