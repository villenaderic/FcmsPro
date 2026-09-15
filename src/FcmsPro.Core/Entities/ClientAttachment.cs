namespace FcmsPro.Core.Entities;

/// <summary>
/// File attached to a client - e.g. a signed contract - mirrors
/// CommissionAttachment exactly (see that class for the on-disk storage
/// explanation: only this metadata row lives in the database, the actual
/// bytes live under FcmsPaths.GetClientAttachmentsDirectory(clientId)).
/// </summary>
public class ClientAttachment : IAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
