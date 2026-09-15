namespace FcmsPro.Core.Entities;

/// <summary>File attached to an invoice - reference file, signed copy, etc. Mirrors CommissionAttachment exactly.</summary>
public class InvoiceAttachment : IAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
