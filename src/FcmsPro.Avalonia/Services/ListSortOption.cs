namespace FcmsPro.Avalonia.Services;

/// <summary>
/// Shared "sort by" vocabulary for list pages (Commissions, Clients,
/// Payments, Invoices, Quotes, Expenses, Receipts) - one enum rather than a
/// per-module type, since sorting is the same small set of ideas everywhere.
/// Each list ViewModel exposes only the subset that applies to its own data
/// via its own SortOptions list (see e.g. CommissionsListViewModel), so a
/// page never offers a sort with nothing to sort by (Receipts has no
/// deadline, Clients has no amount).
/// </summary>
public enum ListSortOption
{
    Newest,
    Oldest,
    AmountHighToLow,
    AmountLowToHigh,
    DeadlineSoonest,
    NameAZ,
    NameZA,
}
