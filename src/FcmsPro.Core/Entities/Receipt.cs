namespace FcmsPro.Core.Entities;

/// <summary>
/// Auto-generated only, one per Payment (1:1). A denormalized snapshot of
/// client + commission + payment state at the moment of payment, so a
/// historical receipt does not change if the client/commission is later edited.
/// Deleted when its Payment is refunded.
/// </summary>
public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = string.Empty;

    public Guid PaymentId { get; set; }
    public Guid CommissionId { get; set; }
    public Guid ClientId { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string? ClientPhone { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientNotes { get; set; }

    public string CommissionTitle { get; set; } = string.Empty;
    public string CommissionStatus { get; set; } = string.Empty;
    public decimal CommissionPrice { get; set; }
    public string? ServiceType { get; set; }
    public string? ClientNote { get; set; }

    public decimal DownPayment { get; set; }
    public decimal PreviousPayments { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingBalance { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public string VerificationCode { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
