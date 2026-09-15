using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FcmsPro.Pdf;

/// <summary>
/// Full-page (Letter) invoice PDF, following the same PdfSharpCore pattern
/// established in ReceiptRenderer - replaces the PWA's window.open +
/// inline-styled HTML document approach (Phase 1 audit §2.5).
/// </summary>
public class InvoiceRenderer : IInvoiceRenderer
{
    private const double PageWidthPt = 612;  // US Letter width in points
    private const double PageHeightPt = 792; // US Letter height in points

    public Task<byte[]> RenderPdfAsync(Invoice invoice, Client client, AppSettings businessSettings, CancellationToken ct = default)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidthPt);
        page.Height = XUnit.FromPoint(PageHeightPt);

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 22, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Regular);
        var labelFont = new XFont("Arial", 10, XFontStyle.Bold);
        var valueFont = new XFont("Arial", 10, XFontStyle.Regular);
        var totalFont = new XFont("Arial", 14, XFontStyle.Bold);

        double margin = 54; // 0.75"
        double contentWidth = PageWidthPt - margin * 2;
        double y = 54;

        // Business header (left) + "INVOICE" + number (right)
        gfx.DrawString(businessSettings.BusinessName ?? businessSettings.FreelancerName ?? "Business Name",
            new XFont("Arial", 14, XFontStyle.Bold), XBrushes.Black, new XPoint(margin, y));
        gfx.DrawString("INVOICE", titleFont, XBrushes.Black,
            new XRect(margin, y - 10, contentWidth, 30), XStringFormats.TopRight);
        y += 20;

        if (!string.IsNullOrWhiteSpace(businessSettings.Address))
        {
            gfx.DrawString(businessSettings.Address, headerFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 14;
        }
        var contactLine = string.Join("  \u00b7  ", new[] { businessSettings.ContactNumber, businessSettings.Email }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(contactLine))
        {
            gfx.DrawString(contactLine, headerFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 14;
        }

        gfx.DrawString(invoice.InvoiceNumber, valueFont, XBrushes.DimGray,
            new XRect(margin, 54 + 30, contentWidth, 16), XStringFormats.TopRight);

        y += 20;
        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 24;

        // Billed-to (left) / dates (right)
        gfx.DrawString("BILLED TO", labelFont, XBrushes.DimGray, new XPoint(margin, y));
        gfx.DrawString("ISSUE DATE", labelFont, XBrushes.DimGray, new XRect(margin, y, contentWidth * 0.5, 14), XStringFormats.TopRight);
        y += 16;
        gfx.DrawString(client.Name, valueFont, XBrushes.Black, new XPoint(margin, y));
        gfx.DrawString(invoice.IssueDate.ToString("MMM d, yyyy"), valueFont, XBrushes.Black,
            new XRect(margin, y, contentWidth * 0.5, 14), XStringFormats.TopRight);
        y += 16;

        if (!string.IsNullOrWhiteSpace(client.Email))
        {
            gfx.DrawString(client.Email, valueFont, XBrushes.DimGray, new XPoint(margin, y));
        }
        gfx.DrawString("DUE DATE", labelFont, XBrushes.DimGray, new XRect(margin, y, contentWidth * 0.5, 14), XStringFormats.TopRight);
        y += 16;
        gfx.DrawString(invoice.DueDate.ToString("MMM d, yyyy"), valueFont, XBrushes.Black,
            new XRect(margin, y, contentWidth * 0.5, 14), XStringFormats.TopRight);
        y += 30;

        if (!string.IsNullOrWhiteSpace(invoice.PoNumber))
        {
            gfx.DrawString($"PO #: {invoice.PoNumber}", valueFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 20;
        }

        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 20;

        // Line item (single line for this scaffold - a real multi-line-item
        // model is a schema addition beyond what Phase 1/2 specified; the
        // PWA itself only has one free-text Description per invoice)
        gfx.DrawString("DESCRIPTION", labelFont, XBrushes.DimGray, new XPoint(margin, y));
        gfx.DrawString("AMOUNT", labelFont, XBrushes.DimGray, new XRect(margin, y, contentWidth, 14), XStringFormats.TopRight);
        y += 18;
        gfx.DrawString(invoice.Description, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth * 0.7, 60),
            XStringFormats.TopLeft);
        gfx.DrawString($"{businessSettings.CurrencySymbol}{invoice.Subtotal:N2}", valueFont, XBrushes.Black,
            new XRect(margin, y, contentWidth, 16), XStringFormats.TopRight);
        y += 50;

        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 16;

        // Totals block, right-aligned
        DrawTotalRow(gfx, labelFont, valueFont, margin, contentWidth, ref y, "Subtotal",
            $"{businessSettings.CurrencySymbol}{invoice.Subtotal:N2}");
        if (invoice.Discount > 0)
            DrawTotalRow(gfx, labelFont, valueFont, margin, contentWidth, ref y, "Discount",
                $"-{businessSettings.CurrencySymbol}{invoice.Discount:N2}");
        if (invoice.Tax > 0)
            DrawTotalRow(gfx, labelFont, valueFont, margin, contentWidth, ref y, "Tax",
                $"{businessSettings.CurrencySymbol}{invoice.Tax:N2}");

        y += 6;
        gfx.DrawLine(XPens.Black, margin + contentWidth * 0.5, y, PageWidthPt - margin, y);
        y += 10;

        gfx.DrawString("TOTAL", totalFont, XBrushes.Black, new XRect(margin, y, contentWidth * 0.75, 22), XStringFormats.TopRight);
        gfx.DrawString($"{businessSettings.CurrencySymbol}{invoice.Total:N2}", totalFont, XBrushes.Black,
            new XRect(margin, y, contentWidth, 22), XStringFormats.TopRight);
        y += 40;

        if (!string.IsNullOrWhiteSpace(invoice.Terms))
        {
            gfx.DrawString("TERMS", labelFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 16;
            gfx.DrawString(invoice.Terms, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth, 60), XStringFormats.TopLeft);
            y += 50;
        }

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            gfx.DrawString("NOTES", labelFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 16;
            gfx.DrawString(invoice.Notes, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth, 60), XStringFormats.TopLeft);
        }

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    private static void DrawTotalRow(XGraphics gfx, XFont labelFont, XFont valueFont, double margin, double contentWidth,
        ref double y, string label, string value)
    {
        gfx.DrawString(label, labelFont, XBrushes.DimGray, new XRect(margin, y, contentWidth * 0.75, 16), XStringFormats.TopRight);
        gfx.DrawString(value, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth, 16), XStringFormats.TopRight);
        y += 18;
    }
}

