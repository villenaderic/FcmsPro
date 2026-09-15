using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Metrics;

/// <summary>
/// Single shared implementation of overdue detection, effective-status
/// computation, and KPI/analytics math - the PWA had three separate
/// implementations of overdue logic across Dashboard, Commissions kanban, and
/// Invoices table; this class replaces all of them. All values here are
/// computed live on each call (no caching/materialized values), matching PWA
/// behavior - see Phase 1 audit §3 for the perf note on indexing at 100k+ rows.
/// </summary>
public class MetricsService
{
    private readonly IUnitOfWork _uow;

    public MetricsService(IUnitOfWork uow) => _uow = uow;

    public static bool IsCommissionOverdue(Commission c, DateOnly today) =>
        c.Deadline.HasValue
        && c.Deadline.Value < today
        && c.Status != CommissionStatus.Delivered
        && c.Status != CommissionStatus.Cancelled;

    public static bool IsInvoiceOverdue(Invoice invoice, DateOnly today) =>
        invoice.DueDate < today
        && invoice.Status != InvoiceStatus.Paid
        && invoice.Status != InvoiceStatus.Cancelled;

    /// <summary>Default lookahead window for "due soon" - matches the item's own wording ("due in 2 days"), not user-configurable yet.</summary>
    public const int DueSoonWindowDays = 3;

    /// <summary>
    /// True when the deadline lands within the next DueSoonWindowDays days
    /// (today counts as due soon, not yet overdue - a deadline of today
    /// hasn't technically passed until IsCommissionOverdue's strictly-less-than
    /// check kicks in tomorrow). Deliberately mutually exclusive with
    /// IsCommissionOverdue: once the deadline has passed, it's overdue, not
    /// due-soon - a commission is never both at once.
    /// </summary>
    public static bool IsCommissionDueSoon(Commission c, DateOnly today, int windowDays = DueSoonWindowDays) =>
        c.Deadline.HasValue
        && c.Deadline.Value >= today
        && c.Deadline.Value <= today.AddDays(windowDays)
        && c.Status != CommissionStatus.Delivered
        && c.Status != CommissionStatus.Cancelled;

    public static bool IsInvoiceDueSoon(Invoice invoice, DateOnly today, int windowDays = DueSoonWindowDays) =>
        invoice.DueDate >= today
        && invoice.DueDate <= today.AddDays(windowDays)
        && invoice.Status != InvoiceStatus.Paid
        && invoice.Status != InvoiceStatus.Cancelled;

    public static bool IsQuoteExpired(Quote quote, DateOnly today) =>
        quote.ValidUntil < today
        && quote.Status != QuoteStatus.Accepted
        && quote.Status != QuoteStatus.Declined;

    /// <summary>
    /// Display-only effective status: returns the literal stored Status unless
    /// the invoice is overdue by date, in which case returns Overdue for
    /// display purposes without mutating the stored value. Preserves the PWA's
    /// dual system (Phase 1 audit §4.3 / Phase 2 §2 default).
    /// </summary>
    public static InvoiceStatus GetEffectiveStatus(Invoice invoice, DateOnly today) =>
        IsInvoiceOverdue(invoice, today) ? InvoiceStatus.Overdue : invoice.Status;

    public static QuoteStatus GetEffectiveStatus(Quote quote, DateOnly today) =>
        IsQuoteExpired(quote, today) ? QuoteStatus.Expired : quote.Status;

    public async Task<DashboardKpis> GetDashboardKpisAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var commissions = await _uow.Commissions.GetAllAsync(ct);
        var expenses = await _uow.Expenses.GetAllAsync(ct);
        var invoices = await _uow.Invoices.GetAllAsync(ct);

        decimal totalIncome = 0m;
        decimal thisMonthIncome = 0m;
        decimal pendingBalance = 0m;
        var overdueCount = 0;
        var dueSoonCount = 0;
        var deliveredCount = 0;

        var now = DateTime.Today;

        foreach (var c in commissions)
        {
            var payments = await _uow.Payments.GetByCommissionIdAsync(c.Id, ct);
            var paid = payments.Sum(p => p.Amount) + c.DownPayment;
            totalIncome += paid;
            thisMonthIncome += payments
                .Where(p => p.Date.Year == now.Year && p.Date.Month == now.Month)
                .Sum(p => p.Amount);

            pendingBalance += c.Remaining;

            if (IsCommissionOverdue(c, today))
                overdueCount++;
            else if (IsCommissionDueSoon(c, today))
                dueSoonCount++;

            if (c.Status == CommissionStatus.Delivered)
                deliveredCount++;
        }

        var overdueInvoiceCount = invoices.Count(i => IsInvoiceOverdue(i, today));
        var dueSoonInvoiceCount = invoices.Count(i => IsInvoiceDueSoon(i, today));

        var totalExpenses = expenses.Sum(e => e.Amount);
        var completionRate = commissions.Count == 0 ? 0 : (decimal)deliveredCount / commissions.Count * 100;

        return new DashboardKpis(
            TotalIncome: totalIncome,
            ThisMonthIncome: thisMonthIncome,
            PendingBalance: pendingBalance,
            NetProfit: totalIncome - totalExpenses,
            CompletionRatePercent: completionRate,
            OverdueCount: overdueCount,
            OverdueInvoiceCount: overdueInvoiceCount,
            DueSoonCount: dueSoonCount,
            DueSoonInvoiceCount: dueSoonInvoiceCount);
    }
}

public record DashboardKpis(
    decimal TotalIncome,
    decimal ThisMonthIncome,
    decimal PendingBalance,
    decimal NetProfit,
    decimal CompletionRatePercent,
    int OverdueCount,
    int OverdueInvoiceCount,
    int DueSoonCount,
    int DueSoonInvoiceCount);
