using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Quote : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuoteNumber { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public string? ServiceType { get; set; }
    public string Scope { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? DownPayment { get; set; }
    public int Revisions { get; set; } = 2;

    public DateOnly IssueDate { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? Terms { get; set; }

    /// <summary>Same effective-status treatment as Invoice.Status - see Phase 1 audit §4.3.</summary>
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
