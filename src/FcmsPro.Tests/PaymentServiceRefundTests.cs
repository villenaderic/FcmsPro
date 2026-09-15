using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers the refund recalculation rule from Phase 1 audit §2.3: refunding a
/// payment must soft-delete it (Round 11 changed this from a hard delete to
/// match every other delete path in the app), hard-delete its linked receipt
/// if present, and recompute Remaining from all OTHER payments on the commission.
/// </summary>
public class PaymentServiceRefundTests
{
    private static Mock<IUnitOfWork> BuildMockUow(
        out Mock<IPaymentRepository> payments,
        out Mock<IReceiptRepository> receipts,
        out Mock<ICommissionRepository> commissions,
        out Mock<IAuditLogRepository> logs,
        decimal sumOfOtherPayments)
    {
        var uow = new Mock<IUnitOfWork>();
        payments = new Mock<IPaymentRepository>();
        receipts = new Mock<IReceiptRepository>();
        commissions = new Mock<ICommissionRepository>();
        logs = new Mock<IAuditLogRepository>();

        uow.SetupGet(u => u.Payments).Returns(payments.Object);
        uow.SetupGet(u => u.Receipts).Returns(receipts.Object);
        uow.SetupGet(u => u.Commissions).Returns(commissions.Object);
        uow.SetupGet(u => u.AuditLogs).Returns(logs.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((work, _) => work());

        payments.Setup(p => p.SumForCommissionExcludingAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sumOfOtherPayments);

        return uow;
    }

    [Fact]
    public async Task Refund_RecalculatesRemainingFromOtherPayments_AndDeletesReceipt()
    {
        var commissionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var uow = BuildMockUow(out var payments, out var receipts, out var commissions, out _, sumOfOtherPayments: 300m);

        var linkedReceipt = new Receipt { Id = Guid.NewGuid(), PaymentId = paymentId, CommissionId = commissionId };
        receipts.Setup(r => r.GetByPaymentIdAsync(paymentId, It.IsAny<CancellationToken>())).ReturnsAsync(linkedReceipt);

        var commission = new Commission
        {
            Id = commissionId,
            Title = "Full-Body Illustration",
            Price = 1000m,
            DownPayment = 200m,
            Remaining = 300m // was 1000 - 200 - (300 other + 200 refunded) before refund
        };
        var payment = new Payment { Id = paymentId, CommissionId = commissionId, Amount = 200m };

        var service = new PaymentService(uow.Object);
        await service.RefundAsync(payment, commission);

        // Remaining = Price - DownPayment - sum(other payments) = 1000 - 200 - 300 = 500
        Assert.Equal(500m, commission.Remaining);
        // Refund soft-deletes the payment (Round 11 changed this from a hard
        // Remove to Update + IsDeleted, same as every other delete path in
        // the app) - the linked receipt, however, is still hard-removed;
        // that asymmetry is intentional, see PaymentService's own comment
        // above BulkDeleteAsync.
        Assert.True(payment.IsDeleted);
        payments.Verify(p => p.Update(payment), Times.Once);
        payments.Verify(p => p.Remove(It.IsAny<Payment>()), Times.Never);
        receipts.Verify(r => r.Remove(linkedReceipt), Times.Once);
        commissions.Verify(c => c.Update(commission), Times.Once);
    }

    [Fact]
    public async Task Refund_WithNoLinkedReceipt_StillRecalculatesRemaining()
    {
        var commissionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var uow = BuildMockUow(out var payments, out var receipts, out _, out _, sumOfOtherPayments: 0m);

        receipts.Setup(r => r.GetByPaymentIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);

        var commission = new Commission { Id = commissionId, Price = 500m, DownPayment = 0m, Remaining = 0m };
        var payment = new Payment { Id = paymentId, CommissionId = commissionId, Amount = 500m };

        var service = new PaymentService(uow.Object);
        await service.RefundAsync(payment, commission);

        Assert.Equal(500m, commission.Remaining);
        receipts.Verify(r => r.Remove(It.IsAny<Receipt>()), Times.Never);
    }

    [Fact]
    public async Task RecordPayment_RejectsAmountExceedingRemainingBalance()
    {
        var uow = BuildMockUow(out _, out _, out _, out _, sumOfOtherPayments: 0m);
        var service = new PaymentService(uow.Object);

        var commission = new Commission { Id = Guid.NewGuid(), Price = 100m, Remaining = 50m };
        var payment = new Payment { Amount = 75m, Date = DateOnly.FromDateTime(DateTime.Today) };
        var settings = new AppSettings();

        await Assert.ThrowsAsync<PaymentValidationException>(
            () => service.RecordPaymentAsync(payment, commission, settings));
    }
}
