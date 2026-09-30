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
