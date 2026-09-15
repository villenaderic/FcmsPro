using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public enum GlobalSearchResultType { Client, Commission, Invoice, Quote, Expense }

/// <summary>
/// One matched row. ClientId is populated for every type except Expense
/// (which has no client relationship) - the Avalonia side uses it to deep
/// link: Client results reopen that client's profile directly
/// (AppPage.Clients + client Id, same mechanism "View Client" already uses),
/// Commission results reopen the commission detail page directly
/// (AppPage.CommissionDetail + commission Id, ditto), but Invoice/Quote/
/// Expense have no dedicated detail page in this app yet, so those results
/// navigate to the matching list page filtered to the client (or
/// unfiltered, for Expenses) rather than to the exact row - the closest
/// this app's existing navigation model can get without inventing new
/// detail-page infrastructure.
/// </summary>
public record GlobalSearchResult(
    GlobalSearchResultType Type,
    Guid Id,
    Guid? ClientId,
    string Title,
    string Subtitle);

/// <summary>
/// Cross-entity search used by the global search window (default "/"
/// keyboard shortcut - see KeySequenceService.FocusSearchRequested, which
/// existed as an unwired hook before this). Every list page's own
/// SearchQuery only ever filtered that page's already-loaded rows; this is
/// the first place in the app that searches across entity types at once.
///
/// Deliberately simple in-memory Contains matching (mirrors how
/// Commissions/Invoices/Quotes list pages already filter client-side) rather
/// than a proper full-text index - appropriate for a local single-user
/// SQLite file where "all commissions" is realistically a few hundred rows,
/// not a scale where an index would matter.
/// </summary>
public class GlobalSearchService
{
    private readonly IUnitOfWork _uow;
    private const int MaxResultsPerType = 8;

    public GlobalSearchService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<GlobalSearchResult>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var results = new List<GlobalSearchResult>();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return results; // avoid a full-table scan-and-render on a single keystroke

        var q = query.Trim().ToLowerInvariant();

        var clients = await _uow.Clients.SearchAsync(query, ct);
        results.AddRange(clients
            .Where(c => !c.IsDeleted)
            .Take(MaxResultsPerType)
            .Select(c => new GlobalSearchResult(
                GlobalSearchResultType.Client, c.Id, c.Id,
                c.Name,
                c.Email ?? c.Phone ?? "Client")));

        var commissions = await _uow.Commissions.GetAllAsync(ct);
        results.AddRange(commissions
            .Where(c => !c.IsDeleted && Matches(q, c.Title, c.ServiceType, c.Description, c.ClientNote))
            .Take(MaxResultsPerType)
            .Select(c => new GlobalSearchResult(
                GlobalSearchResultType.Commission, c.Id, c.ClientId,
                c.Title,
                c.ServiceType is { Length: > 0 } st ? $"{st} · {c.Status}" : c.Status.ToString())));

        var invoices = await _uow.Invoices.GetAllAsync(ct);
        results.AddRange(invoices
            .Where(i => !i.IsDeleted && Matches(q, i.InvoiceNumber, i.Description, i.PoNumber, i.Notes))
            .Take(MaxResultsPerType)
            .Select(i => new GlobalSearchResult(
                GlobalSearchResultType.Invoice, i.Id, i.ClientId,
                i.InvoiceNumber,
                i.Description)));

        var quotes = await _uow.Quotes.GetAllAsync(ct);
        results.AddRange(quotes
            .Where(qt => !qt.IsDeleted && Matches(q, qt.QuoteNumber, qt.Scope, qt.ServiceType, qt.Terms))
            .Take(MaxResultsPerType)
            .Select(qt => new GlobalSearchResult(
                GlobalSearchResultType.Quote, qt.Id, qt.ClientId,
                qt.QuoteNumber,
                qt.Scope)));

        var expenses = await _uow.Expenses.GetAllAsync(ct);
        results.AddRange(expenses
            .Where(e => !e.IsDeleted && Matches(q, e.Description, e.Notes))
            .Take(MaxResultsPerType)
            .Select(e => new GlobalSearchResult(
                GlobalSearchResultType.Expense, e.Id, null,
                e.Description,
                e.Category.ToString())));

        return results;
    }

    private static bool Matches(string lowerQuery, params string?[] fields) =>
        fields.Any(f => !string.IsNullOrEmpty(f) && f.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
}
