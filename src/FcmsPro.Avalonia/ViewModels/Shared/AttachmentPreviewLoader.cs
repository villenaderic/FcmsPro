using Avalonia.Media.Imaging;

namespace FcmsPro.Avalonia.ViewModels.Shared;

/// <summary>
/// Generalizes the image-loading half of Commissions/AttachmentItemViewModel
/// so ClientAttachmentItemViewModel/InvoiceAttachmentItemViewModel/
/// QuoteAttachmentItemViewModel don't each reimplement it. Unlike the
/// Commission version (which only ever deals with images), attachments on
/// these entities can also be PDFs (e.g. a signed contract) - IsImage lets
/// the view show a document icon instead of attempting a doomed Bitmap
/// decode on a PDF and falling into "Couldn't load image".
/// </summary>
public static class AttachmentPreviewLoader
{
    public static (bool isImage, Bitmap? image) Load(string contentType, string filePath)
    {
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        if (!isImage) return (false, null);

        try
        {
            using var stream = File.OpenRead(filePath);
            return (true, new Bitmap(stream));
        }
        catch
        {
            // File missing/corrupt/not a decodable image - the view falls
            // back to a placeholder instead of a broken Image control.
            return (true, null);
        }
    }
}
