using Avalonia.Media.Imaging;
using FcmsPro.Core.Entities;

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// Pairs a CommissionAttachment with its loaded image, so the view can bind
/// an &lt;Image Source="{Binding Image}"&gt; directly. Uses the plain
/// Bitmap(Stream) constructor rather than Bitmap.DecodeToWidth (a
/// lower-level, more error-prone API to get right without a compiler on
/// hand to verify against) - full-resolution decode is an acceptable
/// tradeoff here since AttachmentService already caps files at 10MB and a
/// commission typically has a handful of attachments, not hundreds.
/// </summary>
public class AttachmentItemViewModel
{
    public CommissionAttachment Attachment { get; }
    public Bitmap? Image { get; }
    public bool LoadFailed => Image is null;

    public AttachmentItemViewModel(CommissionAttachment attachment, string filePath)
    {
        Attachment = attachment;
        try
        {
            using var stream = System.IO.File.OpenRead(filePath);
            Image = new Bitmap(stream);
        }
        catch
        {
            // File missing/corrupt/not a decodable image - LoadFailed drives
            // the view to show a placeholder instead of a broken Image
            // control, rather than this constructor (or the caller building
            // the collection) throwing and losing every other attachment
            // in the same LoadAsync pass.
            Image = null;
        }
    }
}
