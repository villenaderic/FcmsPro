using FcmsPro.Core.Enums;

namespace FcmsPro.Core.Entities;

public class Payment : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommissionId { get; set; }

    /// <summary>Denormalized copy of the commission's ClientId at time of payment (matches PWA).</summary>
    public Guid ClientId { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateOnly Date { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
