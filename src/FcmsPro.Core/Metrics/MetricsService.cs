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
        && c.Status != CommissionStatus.Completed
        && c.Status != CommissionStatus.Delivered
        && c.Status != CommissionStatus.Cancelled;

    public const int DueSoonWindowDays = 7;

    public static bool IsCommissionDueSoon(Commission c, DateOnly today) =>
        c.Deadline.HasValue
        && c.Deadline.Value >= today
        && c.Deadline.Value <= today.AddDays(DueSoonWindowDays)
        && c.Status != CommissionStatus.Completed
        && c.Status != CommissionStatus.Delivered
        && c.Status != CommissionStatus.Cancelled;

    public async Task<DashboardKpis> GetDashboardKpisAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        
        var clients = await _uow.Clients.GetAllAsync(ct);
        var activeClientIds = clients.Where(c => !c.IsDeleted).Select(c => c.Id).ToHashSet();
        
        var allCommissions = await _uow.Commissions.GetAllAsync(ct);
        var activeCommissions = allCommissions.Where(c => !c.IsDeleted && activeClientIds.Contains(c.ClientId)).ToList();
        

        var activeCommissionIds = activeCommissions.Select(c => c.Id).ToHashSet();
        
        var allPayments = await _uow.Payments.GetAllAsync(ct);
        var activePayments = allPayments.Where(p => !p.IsDeleted && activeCommissionIds.Contains(p.CommissionId)).ToList();
        
        var allExpenses = await _uow.Expenses.GetAllAsync(ct);
        var activeExpenses = allExpenses.Where(e => !e.IsDeleted).ToList();

        decimal totalIncome = activePayments.Sum(p => p.Amount) + activeCommissions.Sum(c => c.DownPayment);
        
        var now = DateTime.Today;
        decimal thisMonthIncome = activePayments
            .Where(p => p.Date.Year == now.Year && p.Date.Month == now.Month)
            .Sum(p => p.Amount);

        decimal pendingBalance = activeCommissions.Sum(c => c.Remaining);
        
        var overdueCount = activeCommissions.Count(c => IsCommissionOverdue(c, today));
        var dueSoonCount = activeCommissions.Count(c => IsCommissionDueSoon(c, today));
        var deliveredCount = activeCommissions.Count(c => c.Status == CommissionStatus.Delivered);



        var totalExpenses = activeExpenses.Sum(e => e.Amount);
        var completionRate = activeCommissions.Count == 0 ? 0 : (decimal)deliveredCount / activeCommissions.Count * 100;

        return new DashboardKpis(
            TotalIncome: totalIncome,
            ThisMonthIncome: thisMonthIncome,
            PendingBalance: pendingBalance,
            NetProfit: totalIncome - totalExpenses,
            CompletionRatePercent: completionRate,
            OverdueCount: overdueCount,
            DueSoonCount: dueSoonCount);
    }
}

public record DashboardKpis(
    decimal TotalIncome,
    decimal ThisMonthIncome,
    decimal PendingBalance,
    decimal NetProfit,
    decimal CompletionRatePercent,
    int OverdueCount,
    int DueSoonCount);