/// <summary>
/// Full-page (Letter) quote/proposal PDF, following the same PdfSharpCore
/// pattern as ReceiptRenderer/InvoiceRenderer.
/// </summary>
public class QuoteRenderer : IQuoteRenderer
{
    private const double PageWidthPt = 612;
    private const double PageHeightPt = 792;

    public Task<byte[]> RenderPdfAsync(Quote quote, Client client, AppSettings businessSettings, CancellationToken ct = default)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidthPt);
        page.Height = XUnit.FromPoint(PageHeightPt);

        using var gfx = XGraphics.FromPdfPage(page);
        var headerFont = new XFont("Arial", 10, XFontStyle.Regular);
        var labelFont = new XFont("Arial", 10, XFontStyle.Bold);
        var valueFont = new XFont("Arial", 10, XFontStyle.Regular);
        var totalFont = new XFont("Arial", 14, XFontStyle.Bold);

        double margin = 54;
        double contentWidth = PageWidthPt - margin * 2;
        double y = 54;

        gfx.DrawString(businessSettings.BusinessName ?? businessSettings.FreelancerName ?? "Business Name",
            new XFont("Arial", 14, XFontStyle.Bold), XBrushes.Black, new XPoint(margin, y));
        gfx.DrawString("PROPOSAL", new XFont("Arial", 22, XFontStyle.Bold), XBrushes.Black,
            new XRect(margin, y - 10, contentWidth, 30), XStringFormats.TopRight);
        y += 20;

        if (!string.IsNullOrWhiteSpace(businessSettings.Address))
        {
            gfx.DrawString(businessSettings.Address, headerFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 14;
        }

        gfx.DrawString(quote.QuoteNumber, valueFont, XBrushes.DimGray,
            new XRect(margin, 54 + 30, contentWidth, 16), XStringFormats.TopRight);

        y += 20;
        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 24;

        gfx.DrawString("PREPARED FOR", labelFont, XBrushes.DimGray, new XPoint(margin, y));
        gfx.DrawString("VALID UNTIL", labelFont, XBrushes.DimGray, new XRect(margin, y, contentWidth, 14), XStringFormats.TopRight);
        y += 16;
        gfx.DrawString(client.Name, valueFont, XBrushes.Black, new XPoint(margin, y));
        gfx.DrawString(quote.ValidUntil.ToString("MMM d, yyyy"), valueFont, XBrushes.Black,
            new XRect(margin, y, contentWidth, 14), XStringFormats.TopRight);
        y += 30;

        if (!string.IsNullOrWhiteSpace(quote.ServiceType))
        {
            gfx.DrawString("SERVICE", labelFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 16;
            gfx.DrawString(quote.ServiceType, valueFont, XBrushes.Black, new XPoint(margin, y));
            y += 24;
        }

        gfx.DrawString("SCOPE OF WORK", labelFont, XBrushes.DimGray, new XPoint(margin, y));
        y += 16;
        gfx.DrawString(quote.Scope, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth, 100), XStringFormats.TopLeft);
        y += 90;

        gfx.DrawString($"Revisions included: {quote.Revisions}", valueFont, XBrushes.DimGray, new XPoint(margin, y));
        y += 24;

        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 20;

        gfx.DrawString("TOTAL", totalFont, XBrushes.Black, new XRect(margin, y, contentWidth * 0.75, 22), XStringFormats.TopRight);
        gfx.DrawString($"{businessSettings.CurrencySymbol}{quote.Total:N2}", totalFont, XBrushes.Black,
            new XRect(margin, y, contentWidth, 22), XStringFormats.TopRight);
        y += 26;

        if (quote.DownPayment is > 0)
        {
            gfx.DrawString($"Down payment to begin: {businessSettings.CurrencySymbol}{quote.DownPayment:N2}",
                valueFont, XBrushes.DimGray, new XRect(margin, y, contentWidth, 16), XStringFormats.TopRight);
            y += 30;
        }

        if (!string.IsNullOrWhiteSpace(quote.Terms))
        {
            gfx.DrawString("TERMS", labelFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 16;
            gfx.DrawString(quote.Terms, valueFont, XBrushes.Black, new XRect(margin, y, contentWidth, 80), XStringFormats.TopLeft);
        }

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }
}
