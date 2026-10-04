using FcmsPro.Core.Entities;
using FcmsPro.Core.Metrics;

namespace FcmsPro.Core.Interfaces;

/// <summary>
/// Implemented in FcmsPro.Pdf (PdfSharpCore). Kept as an interface here so
/// FcmsPro.Avalonia depends only on abstractions, and the PDF library choice
/// can change later without touching ViewModels.
/// </summary>

public interface ITaxSummaryRenderer
{
    Task<byte[]> RenderPdfAsync(TaxSummary summary, AppSettings businessSettings, CancellationToken ct = default);
}
