using Avalonia.Media.Imaging;
using FcmsPro.Avalonia.ViewModels.Shared;
using FcmsPro.Core.Entities;

namespace FcmsPro.Avalonia.ViewModels.Invoices;

/// <summary>Pairs an InvoiceAttachment with its loaded preview. See AttachmentPreviewLoader for the image-vs-document distinction.</summary>
public class InvoiceAttachmentItemViewModel
{
    public InvoiceAttachment Attachment { get; }
    public Bitmap? Image { get; }
    public bool IsImage { get; }
    public bool LoadFailed => IsImage && Image is null;

    public InvoiceAttachmentItemViewModel(InvoiceAttachment attachment, string filePath)
    {
        Attachment = attachment;
        (IsImage, Image) = AttachmentPreviewLoader.Load(attachment.ContentType, filePath);
    }
}
