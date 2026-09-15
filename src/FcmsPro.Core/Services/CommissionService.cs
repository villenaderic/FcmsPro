using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

public class CommissionValidationException : Exception
{
    public CommissionValidationException(string message) : base(message) { }
}

public class CommissionService
{
    private readonly IUnitOfWork _uow;
    private readonly InvoiceService _invoiceService;

    public CommissionService(IUnitOfWork uow, InvoiceService invoiceService)
    {
        _uow = uow;
        _invoiceService = invoiceService;
    }

    public async Task<Commission> CreateAsync(Commission commission, CancellationToken ct = default)
    {
        Validate(commission);
        commission.Id = Guid.NewGuid();
        commission.DateAdded = DateTimeOffset.UtcNow;
        commission.UpdatedAt = commission.DateAdded;
        commission.Remaining = Math.Max(0, commission.Price - commission.DownPayment);

        await _uow.Commissions.AddAsync(commission, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Added commission: {commission.Title}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return commission;
    }

    /// <summary>
    /// Updates an existing commission. Recomputes Remaining defensively from
    /// actual Payment records (not trusted from the incoming form state) - matches
    /// PWA behavior. If the status transitions into Delivered from a non-Delivered
    /// status and RecurFrequency != None, spawns a new recurring commission.
    /// This must be called from BOTH the full edit form AND the inline quick-status
    /// change path so recurrence behavior is identical either way (Phase 1 audit §2.2).
    /// </summary>
    public async Task UpdateAsync(Commission commission, CommissionStatus previousStatus, CancellationToken ct = default)
    {
        Validate(commission);
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        var paymentsSum = await _uow.Payments.SumForCommissionExcludingAsync(commission.Id, null, ct);
        commission.Remaining = Math.Max(0, commission.Price - commission.DownPayment - paymentsSum);

        _uow.Commissions.Update(commission);

        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Update,
            Message = $"Updated commission: {commission.Title}"
        }, ct);

        Commission? spawned = null;
        var justDelivered = previousStatus != CommissionStatus.Delivered
            && commission.Status == CommissionStatus.Delivered;

        if (justDelivered && commission.RecurFrequency != RecurFrequency.None)
        {
            spawned = BuildRecurringCopy(commission);
            await _uow.Commissions.AddAsync(spawned, ct);
            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                Type = AuditLogType.Create,
                Message = $"Auto-created recurring commission: {spawned.Title}"
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);

        // Runs after the SaveChangesAsync above (not before) because
        // InvoiceService.CreateAsync does its own separate SaveChangesAsync
        // and reads commission.Remaining via the repository - keeping this
        // commission's own update fully persisted first avoids any chance of
        // the auto-generated invoice being created against not-yet-committed
        // state if something upstream changes this method's ordering later.
        if (justDelivered)
            await MaybeAutoCreateInvoiceAsync(commission, ct);
    }

    /// <summary>
    /// Opt-in (AppSettings.AutoInvoiceOnDelivery, default off): when a
    /// commission is marked Delivered and still has money owed, generate a
    /// Draft invoice for the remaining balance automatically instead of
    /// requiring a manual "New Invoice" step. Skips silently if the setting
    /// is off, nothing is owed, or an invoice already exists for this
    /// commission (so flipping a commission back and forth through Delivered
    /// can't spam duplicate invoices).
    /// </summary>
    private async Task MaybeAutoCreateInvoiceAsync(Commission commission, CancellationToken ct)
    {
        var settings = await _uow.Settings.GetAppSettingsAsync(ct);
        if (!settings.AutoInvoiceOnDelivery) return;
        if (commission.Remaining <= 0) return;

        var existing = await _uow.Invoices.GetAllAsync(ct);
        if (existing.Any(i => i.CommissionId == commission.Id)) return;

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await _invoiceService.CreateAsync(new Invoice
        {
            ClientId = commission.ClientId,
            CommissionId = commission.Id,
            Description = $"Balance due for: {commission.Title}",
            Subtotal = commission.Remaining,
            Discount = 0,
            Tax = 0,
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(Math.Max(0, settings.InvoiceDueDays)),
            Status = InvoiceStatus.Draft,
            Terms = settings.InvoiceTerms,
            Notes = settings.InvoiceNotes
        }, ct);
    }

    /// <summary>
    /// Same spawn logic as UpdateAsync, exposed separately for the inline
    /// quick-status dropdown path so both call sites share one implementation
    /// instead of two independent copies (Phase 1 flagged the PWA has two).
    /// </summary>
    public Task QuickStatusChangeAsync(Commission commission, CommissionStatus previousStatus, CancellationToken ct = default)
        => UpdateAsync(commission, previousStatus, ct);

    private static Commission BuildRecurringCopy(Commission source)
    {
        var baseDate = source.Deadline.HasValue
            ? source.Deadline.Value
            : DateOnly.FromDateTime(source.DateAdded.UtcDateTime);

        var now = DateTimeOffset.UtcNow;
        return new Commission
        {
            Id = Guid.NewGuid(),
            Title = source.Title,
            ClientId = source.ClientId,
            ServiceType = source.ServiceType,
            Description = source.Description,
            ClientNote = source.ClientNote,
            Price = source.Price,
            DownPayment = source.DownPayment,
            // Full price owed again - payments/down payment are NOT carried over (matches PWA).
            Remaining = source.Price,
            Deadline = AddRecurInterval(baseDate, source.RecurFrequency),
            Status = CommissionStatus.Pending,
            Priority = source.Priority,
            RecurFrequency = source.RecurFrequency,
            DateAdded = now,
            UpdatedAt = now
        };
    }

    private static DateOnly AddRecurInterval(DateOnly date, RecurFrequency frequency) => frequency switch
    {
        RecurFrequency.Weekly => date.AddDays(7),
        RecurFrequency.Biweekly => date.AddDays(14),
        RecurFrequency.Monthly => date.AddMonths(1),
        _ => date
    };

    public async Task<Commission> DuplicateAsync(Commission source, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = new Commission
        {
            Id = Guid.NewGuid(),
            Title = source.Title + " (Copy)",
            ClientId = source.ClientId,
            ServiceType = source.ServiceType,
            Description = source.Description,
            ClientNote = source.ClientNote,
            Price = source.Price,
            DownPayment = source.DownPayment,
            Remaining = source.Price,
            Deadline = source.Deadline,
            Status = CommissionStatus.Pending,
            Priority = source.Priority,
            RecurFrequency = source.RecurFrequency,
            DateAdded = now,
            UpdatedAt = now
        };

        await _uow.Commissions.AddAsync(copy, ct);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Create,
            Message = $"Duplicated commission: {copy.Title}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return copy;
    }

    /// <summary>No cascade guard - matches PWA (simple confirm-only delete).</summary>
    public async Task DeleteAsync(Commission commission, CancellationToken ct = default)
    {
        commission.IsDeleted = true;
        commission.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Commissions.Update(commission);
        await _uow.AuditLogs.AddAsync(new AuditLog
        {
            Type = AuditLogType.Delete,
            Message = $"Moved commission to trash: {commission.Title}"
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static void Validate(Commission commission)
    {
        if (string.IsNullOrWhiteSpace(commission.Title))
            throw new CommissionValidationException("Title is required.");
        if (commission.ClientId == Guid.Empty)
            throw new CommissionValidationException("A client must be selected.");
        if (commission.Price <= 0)
            throw new CommissionValidationException("Price must be greater than zero.");
        if (commission.DownPayment > commission.Price)
            throw new CommissionValidationException("Down payment cannot exceed the price.");
    }
}
