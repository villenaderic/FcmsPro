using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Services;

/// <summary>
/// Handles both halves of an attachment (Commission/Client/Invoice/Quote):
/// the metadata row (via IUnitOfWork) and the actual file copy on disk.
/// Originally Commission-only; generalized via IAttachment so the shared
/// validation/copy logic lives in one place while each entity still gets
/// its own table (matches the rest of the app's one-table-per-concept
/// convention rather than a single polymorphic Attachments table).
/// Like BackupService, this project (FcmsPro.Core) has zero project
/// references by design, so it never resolves FcmsPaths itself - the caller
/// resolves the target directory (FcmsPaths.GetXAttachmentsDirectory(id))
/// and passes it in, keeping this project persistence/filesystem-location-agnostic.
/// </summary>
public class AttachmentService
{
    private readonly IUnitOfWork _uow;

    public AttachmentService(IUnitOfWork uow) => _uow = uow;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".pdf" };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        // .pdf added when this service was generalized beyond Commission -
        // a signed contract (Client) or reference document (Invoice/Quote)
        // is exactly the kind of attachment those entities actually need,
        // and there's no reason to withhold it from Commission attachments
        // either (a reference sheet PDF is just as legitimate there).
        [".pdf"] = "application/pdf"
    };

    /// <summary>10 MB - generous for a reference photo, delivered artwork export, or a scanned contract, while still ruling out someone accidentally picking a huge raw file.</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public class AttachmentValidationException : Exception
    {
        public AttachmentValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Shared core of every AddFor*Async overload below: validates
    /// extension/size, copies the file to targetDirectory under a fresh
    /// Guid-based name, fills in the metadata fields, persists via the given
    /// repository, and saves. `attachment` should already have its owning
    /// FooId (and optional Caption) set - this method only touches the
    /// IAttachment-shared fields.
    /// </summary>
    private async Task<TAttachment> AddCoreAsync<TAttachment>(
        IRepository<TAttachment> repository, TAttachment attachment,
        string sourceFilePath, string targetDirectory, CancellationToken ct)
        where TAttachment : class, IAttachment
    {
        var originalName = Path.GetFileName(sourceFilePath);
        var extension = Path.GetExtension(sourceFilePath);

        if (!AllowedExtensions.Contains(extension))
            throw new AttachmentValidationException(
                $"'{extension}' isn't a supported file type. Use PNG, JPG, WEBP, GIF, BMP, or PDF.");

        var fileInfo = new FileInfo(sourceFilePath);
        if (!fileInfo.Exists)
            throw new AttachmentValidationException("That file no longer exists.");
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new AttachmentValidationException(
                $"'{originalName}' is too large ({fileInfo.Length / 1024 / 1024} MB) - the limit is 10 MB.");

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var targetPath = Path.Combine(targetDirectory, storedFileName);

        Directory.CreateDirectory(targetDirectory);
        using (var source = File.OpenRead(sourceFilePath))
        using (var dest = File.Create(targetPath))
            await source.CopyToAsync(dest, ct);

        attachment.FileName = originalName;
        attachment.StoredFileName = storedFileName;
        attachment.ContentType = ContentTypeByExtension.GetValueOrDefault(extension, "application/octet-stream");
        attachment.FileSizeBytes = fileInfo.Length;

        await repository.AddAsync(attachment, ct);
        await _uow.SaveChangesAsync(ct);

        return attachment;
    }

    /// <summary>Deletes both the metadata row and the on-disk file. Missing file is not an error - the metadata row is what actually matters for the user-visible state.</summary>
    private async Task RemoveCoreAsync<TAttachment>(
        IRepository<TAttachment> repository, TAttachment attachment, string directory, CancellationToken ct)
        where TAttachment : class, IAttachment
    {
        repository.Remove(attachment);
        await _uow.SaveChangesAsync(ct);

        try
        {
            var path = Path.Combine(directory, attachment.StoredFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // The DB row (the source of truth for "does this attachment
            // exist") is already gone - a stray orphaned file on disk isn't
            // worth failing this operation over.
        }
    }

    public Task<CommissionAttachment> AddForCommissionAsync(
        Guid commissionId, string sourceFilePath, string targetDirectory, string? caption = null, CancellationToken ct = default) =>
        AddCoreAsync(_uow.CommissionAttachments,
            new CommissionAttachment { CommissionId = commissionId, Caption = caption },
            sourceFilePath, targetDirectory, ct);

    public Task RemoveAsync(CommissionAttachment attachment, string directory, CancellationToken ct = default) =>
        RemoveCoreAsync(_uow.CommissionAttachments, attachment, directory, ct);

    public Task<ClientAttachment> AddForClientAsync(
        Guid clientId, string sourceFilePath, string targetDirectory, string? caption = null, CancellationToken ct = default) =>
        AddCoreAsync(_uow.ClientAttachments,
            new ClientAttachment { ClientId = clientId, Caption = caption },
            sourceFilePath, targetDirectory, ct);

    public Task RemoveAsync(ClientAttachment attachment, string directory, CancellationToken ct = default) =>
        RemoveCoreAsync(_uow.ClientAttachments, attachment, directory, ct);

    public Task<InvoiceAttachment> AddForInvoiceAsync(
        Guid invoiceId, string sourceFilePath, string targetDirectory, string? caption = null, CancellationToken ct = default) =>
        AddCoreAsync(_uow.InvoiceAttachments,
            new InvoiceAttachment { InvoiceId = invoiceId, Caption = caption },
            sourceFilePath, targetDirectory, ct);

    public Task RemoveAsync(InvoiceAttachment attachment, string directory, CancellationToken ct = default) =>
        RemoveCoreAsync(_uow.InvoiceAttachments, attachment, directory, ct);

    public Task<QuoteAttachment> AddForQuoteAsync(
        Guid quoteId, string sourceFilePath, string targetDirectory, string? caption = null, CancellationToken ct = default) =>
        AddCoreAsync(_uow.QuoteAttachments,
            new QuoteAttachment { QuoteId = quoteId, Caption = caption },
            sourceFilePath, targetDirectory, ct);

    public Task RemoveAsync(QuoteAttachment attachment, string directory, CancellationToken ct = default) =>
        RemoveCoreAsync(_uow.QuoteAttachments, attachment, directory, ct);
}
