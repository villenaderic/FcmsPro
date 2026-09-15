using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Metrics;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers MetricsService's due-soon predicates - the "due in 2 days, while
/// there's still time to act" gap the overdue-only banner never filled.
/// Deliberately unit-tests the static predicates directly (no IUnitOfWork
/// mocking needed) since that's where the actual date-boundary logic lives;
/// GetDashboardKpisAsync just sums these per-item booleans.
/// </summary>
public class MetricsServiceDueSoonTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    [Fact]
    public void CommissionDueSoon_TodayItself_CountsAsDueSoon()
    {
        var commission = new Commission { Deadline = Today, Status = CommissionStatus.InProgress };
        Assert.True(MetricsService.IsCommissionDueSoon(commission, Today));
        Assert.False(MetricsService.IsCommissionOverdue(commission, Today)); // not yet - see mutual-exclusivity test below
    }

    [Fact]
    public void CommissionDueSoon_WithinWindow_IsDueSoon()
    {
        var commission = new Commission { Deadline = Today.AddDays(2), Status = CommissionStatus.InProgress };
        Assert.True(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Fact]
    public void CommissionDueSoon_ExactlyAtWindowEdge_IsDueSoon()
    {
        var commission = new Commission
        {
            Deadline = Today.AddDays(MetricsService.DueSoonWindowDays),
            Status = CommissionStatus.InProgress
        };
        Assert.True(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Fact]
    public void CommissionDueSoon_JustPastWindow_IsNotDueSoon()
    {
        var commission = new Commission
        {
            Deadline = Today.AddDays(MetricsService.DueSoonWindowDays + 1),
            Status = CommissionStatus.InProgress
        };
        Assert.False(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Fact]
    public void CommissionDueSoon_AlreadyPastDeadline_IsOverdueNotDueSoon()
    {
        // Mutual exclusivity: yesterday's deadline is overdue, not due-soon,
        // even though it's "close" in absolute terms - a commission is never
        // flagged as both at once (see MetricsService's doc comment).
        var commission = new Commission { Deadline = Today.AddDays(-1), Status = CommissionStatus.InProgress };
        Assert.True(MetricsService.IsCommissionOverdue(commission, Today));
        Assert.False(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Fact]
    public void CommissionDueSoon_NoDeadline_IsNeverDueSoon()
    {
        var commission = new Commission { Deadline = null, Status = CommissionStatus.InProgress };
        Assert.False(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Theory]
    [InlineData(CommissionStatus.Delivered)]
    [InlineData(CommissionStatus.Cancelled)]
    public void CommissionDueSoon_AlreadyFinished_IsNotDueSoon(CommissionStatus status)
    {
        var commission = new Commission { Deadline = Today.AddDays(1), Status = status };
        Assert.False(MetricsService.IsCommissionDueSoon(commission, Today));
    }

    [Fact]
    public void InvoiceDueSoon_WithinWindow_IsDueSoon()
    {
        var invoice = new Invoice { DueDate = Today.AddDays(1), Status = InvoiceStatus.Sent };
        Assert.True(MetricsService.IsInvoiceDueSoon(invoice, Today));
    }

    [Fact]
    public void InvoiceDueSoon_AlreadyPaid_IsNotDueSoon()
    {
        var invoice = new Invoice { DueDate = Today.AddDays(1), Status = InvoiceStatus.Paid };
        Assert.False(MetricsService.IsInvoiceDueSoon(invoice, Today));
    }

    [Fact]
    public void InvoiceDueSoon_PastWindow_IsNotDueSoon()
    {
        var invoice = new Invoice { DueDate = Today.AddDays(30), Status = InvoiceStatus.Sent };
        Assert.False(MetricsService.IsInvoiceDueSoon(invoice, Today));
    }

    [Fact]
    public void InvoiceDueSoon_AlreadyOverdue_IsNotDueSoon()
    {
        var invoice = new Invoice { DueDate = Today.AddDays(-3), Status = InvoiceStatus.Sent };
        Assert.True(MetricsService.IsInvoiceOverdue(invoice, Today));
        Assert.False(MetricsService.IsInvoiceDueSoon(invoice, Today));
    }
}
