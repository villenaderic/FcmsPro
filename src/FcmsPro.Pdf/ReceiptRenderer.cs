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

    // --- Thermal (80mm roll) receipt ---------------------------------------

    private const double ThermalWidthPt = 227; // 80mm at 72pt/inch, minus small print margins
    private const double ThermalMargin = 10;
    private const double ThermalLineHeight = 13;

    public Task<byte[]> RenderThermalPdfAsync(Receipt receipt, AppSettings businessSettings, CancellationToken ct = default)
    {
        var contentWidth = ThermalWidthPt - ThermalMargin * 2;
        var font = new XFont("Courier New", 8, XFontStyle.Regular);
        var boldFont = new XFont("Courier New", 8, XFontStyle.Bold);
        var titleFont = new XFont("Courier New", 11, XFontStyle.Bold);
        var smallFont = new XFont("Courier New", 7, XFontStyle.Regular);

        // Thermal rolls are a continuous strip, not a fixed page - PdfSharpCore
        // still needs a concrete page height up front, so the line count is
        // worked out first (matching the exact sequence DrawThermal below
        // writes) and converted to points, plus top/bottom margin and a little
        // slack for word-wrapped long values (client/commission names).
        var lineCount = 8; // header block: business name + up to addr/contact + spacer + receipt#/date
        if (!string.IsNullOrWhiteSpace(businessSettings.Address)) lineCount++;
        var contactLine = BuildContactLine(businessSettings);
        if (!string.IsNullOrWhiteSpace(contactLine)) lineCount++;

        lineCount += WrappedLineCount("Billed To: " + receipt.ClientName, contentWidth, font);
        lineCount += 2; // separator + spacer
        lineCount += WrappedLineCount("For: " + receipt.CommissionTitle, contentWidth, font);
        if (!string.IsNullOrWhiteSpace(receipt.ServiceType))
            lineCount += WrappedLineCount("Type: " + receipt.ServiceType, contentWidth, font);
        lineCount += 3; // total price row + separator + spacer

        lineCount += 6; // down payment, previous payments, this payment, remaining, separator, spacer
        lineCount += WrappedLineCount("Method: " + receipt.PaymentMethod, contentWidth, font);
        if (!string.IsNullOrWhiteSpace(receipt.ReferenceNumber))
            lineCount += WrappedLineCount("Ref: " + receipt.ReferenceNumber, contentWidth, font);

        lineCount += 6; // separator, spacer, "verify below", code, spacer, footer thanks line

        var pageHeight = lineCount * ThermalLineHeight + ThermalMargin * 2 + 20;
        pageHeight *= 1.15; // safety margin: the line-count pre-pass estimates
                             // wrap points via average char width, while the
                             // actual draw pass below measures real glyph
                             // widths - this buffer absorbs that difference
                             // so long client/commission names can't get
                             // clipped off the bottom of the page.

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(ThermalWidthPt);
        page.Height = XUnit.FromPoint(pageHeight);

        using var gfx = XGraphics.FromPdfPage(page);
        double y = ThermalMargin;

        void Center(string text, XFont f)
        {
            gfx.DrawString(text, f, XBrushes.Black, new XRect(ThermalMargin, y, contentWidth, ThermalLineHeight), XStringFormats.TopCenter);
            y += ThermalLineHeight;
        }

        void Left(string text, XFont f)
        {
            foreach (var line in WrapText(text, contentWidth, f, gfx))
            {
                gfx.DrawString(line, f, XBrushes.Black, new XPoint(ThermalMargin, y));
                y += ThermalLineHeight;
            }
        }

        void LeftRight(string left, string right, XFont f)
        {
            gfx.DrawString(left, f, XBrushes.Black, new XPoint(ThermalMargin, y));
            gfx.DrawString(right, f, XBrushes.Black, new XRect(ThermalMargin, y, contentWidth, ThermalLineHeight), XStringFormats.TopRight);
            y += ThermalLineHeight;
        }

        void Dashes()
        {
            Center(new string('-', 32), font);
        }

        var name = businessSettings.BusinessName ?? businessSettings.FreelancerName ?? "Business Name";
        Center(name, titleFont);
        if (!string.IsNullOrWhiteSpace(businessSettings.Address)) Center(businessSettings.Address!, smallFont);
        if (!string.IsNullOrWhiteSpace(contactLine)) Center(contactLine!, smallFont);
        y += 4;

        Center($"RECEIPT #{receipt.ReceiptNumber}", boldFont);
        Center(receipt.Date.ToString("MMM d, yyyy"), font);
        Dashes();

        Left("Billed To: " + receipt.ClientName, font);
        Dashes();

        Left("For: " + receipt.CommissionTitle, font);
        if (!string.IsNullOrWhiteSpace(receipt.ServiceType)) Left("Type: " + receipt.ServiceType, font);
        LeftRight("Total Price", $"{businessSettings.CurrencySymbol}{receipt.CommissionPrice:N2}", boldFont);
        Dashes();

        LeftRight("Down Payment", $"{businessSettings.CurrencySymbol}{receipt.DownPayment:N2}", font);
        LeftRight("Prev. Payments", $"{businessSettings.CurrencySymbol}{receipt.PreviousPayments:N2}", font);
        LeftRight("This Payment", $"{businessSettings.CurrencySymbol}{receipt.AmountPaid:N2}", boldFont);
        LeftRight("Remaining", $"{businessSettings.CurrencySymbol}{receipt.RemainingBalance:N2}", font);
        Left("Method: " + receipt.PaymentMethod, font);
        if (!string.IsNullOrWhiteSpace(receipt.ReferenceNumber)) Left("Ref: " + receipt.ReferenceNumber, font);
        Dashes();

        Center("Verification Code", smallFont);
        Center(receipt.VerificationCode, boldFont);
        y += 4;
        Center("Thank you!", font);

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    private static string? BuildContactLine(AppSettings businessSettings) =>
        string.Join("  ", new[] { businessSettings.ContactNumber, businessSettings.Email }
            .Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } s ? s : null;

    /// <summary>Word-wraps text to fit maxWidth, matching XGraphics's actual measured string width for the given font (Courier New is monospace, but font metrics still vary by platform, so this measures rather than assumes a fixed char count).</summary>
    private static List<string> WrapText(string text, double maxWidth, XFont font, XGraphics gfx)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var current = "";
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (gfx.MeasureString(candidate, font).Width > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines.Count == 0 ? new List<string> { "" } : lines;
    }

    /// <summary>Estimates wrapped-line count before a real XGraphics context exists (for the page-height pre-pass), using a conservative average character width for 8pt Courier New rather than an actual measurement.</summary>
    private static int WrappedLineCount(string text, double maxWidth, XFont font)
    {
        const double approxCharWidthPt = 4.8; // 8pt Courier New monospace glyph width
        var charsPerLine = Math.Max(1, (int)(maxWidth / approxCharWidthPt));
        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
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
