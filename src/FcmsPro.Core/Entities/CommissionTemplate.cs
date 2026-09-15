namespace FcmsPro.Core.Entities;

public class CommissionTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? ServiceType { get; set; }
    public decimal Price { get; set; }

    /// <summary>Day offset from "today", applied when the template is used - not an absolute date.</summary>
    public int DeadlineDays { get; set; }

    public string? Description { get; set; }
    public decimal DownPayment { get; set; }

    /// <summary>True only for the 8 seeded defaults. "Reset Defaults" re-seeds rows where this is true.</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
