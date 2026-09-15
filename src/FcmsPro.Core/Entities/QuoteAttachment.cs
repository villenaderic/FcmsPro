namespace FcmsPro.Core.Entities;

/// <summary>File attached to a quote - reference file, mockup, etc. Mirrors CommissionAttachment exactly.</summary>
public class QuoteAttachment : IAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
