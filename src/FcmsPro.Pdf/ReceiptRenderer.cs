using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FcmsPro.Pdf;

/// <summary>
/// Vector PDF rendering via PdfSharpCore, replacing the PWA's canvas-draw ->
/// rasterize -> embed-in-PDF pipeline (Phase 1 audit §2.4). This is a fresh
/// layout, not a literal port of the canvas drawing code - it targets the same
/// A5 page size and the same information (business header, client info, line
/// items, verification code) but is redrawn with real vector text/shapes so it
/// stays sharp at any zoom/print DPI.
///
/// Layout constants are placeholders for a first pass - a real visual design
/// pass (matching FcmsPro.Avalonia's theme/branding) is still needed before
/// this ships; flagged in Phase 2 §2 as a follow-up, not a blocker for scaffolding.
/// </summary>
public class ReceiptRenderer : IReceiptRenderer
{
    private const double PageWidthPt = 420;  // A5 portrait width in points
    private const double PageHeightPt = 595; // A5 portrait height in points

    public Task<byte[]> RenderPdfAsync(Receipt receipt, AppSettings businessSettings, CancellationToken ct = default)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidthPt);
        page.Height = XUnit.FromPoint(PageHeightPt);

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Regular);
        var labelFont = new XFont("Arial", 9, XFontStyle.Bold);
        var valueFont = new XFont("Arial", 9, XFontStyle.Regular);
        var monoFont = new XFont("Courier New", 11, XFontStyle.Bold);

        double y = 36;
        double margin = 32;
        double contentWidth = PageWidthPt - margin * 2;

        // Business header
        gfx.DrawString(businessSettings.BusinessName ?? businessSettings.FreelancerName ?? "Business Name",
            titleFont, XBrushes.Black, new XPoint(margin, y));
        y += 22;

        if (!string.IsNullOrWhiteSpace(businessSettings.Address))
        {
            gfx.DrawString(businessSettings.Address, headerFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 14;
        }

        var contactLine = string.Join("  \u00b7  ", new[]
        {
            businessSettings.ContactNumber, businessSettings.Email
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(contactLine))
        {
            gfx.DrawString(contactLine, headerFont, XBrushes.DimGray, new XPoint(margin, y));
            y += 20;
        }

        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 20;

        // Receipt number + date
        gfx.DrawString($"RECEIPT #{receipt.ReceiptNumber}", new XFont("Arial", 13, XFontStyle.Bold),
            XBrushes.Black, new XPoint(margin, y));
        gfx.DrawString(receipt.Date.ToString("MMM d, yyyy"), valueFont, XBrushes.DimGray,
            new XRect(margin, y - 12, contentWidth, 20), XStringFormats.TopRight);
        y += 26;

        // Client info
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Billed To", receipt.ClientName);
        if (!string.IsNullOrWhiteSpace(receipt.ClientPhone))
            DrawRow(gfx, labelFont, valueFont, margin, ref y, "Phone", receipt.ClientPhone!);
        if (!string.IsNullOrWhiteSpace(receipt.ClientEmail))
            DrawRow(gfx, labelFont, valueFont, margin, ref y, "Email", receipt.ClientEmail!);

        y += 10;
        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 16;

        // Commission / line item
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "For", receipt.CommissionTitle);
        if (!string.IsNullOrWhiteSpace(receipt.ServiceType))
            DrawRow(gfx, labelFont, valueFont, margin, ref y, "Service Type", receipt.ServiceType!);
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Total Price",
            $"{businessSettings.CurrencySymbol}{receipt.CommissionPrice:N2}");

        y += 10;
        gfx.DrawLine(XPens.LightGray, margin, y, PageWidthPt - margin, y);
        y += 16;

        // Payment breakdown
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Down Payment",
            $"{businessSettings.CurrencySymbol}{receipt.DownPayment:N2}");
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Previous Payments",
            $"{businessSettings.CurrencySymbol}{receipt.PreviousPayments:N2}");
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "This Payment",
            $"{businessSettings.CurrencySymbol}{receipt.AmountPaid:N2}", emphasize: true);
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Remaining Balance",
            $"{businessSettings.CurrencySymbol}{receipt.RemainingBalance:N2}");
        DrawRow(gfx, labelFont, valueFont, margin, ref y, "Payment Method", receipt.PaymentMethod);
        if (!string.IsNullOrWhiteSpace(receipt.ReferenceNumber))
            DrawRow(gfx, labelFont, valueFont, margin, ref y, "Reference #", receipt.ReferenceNumber!);

        // Verification code, bottom of page
        var codeY = PageHeightPt - 70;
        gfx.DrawLine(XPens.LightGray, margin, codeY - 10, PageWidthPt - margin, codeY - 10);
        gfx.DrawString("Verification Code", labelFont, XBrushes.DimGray, new XPoint(margin, codeY + 8));
        gfx.DrawString(receipt.VerificationCode, monoFont, XBrushes.Black, new XPoint(margin, codeY + 26));

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    private static void DrawRow(XGraphics gfx, XFont labelFont, XFont valueFont, double margin, ref double y,
        string label, string value, bool emphasize = false)
    {
        gfx.DrawString(label, labelFont, XBrushes.DimGray, new XPoint(margin, y));
        gfx.DrawString(value, emphasize ? new XFont("Arial", 9, XFontStyle.Bold) : valueFont,
            XBrushes.Black, new XRect(margin, y - 11, PageWidthPt - margin * 2, 16), XStringFormats.TopRight);
        y += 16;
    }
}
