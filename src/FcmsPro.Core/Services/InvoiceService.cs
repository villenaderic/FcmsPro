using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class InvoiceValidationException : Exception
{
    public InvoiceValidationException(string message) : base(message) { }
}

public class InvoiceService
{
    private readonly IUnitOfWork _uow;

    public InvoiceService(IUnitOfWork uow) => _uow = uow;

    public async Task<Invoice> CreateAsync(Invoice invoice, CancellationToken ct = default)
    {
        Validate(invoice);
        invoice.Id = Guid.NewGuid();
        invoice.Total = ComputeTotal(invoice);
        invoice.CreatedAt = DateTimeOffset.UtcNow;
        invoice.UpdatedAt = invoice.CreatedAt;

        var seq = await _uow.Counters.NextAsync("invoice_seq", ct);
        invoice.InvoiceNumber = $"INV-{seq:D5}";

        await _uow.Invoices.AddAsync(invoice, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Created invoice: {invoice.InvoiceNumber}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return invoice;
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        Validate(invoice);
        invoice.Total = ComputeTotal(invoice);
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        _uow.Invoices.Update(invoice);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Update,
            Message = $"Updated invoice: {invoice.InvoiceNumber}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sets literal Status = Paid with no validation against actual Payment
    /// records - Invoices and the Payments/Commissions-remaining ledger are two
    /// independently-tracked systems, matching PWA behavior (Phase 1 audit §2.5,
    /// flagged there as a product gap worth knowing about but preserved for parity).
    /// </summary>
    public async Task MarkPaidAsync(Invoice invoice, CancellationToken ct = default)
    {
        invoice.Status = InvoiceStatus.Paid;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        _uow.Invoices.Update(invoice);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Invoice invoice, CancellationToken ct = default)
    {
        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Invoices.Update(invoice);
        await _uow.SaveChangesAsync(ct);
    }

    private static decimal ComputeTotal(Invoice invoice) =>
        Math.Max(0, invoice.Subtotal - invoice.Discount + invoice.Tax);

    private static void Validate(Invoice invoice)
    {
        if (invoice.ClientId == Guid.Empty)
            throw new InvoiceValidationException("A client must be selected.");
        if (string.IsNullOrWhiteSpace(invoice.Description))
            throw new InvoiceValidationException("Description is required.");
        if (invoice.Subtotal <= 0)
            throw new InvoiceValidationException("Subtotal must be greater than zero.");
    }
}
