using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers ExpenseService's time-based recurrence: unlike Commission (which
/// spawns off a single status-transition event), a recurring expense has no
/// lifecycle event to hook - so CreateAsync/UpdateAsync anchor NextOccurrence,
/// and ProcessDueRecurrencesAsync (run once at app startup) is what actually
/// spawns copies once that date arrives.
/// </summary>
public class ExpenseServiceRecurrenceTests
{
    private static Mock<IUnitOfWork> BuildMockUow(out Mock<IExpenseRepository> expenses, out List<Expense> store)
    {
        var uow = new Mock<IUnitOfWork>();
        expenses = new Mock<IExpenseRepository>();
        var logs = new Mock<IAuditLogRepository>();
        var capturedStore = new List<Expense>();
        store = capturedStore;

        uow.SetupGet(u => u.Expenses).Returns(expenses.Object);
        uow.SetupGet(u => u.AuditLogs).Returns(logs.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        expenses.Setup(e => e.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, CancellationToken>((e, _) => capturedStore.Add(e))
            .Returns(Task.CompletedTask);
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedStore.ToList());

        return uow;
    }

    [Fact]
    public async Task CreatingARecurringExpense_AnchorsNextOccurrenceFromDate()
    {
        var uow = BuildMockUow(out _, out _);
        var service = new ExpenseService(uow.Object);

        var created = await service.CreateAsync(new Expense
        {
            Description = "Design software subscription",
            Category = ExpenseCategory.SoftwareTools,
            Amount = 20m,
            Date = new DateOnly(2026, 1, 1),
            RecurFrequency = RecurFrequency.Monthly
        });

        Assert.Equal(new DateOnly(2026, 2, 1), created.NextOccurrence);
    }

    [Fact]
    public async Task CreatingANonRecurringExpense_LeavesNextOccurrenceNull()
    {
        var uow = BuildMockUow(out _, out _);
        var service = new ExpenseService(uow.Object);

        var created = await service.CreateAsync(new Expense
        {
            Description = "One-off printer cartridge",
            Category = ExpenseCategory.OfficeSupplies,
            Amount = 15m,
            Date = new DateOnly(2026, 1, 1),
            RecurFrequency = RecurFrequency.None
        });

        Assert.Null(created.NextOccurrence);
    }

    [Fact]
    public async Task ProcessDueRecurrences_SpawnsOneCopyAndAdvancesNextOccurrence()
    {
        var uow = BuildMockUow(out var expenses, out var store);
        var service = new ExpenseService(uow.Object);

        // Due exactly as of "today" (not a hardcoded date) so the catch-up
        // loop runs exactly once regardless of the sandbox's system clock.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var source = new Expense
        {
            Id = Guid.NewGuid(),
            Description = "Monthly retainer tool",
            Category = ExpenseCategory.SoftwareTools,
            Amount = 50m,
            Date = today,
            RecurFrequency = RecurFrequency.Monthly,
            NextOccurrence = today // already due as of "today"
        };
        store.Add(source);
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => store.ToList());

        await service.ProcessDueRecurrencesAsync();

        // Exactly one new copy spawned (the seed source expense plus one spawn = 2 total).
        Assert.Equal(2, store.Count);
        var spawned = store.Single(e => e.Id != source.Id);
        Assert.Equal(source.Description, spawned.Description);
        Assert.Equal(source.Amount, spawned.Amount);
        Assert.Equal(today, spawned.Date);
        Assert.Equal(RecurFrequency.None, spawned.RecurFrequency); // the spawned copy itself does not recur
        Assert.Null(spawned.NextOccurrence);

        // Source's own schedule advances one month past the spawned occurrence.
        Assert.Equal(today.AddMonths(1), source.NextOccurrence);
    }

    [Fact]
    public async Task ProcessDueRecurrences_CatchesUpMultipleMissedPeriods()
    {
        var uow = BuildMockUow(out var expenses, out var store);
        var service = new ExpenseService(uow.Object);

        // Weekly expense that hasn't run in 3 weeks (app wasn't opened) -
        // anchored relative to today so this test doesn't depend on the
        // sandbox's system clock relative to a hardcoded date.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var threeWeeksAgo = today.AddDays(-21);
        var source = new Expense
        {
            Id = Guid.NewGuid(),
            Description = "Weekly cloud backup",
            Category = ExpenseCategory.InternetUtilities,
            Amount = 5m,
            Date = threeWeeksAgo,
            RecurFrequency = RecurFrequency.Weekly,
            NextOccurrence = threeWeeksAgo
        };
        store.Add(source);
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => store.ToList());

        await service.ProcessDueRecurrencesAsync();

        // Occurrences land on day -21, -14, -7, and 0 (today itself counts as
        // due) - four spawns - and the source's own schedule should now sit
        // strictly in the future.
        Assert.Equal(5, store.Count); // source + 4 spawned copies
        Assert.True(source.NextOccurrence > today);
        Assert.All(store.Where(e => e.Id != source.Id), e => Assert.Equal(RecurFrequency.None, e.RecurFrequency));
    }

    [Fact]
    public async Task ProcessDueRecurrences_IgnoresExpensesNotYetDue()
    {
        var uow = BuildMockUow(out var expenses, out var store);
        var service = new ExpenseService(uow.Object);

        var futureExpense = new Expense
        {
            Id = Guid.NewGuid(),
            Description = "Future subscription",
            Amount = 10m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecurFrequency = RecurFrequency.Monthly,
            NextOccurrence = DateOnly.FromDateTime(DateTime.Today).AddMonths(6)
        };
        store.Add(futureExpense);
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => store.ToList());

        await service.ProcessDueRecurrencesAsync();

        Assert.Single(store); // nothing spawned
        expenses.Verify(e => e.Update(It.IsAny<Expense>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueRecurrences_IgnoresDeletedExpenses()
    {
        var uow = BuildMockUow(out var expenses, out var store);
        var service = new ExpenseService(uow.Object);

        var deleted = new Expense
        {
            Id = Guid.NewGuid(),
            Description = "Cancelled subscription",
            Amount = 10m,
            Date = new DateOnly(2026, 1, 1),
            RecurFrequency = RecurFrequency.Monthly,
            NextOccurrence = new DateOnly(2026, 1, 1),
            IsDeleted = true
        };
        store.Add(deleted);
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => store.ToList());

        await service.ProcessDueRecurrencesAsync();

        Assert.Single(store); // nothing spawned for a soft-deleted expense
    }
}
