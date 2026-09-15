namespace FcmsPro.Core.Entities;

/// <summary>
/// Reference/finished-piece image attached to a commission - lets/logs, e.g.
/// a client's reference photo or the delivered artwork, live alongside the
/// commission record instead of scattered across chat apps and folders.
/// Soft-referenced to Commission by Id only, matching the rest of the app's
/// convention (see Payment.CommissionId) - no EF navigation property or FK
/// constraint.
///
/// The actual image bytes are NOT stored in the database - only this
/// metadata row is. The file itself lives on disk under
/// FcmsPaths.GetAttachmentsDirectory(commissionId)/{StoredFileName}, named
/// by a fresh Guid rather than the original filename to avoid collisions,
/// path-unsafe characters, and duplicate-name overwrites.
/// </summary>
public class CommissionAttachment : IAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommissionId { get; set; }

    /// <summary>Original filename as picked by the user - display only, never used as a path.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Actual on-disk filename (a Guid + original extension) under the commission's attachments folder.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
