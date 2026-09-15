using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.Converters;

/// <summary>
/// Compares a bound AppPage (typically NavigationService.CurrentPage) against
/// the ConverterParameter (the sidebar button's own target page) and returns
/// true when they match. Used to toggle each sidebar nav button's "active"
/// style class so the current page is visually highlighted.
/// </summary>
public class PageMatchConverter : IValueConverter
{
    public static readonly PageMatchConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppPage current && parameter is AppPage target)
            return current == target;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
