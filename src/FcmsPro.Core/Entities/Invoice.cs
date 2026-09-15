using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Invoice : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public Guid? CommissionId { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }

    /// <summary>Stored, recomputed by InvoiceService: max(0, Subtotal - Discount + Tax).</summary>
    public decimal Total { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string? PoNumber { get; set; }

    /// <summary>
    /// Literal stored status. "Overdue" as *displayed* is computed separately by
    /// MetricsService.GetEffectiveStatus() when Status isn't Paid/Cancelled and
    /// DueDate has passed - the stored value only becomes literally Overdue if
    /// the user sets it manually. See Phase 1 audit §4.3.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
