using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Metrics;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FcmsPro.Pdf;

/// <summary>
/// Full-page (Letter) year-end/quarterly summary, same PdfSharpCore pattern
/// as InvoiceRenderer/QuoteRenderer. Not a specific jurisdiction's tax form
/// - just the raw income/expense totals and category breakdown an
/// accountant or tax software would actually want, computed by
/// TaxSummaryService from data the user is already entering for other
/// reasons (Payments, Expenses).
/// </summary>
public class TaxSummaryRenderer : ITaxSummaryRenderer
{
    private const double PageWidthPt = 612;
    private const double PageHeightPt = 792;

    public Task<byte[]> RenderPdfAsync(TaxSummary summary, AppSettings businessSettings, CancellationToken ct = default)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidthPt);
        page.Height = XUnit.FromPoint(PageHeightPt);

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 22, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Regular);
        var sectionFont = new XFont("Arial", 13, XFontStyle.Bold);
        var labelFont = new XFont("Arial", 10, XFontStyle.Bold);
        var valueFont = new XFont("Arial", 10, XFontStyle.Regular);
        var totalFont = new XFont("Arial", 14, XFontStyle.Bold);

        double margin = 54;
        double contentWidth = PageWidthPt - margin * 2;
        double y = margin;

        var name = businessSettings.BusinessName ?? businessSettings.FreelancerName ?? "";
        gfx.DrawString(name, headerFont, XBrushes.Black, new XPoint(margin, y));
        y += 16;
        gfx.DrawString($"Income & Expense Summary - {summary.Year}", titleFont, XBrushes.Black, new XPoint(margin, y));
        y += 40;

        // --- Year totals -----------------------------------------------
        void Row(string label, decimal amount, XFont font, bool rule = false)
        {
            gfx.DrawString(label, font, XBrushes.Black, new XPoint(margin, y));
            gfx.DrawString($"{businessSettings.CurrencySymbol}{amount:N2}", font, XBrushes.Black,
                new XRect(margin, y - 12, contentWidth, 16), XStringFormats.TopRight);
            y += 20;
            if (rule)
            {
                gfx.DrawLine(XPens.LightGray, margin, y - 8, margin + contentWidth, y - 8);
                y += 4;
            }
        }

        Row("Total Income", summary.TotalIncome, valueFont);
        Row("Total Expenses", summary.TotalExpenses, valueFont, rule: true);
        Row("Net Profit", summary.NetProfit, totalFont);
        y += 24;

        // --- Quarterly breakdown -----------------------------------------
        gfx.DrawString("By Quarter", sectionFont, XBrushes.Black, new XPoint(margin, y));
        y += 22;

        double colQuarter = margin, colIncome = margin + 130, colExpenses = margin + 280, colNet = margin + 430;
        gfx.DrawString("Quarter", labelFont, XBrushes.Black, new XPoint(colQuarter, y));
        gfx.DrawString("Income", labelFont, XBrushes.Black, new XPoint(colIncome, y));
        gfx.DrawString("Expenses", labelFont, XBrushes.Black, new XPoint(colExpenses, y));
        gfx.DrawString("Net", labelFont, XBrushes.Black, new XPoint(colNet, y));
        y += 16;
        gfx.DrawLine(XPens.Black, margin, y, margin + contentWidth, y);
        y += 10;

        foreach (var q in summary.Quarters)
        {
            gfx.DrawString($"Q{q.Quarter}", valueFont, XBrushes.Black, new XPoint(colQuarter, y));
            gfx.DrawString($"{businessSettings.CurrencySymbol}{q.Income:N2}", valueFont, XBrushes.Black, new XPoint(colIncome, y));
            gfx.DrawString($"{businessSettings.CurrencySymbol}{q.Expenses:N2}", valueFont, XBrushes.Black, new XPoint(colExpenses, y));
            gfx.DrawString($"{businessSettings.CurrencySymbol}{q.Net:N2}", valueFont, XBrushes.Black, new XPoint(colNet, y));
            y += 20;
        }

        y += 24;

        // --- Expenses by category -----------------------------------------
        gfx.DrawString("Expenses by Category", sectionFont, XBrushes.Black, new XPoint(margin, y));
        y += 22;

        if (summary.ExpensesByCategory.Count == 0)
        {
            gfx.DrawString("No expenses recorded this year.", valueFont, XBrushes.Gray, new XPoint(margin, y));
            y += 20;
        }
        else
        {
            foreach (var c in summary.ExpensesByCategory)
            {
                gfx.DrawString(SplitPascalCase(c.Category.ToString()), valueFont, XBrushes.Black, new XPoint(margin, y));
                gfx.DrawString($"{businessSettings.CurrencySymbol}{c.Total:N2}", valueFont, XBrushes.Black,
                    new XRect(margin, y - 12, contentWidth, 16), XStringFormats.TopRight);
                y += 20;
            }
        }

        y += 16;
        var footerFont = new XFont("Arial", 8, XFontStyle.Italic);
        gfx.DrawString(
            "This summary is generated from the payments and expenses recorded in FCMS Pro and is not a substitute for professional tax advice.",
            footerFont, XBrushes.Gray, new XRect(margin, y, contentWidth, 20), XStringFormats.TopLeft);

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    /// <summary>"SoftwareTools" -&gt; "Software Tools" - ExpenseCategory enum names are PascalCase with no spaces.</summary>
    private static string SplitPascalCase(string value)
    {
        var chars = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]))
                chars.Append(' ');
            chars.Append(value[i]);
        }
        return chars.ToString();
    }
}
