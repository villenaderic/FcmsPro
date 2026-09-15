using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Commission : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Soft reference to Client.Id. Not a DB-enforced FK, matching PWA behavior:
    /// deleting a client leaves this pointing at a no-longer-existent row rather
    /// than cascading or blocking. See Phase 1 audit §4.2.
    /// </summary>
    public Guid ClientId { get; set; }

    public string? ServiceType { get; set; }
    public string? Description { get; set; }
    public string? ClientNote { get; set; }

    public decimal Price { get; set; }
    public decimal DownPayment { get; set; }

    /// <summary>
    /// Recomputed by CommissionService on every mutating save:
    /// max(0, Price - (DownPayment + sum of all Payments for this commission)).
    /// Stored (not computed-on-read) to match PWA semantics and avoid recomputing
    /// on every list render at 100k+ record scale.
    /// </summary>
    public decimal Remaining { get; set; }

    public DateOnly? Deadline { get; set; }
    public CommissionStatus Status { get; set; } = CommissionStatus.Pending;

    /// <summary>
    /// Made a first-class persisted field on all commissions (Phase 1 audit §4.1
    /// default: PWA only wired this into one of two form variants).
    /// </summary>
    public CommissionPriority Priority { get; set; } = CommissionPriority.Normal;

    public RecurFrequency RecurFrequency { get; set; } = RecurFrequency.None;

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
