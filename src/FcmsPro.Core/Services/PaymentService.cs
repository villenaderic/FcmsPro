using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class PaymentValidationException : Exception
{
    public PaymentValidationException(string message) : base(message) { }
}

public class PaymentService
{
    private readonly IUnitOfWork _uow;
    private const decimal FloatTolerance = 0.005m;

    public PaymentService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Records a payment against a commission and auto-generates its receipt in
    /// one atomic transaction (Phase 1 flagged the PWA does this as two sequential
    /// IndexedDB operations rather than a true transaction - fixed here since it's
    /// free to do correctly with EF Core).
    /// </summary>
    public async Task<Payment> RecordPaymentAsync(
        Payment payment,
        Commission commission,
        AppSettings businessSettings,
        CancellationToken ct = default)
    {
        if (payment.Amount <= 0)
            throw new PaymentValidationException("Payment amount must be greater than zero.");
        if (payment.Amount > commission.Remaining + FloatTolerance)
            throw new PaymentValidationException("Payment amount cannot exceed the remaining balance.");

        return payment;
    }

    /// <summary>
    /// Soft-deletes the payment (moves it to the trash - see TrashService),
    /// deletes its linked receipt (receipts aren't in the soft-delete scope -
    /// a receipt for a refunded payment shouldn't exist as a valid document
    /// regardless), and recalculates the commission's Remaining balance from
    /// all OTHER, non-deleted payments (GetByCommissionIdAsync/
    /// SumForCommissionExcludingAsync both already filter out soft-deleted
    /// rows, so this payment stops counting toward the balance immediately).
    /// </summary>
    public async Task RefundAsync(Payment payment, Commission commission, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async () =>
        {

            payment.IsDeleted = true;
            payment.DeletedAt = DateTimeOffset.UtcNow;
            _uow.Payments.Update(payment);

            var remainingOtherPayments = await _uow.Payments.SumForCommissionExcludingAsync(commission.Id, payment.Id, ct);
            commission.Remaining = Math.Max(0, commission.Price - commission.DownPayment - remainingOtherPayments);
            commission.UpdatedAt = DateTimeOffset.UtcNow;
            _uow.Commissions.Update(commission);

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Type = AuditLogType.Delete,
                Message = $"Refunded payment for {commission.Title}"
            }, ct);

            await _uow.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Bulk delete does NOT recalculate commission balances - intentionally
    /// asymmetric with RefundAsync, matching the PWA's documented behavior
    /// (Phase 1 audit §2.3, Phase 2 §2 default). If you'd rather this recompute
    /// balances too, this is the one place to change it.
    /// </summary>
    public async Task BulkDeleteAsync(IEnumerable<Payment> payments, CancellationToken ct = default)
    {
        foreach (var payment in payments)
        {
            payment.IsDeleted = true;
            payment.DeletedAt = DateTimeOffset.UtcNow;
            _uow.Payments.Update(payment);
        }

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Delete,
            Message = $"Bulk-deleted {payments.Count()} payment(s)"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static string GenerateVerificationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
        Span<char> code = stackalloc char[8];
        for (var i = 0; i < code.Length; i++)
            code[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(code);
    }
}
