using Avalonia.Media.Imaging;
using FcmsPro.Avalonia.ViewModels.Shared;
using FcmsPro.Core.Entities;

namespace FcmsPro.Avalonia.ViewModels.Quotes;

/// <summary>Pairs a QuoteAttachment with its loaded preview. See AttachmentPreviewLoader for the image-vs-document distinction.</summary>
public class QuoteAttachmentItemViewModel
{
    public QuoteAttachment Attachment { get; }
    public Bitmap? Image { get; }
    public bool IsImage { get; }
    public bool LoadFailed => IsImage && Image is null;

    public QuoteAttachmentItemViewModel(QuoteAttachment attachment, string filePath)
    {
        Attachment = attachment;
        (IsImage, Image) = AttachmentPreviewLoader.Load(attachment.ContentType, filePath);
    }
}
