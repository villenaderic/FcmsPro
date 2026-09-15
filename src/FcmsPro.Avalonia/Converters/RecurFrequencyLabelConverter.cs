using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FcmsPro.Core.Enums;

namespace FcmsPro.Avalonia.Converters;

/// <summary>
/// Turns a RecurFrequency into a short "↻ Monthly" style label for list-row
/// badges, or an empty string for RecurFrequency.None so the badge's
/// IsVisible (bound via StringConverters.IsNotNullOrEmpty) collapses cleanly
/// instead of showing "↻ None".
/// </summary>
public class RecurFrequencyLabelConverter : IValueConverter
{
    public static readonly RecurFrequencyLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            RecurFrequency.Weekly => "↻ Weekly",
            RecurFrequency.Biweekly => "↻ Biweekly",
            RecurFrequency.Monthly => "↻ Monthly",
            _ => string.Empty
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when RecurFrequency != None - drives the recurring badge's IsVisible in Expenses/Commissions list rows.</summary>
public class IsRecurringConverter : IValueConverter
{
    public static readonly IsRecurringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RecurFrequency freq && freq != RecurFrequency.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
