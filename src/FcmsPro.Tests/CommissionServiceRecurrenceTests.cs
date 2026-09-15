using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers the recurrence auto-spawn rule from Phase 1 audit §2.2 / §3: a
/// commission transitioning into Delivered with RecurFrequency != None must
/// spawn exactly one new Pending commission with the interval correctly
/// applied and payments/down-payment NOT carried over.
/// </summary>
public class CommissionServiceRecurrenceTests
{
    private static Mock<IUnitOfWork> BuildMockUow(out Mock<ICommissionRepository> commissions, out Mock<IAuditLogRepository> logs, out Mock<IPaymentRepository> payments)
    {
        var uow = new Mock<IUnitOfWork>();
        commissions = new Mock<ICommissionRepository>();
        logs = new Mock<IAuditLogRepository>();
        payments = new Mock<IPaymentRepository>();
        var settings = new Mock<ISettingsRepository>();
        var invoices = new Mock<IInvoiceRepository>();

        uow.SetupGet(u => u.Commissions).Returns(commissions.Object);
        uow.SetupGet(u => u.AuditLogs).Returns(logs.Object);
        uow.SetupGet(u => u.Payments).Returns(payments.Object);
        uow.SetupGet(u => u.Settings).Returns(settings.Object);
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        payments.Setup(p => p.SumForCommissionExcludingAsync(It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // AutoInvoiceOnDelivery defaults to false on a fresh AppSettings, same
        // as production - these recurrence tests aren't exercising the
        // auto-invoice feature, so this just keeps MaybeAutoCreateInvoiceAsync
        // a no-op for them (it returns immediately after reading this).
        settings.Setup(s => s.GetAppSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings());
        invoices.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Invoice>());

        return uow;
    }

    private static CommissionService BuildService(Mock<IUnitOfWork> uow) =>
        new(uow.Object, new InvoiceService(uow.Object));

    [Theory]
    [InlineData(RecurFrequency.Weekly, 7)]
    [InlineData(RecurFrequency.Biweekly, 14)]
    public async Task DeliveringARecurringCommission_SpawnsOneNewPendingCommission(RecurFrequency frequency, int expectedDayOffset)
    {
        var uow = BuildMockUow(out var commissions, out _, out _);
        Commission? spawned = null;
        commissions.Setup(c => c.AddAsync(It.IsAny<Commission>(), It.IsAny<CancellationToken>()))
            .Callback<Commission, CancellationToken>((c, _) =>
            {
                // First AddAsync call in this flow is only reached via CreateAsync,
                // not UpdateAsync - capture whichever Commission gets added here.
                spawned = c;
            })
            .Returns(Task.CompletedTask);

        var service = BuildService(uow);
        var deadline = new DateOnly(2026, 1, 1);
        var commission = new Commission
        {
            Id = Guid.NewGuid(),
            Title = "Monthly Retainer",
            ClientId = Guid.NewGuid(),
            Price = 1000m,
            DownPayment = 0m,
            Deadline = deadline,
            Status = CommissionStatus.Delivered,
            RecurFrequency = frequency
        };

        await service.UpdateAsync(commission, previousStatus: CommissionStatus.InProgress);

        Assert.NotNull(spawned);
        Assert.Equal(CommissionStatus.Pending, spawned!.Status);
        Assert.Equal(commission.Price, spawned.Remaining); // full price owed again, not carried over
        Assert.Equal(deadline.AddDays(expectedDayOffset), spawned.Deadline);
        Assert.NotEqual(commission.Id, spawned.Id);
    }

    [Fact]
    public async Task DeliveringANonRecurringCommission_DoesNotSpawnAnything()
    {
        var uow = BuildMockUow(out var commissions, out _, out _);
        var addCallCount = 0;
        commissions.Setup(c => c.AddAsync(It.IsAny<Commission>(), It.IsAny<CancellationToken>()))
            .Callback(() => addCallCount++)
            .Returns(Task.CompletedTask);

        var service = BuildService(uow);
        var commission = new Commission
        {
            Id = Guid.NewGuid(),
            Title = "One-off Icon",
            ClientId = Guid.NewGuid(),
            Price = 500m,
            Status = CommissionStatus.Delivered,
            RecurFrequency = RecurFrequency.None
        };

        await service.UpdateAsync(commission, previousStatus: CommissionStatus.InProgress);

        Assert.Equal(0, addCallCount);
    }

    [Fact]
    public async Task StayingDelivered_DoesNotReSpawn()
    {
        // Guards against re-triggering on every subsequent save of an
        // already-Delivered recurring commission (previousStatus == Delivered).
        var uow = BuildMockUow(out var commissions, out _, out _);
        var addCallCount = 0;
        commissions.Setup(c => c.AddAsync(It.IsAny<Commission>(), It.IsAny<CancellationToken>()))
            .Callback(() => addCallCount++)
            .Returns(Task.CompletedTask);

        var service = BuildService(uow);
        var commission = new Commission
        {
            Id = Guid.NewGuid(),
            Title = "Monthly Retainer",
            ClientId = Guid.NewGuid(),
            Price = 1000m,
            Status = CommissionStatus.Delivered,
            RecurFrequency = RecurFrequency.Monthly
        };

        await service.UpdateAsync(commission, previousStatus: CommissionStatus.Delivered);

        Assert.Equal(0, addCallCount);
    }
}
