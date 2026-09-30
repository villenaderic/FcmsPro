using FcmsPro.Core.Enums;

namespace FcmsPro.Avalonia.Converters;

/// <summary>Which color bucket a status pill falls into - shared by StatusBadgeBackgroundConverter and StatusBadgeForegroundConverter so the two never disagree about which status is which color.</summary>
public enum StatusBadgeBucket
{
    Neutral,
    Info,
    Warning,
    Success,
    Danger,
}

/// <summary>
/// Maps CommissionStatus/InvoiceStatus/QuoteStatus to one of five fixed
/// color buckets for the status pill shown on Commissions/Invoices/Quotes
/// rows. Colors are fixed saturated hex values (not theme DynamicResources)
/// deliberately - a status pill's background needs to read the same in
/// light and dark mode the way GitHub/Trello labels do, rather than
/// following the app's own light/dark surface colors, which is what
/// DynamicResource would do here and isn't the effect wanted.
/// </summary>
public static class StatusBadgeColors
{
    public static StatusBadgeBucket GetBucket(object? status) => status switch
    {
        CommissionStatus.Pending => StatusBadgeBucket.Neutral,
        CommissionStatus.InProgress => StatusBadgeBucket.Info,
        CommissionStatus.Revision => StatusBadgeBucket.Warning,
        CommissionStatus.Completed => StatusBadgeBucket.Success,
        CommissionStatus.Delivered => StatusBadgeBucket.Success,
        CommissionStatus.Cancelled => StatusBadgeBucket.Danger,

        InvoiceStatus.Draft => StatusBadgeBucket.Neutral,
        InvoiceStatus.Sent => StatusBadgeBucket.Info,
        InvoiceStatus.Paid => StatusBadgeBucket.Success,
        InvoiceStatus.Overdue => StatusBadgeBucket.Danger,
        InvoiceStatus.Cancelled => StatusBadgeBucket.Neutral,

        QuoteStatus.Draft => StatusBadgeBucket.Neutral,
        QuoteStatus.Sent => StatusBadgeBucket.Info,
        QuoteStatus.Accepted => StatusBadgeBucket.Success,
        QuoteStatus.Declined => StatusBadgeBucket.Danger,
        QuoteStatus.Expired => StatusBadgeBucket.Warning,

        _ => StatusBadgeBucket.Neutral,
    };
}
