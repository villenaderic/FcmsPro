namespace FcmsPro.Core.Entities;

/// <summary>
/// Common shape of every attachment entity (CommissionAttachment plus the
/// generalized ClientAttachment/InvoiceAttachment/QuoteAttachment added
/// alongside this interface). Lets AttachmentService's file-copy/validation
/// logic be written once generically instead of copy-pasted per entity type -
/// each concrete attachment entity still gets its own table and its own
/// FooId foreign key property (not part of this interface, since that key
/// name differs per entity), matching the rest of the app's one-table-per-
/// concept convention rather than a single polymorphic Attachments table.
/// </summary>
public interface IAttachment
{
    Guid Id { get; set; }

    /// <summary>Original filename as picked by the user - display only, never used as a path.</summary>
    string FileName { get; set; }

    /// <summary>Actual on-disk filename (a Guid + original extension) under the owning entity's attachments folder.</summary>
    string StoredFileName { get; set; }

    string ContentType { get; set; }
    long FileSizeBytes { get; set; }
    string? Caption { get; set; }
    DateTimeOffset UploadedAt { get; set; }
}
