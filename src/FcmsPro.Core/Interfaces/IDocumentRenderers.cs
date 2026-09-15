using FcmsPro.Core.Entities;
using FcmsPro.Core.Metrics;

namespace FcmsPro.Core.Interfaces;

/// <summary>
/// Implemented in FcmsPro.Pdf (PdfSharpCore). Kept as an interface here so
/// FcmsPro.Avalonia depends only on abstractions, and the PDF library choice
/// can change later without touching ViewModels.
/// </summary>
public interface IReceiptRenderer
{
    Task<byte[]> RenderPdfAsync(Receipt receipt, AppSettings businessSettings, CancellationToken ct = default);

    /// <summary>
    /// Renders a narrow-column receipt sized for 80mm thermal roll paper
    /// (the standard width for USB/Bluetooth receipt printers - the other
    /// common size, 58mm, prints fine on 80mm-formatted output too since
    /// most 58mm printer drivers just center/crop it, but a dedicated 58mm
    /// layout can be added later if a specific printer needs it). Unlike
    /// the A5 receipt this targets a single continuous roll, not a fixed
    /// page, so the page height is computed from content instead of fixed.
    /// </summary>
    Task<byte[]> RenderThermalPdfAsync(Receipt receipt, AppSettings businessSettings, CancellationToken ct = default);
}

public interface IInvoiceRenderer
{
    Task<byte[]> RenderPdfAsync(Invoice invoice, Client client, AppSettings businessSettings, CancellationToken ct = default);
}

public interface IQuoteRenderer
{
    Task<byte[]> RenderPdfAsync(Quote quote, Client client, AppSettings businessSettings, CancellationToken ct = default);
}

public interface ITaxSummaryRenderer
{
    Task<byte[]> RenderPdfAsync(TaxSummary summary, AppSettings businessSettings, CancellationToken ct = default);
}
