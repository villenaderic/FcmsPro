using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.Converters;

/// <summary>Human-readable label for a ListSortOption - backs the "Sort by" ComboBox shared across every list page.</summary>
public class ListSortOptionLabelConverter : IValueConverter
{
    public static readonly ListSortOptionLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ListSortOption.Newest => "Newest first",
            ListSortOption.Oldest => "Oldest first",
            ListSortOption.AmountHighToLow => "Amount: High to Low",
            ListSortOption.AmountLowToHigh => "Amount: Low to High",
            ListSortOption.DeadlineSoonest => "Deadline: Soonest",
            ListSortOption.NameAZ => "Name: A-Z",
            ListSortOption.NameZA => "Name: Z-A",
            _ => value?.ToString() ?? string.Empty
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
