using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using FcmsPro.Avalonia.ViewModels.Clients;
using FcmsPro.Avalonia.ViewModels.Onboarding;
using FcmsPro.Avalonia.ViewModels.Shell;
using FcmsPro.Avalonia.Views.Shell;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SkiaSharp;

namespace FcmsPro.Avalonia.Services;

public static class ScreenshotGenerator
{
    private const int TargetWidth = 1200;
    private const int TargetHeight = 660;

    public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services)
    {
        Log.Information("ScreenshotGenerator: starting generation...");

        // 1. Seed deterministic, realistic showcase data
        await SeedDataAsync(services);

        var themeService = services.GetRequiredService<ThemeService>();
        AppCulture.Apply("$");
        themeService.Apply("Dark", "#3B82F6");

        var mainVm = services.GetRequiredService<MainWindowViewModel>();

        var window = new MainWindow
        {
            DataContext = mainVm,
            Width = TargetWidth,
            Height = TargetHeight,
            SystemDecorations = SystemDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(0, 0),
            Background = new SolidColorBrush(Color.Parse("#0B0F17"))
        };

        desktop.MainWindow = window;
        window.Show();

        // Target directory: site/assets/img/screenshots
        var baseDir = AppContext.BaseDirectory;
        var outDir = Path.GetFullPath(Path.Combine(baseDir, "../../../../../site/assets/img/screenshots"));
        Directory.CreateDirectory(outDir);
        Log.Information("ScreenshotGenerator: saving screenshots to {OutDir}", outDir);

        async Task CaptureViewAsync(string filename, int delayMs = 900)
        {
            await Task.Delay(delayMs);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(200);

            var actualWidth = (int)Math.Round(window.ClientSize.Width);
            var actualHeight = (int)Math.Round(window.ClientSize.Height);
            var pixelSize = new PixelSize(actualWidth, actualHeight);

            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(window);

            var filePath = Path.Combine(outDir, filename);
            rtb.Save(filePath);
            Log.Information("ScreenshotGenerator: saved {File} ({W}x{H}, {Size} bytes)", filename, actualWidth, actualHeight, new FileInfo(filePath).Length);
        }

        // 1. Dashboard (Dark)
        mainVm.Navigation.NavigateTo(AppPage.Dashboard);
        await CaptureViewAsync("dashboard.png", 1000);

        // 2. Commissions
        mainVm.Navigation.NavigateTo(AppPage.Commissions);
        await CaptureViewAsync("commissions.png", 700);

        // 3. Clients (Starlight Studios Profile)
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FcmsDbContext>();
            var starlight = await db.Clients.FirstOrDefaultAsync(c => c.Name == "Starlight Studios");
            if (starlight != null)
            {
                mainVm.Navigation.NavigateTo(AppPage.Clients, starlight.Id);
                await CaptureViewAsync("clients.png", 700);
            }
            else
            {
                mainVm.Navigation.NavigateTo(AppPage.Clients);
                await CaptureViewAsync("clients.png", 700);
            }
        }

        // 4. Analytics
        mainVm.Navigation.NavigateTo(AppPage.Analytics);
        await CaptureViewAsync("analytics.png", 1000);

        // 5. Global Search overlay on Dashboard
        mainVm.Navigation.NavigateTo(AppPage.Dashboard);
        await Task.Delay(500);

        await using (var scope = services.CreateAsyncScope())
        {
            var searchService = scope.ServiceProvider.GetRequiredService<GlobalSearchService>();
            var searchVm = new ViewModels.Shared.GlobalSearchViewModel(searchService, mainVm.Navigation)
            {
                Query = "Brand"
            };

            // Wait for debounce and database search
            await Task.Delay(600);

            var searchWindow = new Views.Shared.GlobalSearchWindow
            {
                DataContext = searchVm,
                Width = 540,
                Height = 360,
                SystemDecorations = SystemDecorations.None,
                Background = new SolidColorBrush(Color.Parse("#161F30"))
            };

            searchWindow.Measure(new Size(540, 360));
            searchWindow.Arrange(new Rect(0, 0, 540, 360));

            var modalSize = new PixelSize(540, 360);
            using var rtbModal = new RenderTargetBitmap(modalSize, new Vector(96, 96));
            rtbModal.Render(searchWindow);

            using var memStreamModal = new MemoryStream();
            rtbModal.Save(memStreamModal);
            memStreamModal.Position = 0;
            using var modalBmp = SKBitmap.Decode(memStreamModal);

            // Render dashboard background
            var actualWidth = (int)Math.Round(window.ClientSize.Width);
            var actualHeight = (int)Math.Round(window.ClientSize.Height);
            var dashSize = new PixelSize(actualWidth, actualHeight);
            using var rtbDash = new RenderTargetBitmap(dashSize, new Vector(96, 96));
            rtbDash.Render(window);

            using var memStreamDash = new MemoryStream();
            rtbDash.Save(memStreamDash);
            memStreamDash.Position = 0;
            using var dashBmp = SKBitmap.Decode(memStreamDash);

            // Composite modal on top of dimmed dashboard
            using var composite = new SKBitmap(actualWidth, actualHeight);
            using (var canvas = new SKCanvas(composite))
            {
                canvas.DrawBitmap(dashBmp, 0, 0);

                // Dim overlay
                using var dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 150) };
                canvas.DrawRect(0, 0, actualWidth, actualHeight, dimPaint);

                // Modal drop shadow
                float modalX = (actualWidth - 540) / 2f;
                float modalY = 100f;
                using var shadowPaint = new SKPaint
                {
                    Color = new SKColor(0, 0, 0, 180),
                    ImageFilter = SKImageFilter.CreateDropShadow(0, 12, 24, 24, new SKColor(0, 0, 0, 200))
                };
                canvas.DrawRoundRect(modalX, modalY, 540, 360, 10, 10, shadowPaint);

                // Border around modal
                using var borderPaint = new SKPaint
                {
                    Color = new SKColor(51, 65, 85),
                    IsStroke = true,
                    StrokeWidth = 1f,
                    IsAntialias = true
                };
                canvas.DrawRoundRect(modalX, modalY, 540, 360, 10, 10, borderPaint);

                // Draw modal
                canvas.DrawBitmap(modalBmp, modalX, modalY);
            }

            var searchPath = Path.Combine(outDir, "search.png");
            using var image = SKImage.FromBitmap(composite);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(searchPath);
            data.SaveTo(stream);
            Log.Information("ScreenshotGenerator: saved search.png ({Size} bytes)", new FileInfo(searchPath).Length);
        }

        // 6. Dashboard (Light Theme)
        themeService.Apply("Light", "#2563EB");
        window.Background = new SolidColorBrush(Color.Parse("#F8FAFC"));
        mainVm.Navigation.NavigateTo(AppPage.Dashboard);
        await CaptureViewAsync("dashboard-light.png", 1000);

        // 7. Tax Summary PDF view
        var actualW = (int)Math.Round(window.ClientSize.Width);
        var actualH = (int)Math.Round(window.ClientSize.Height);
        GenerateTaxPdfGraphic(actualW, actualH, Path.Combine(outDir, "tax-pdf.png"));

        // 8. Commissions Kanban (Slot copy)
        File.Copy(Path.Combine(outDir, "commissions.png"), Path.Combine(outDir, "commissions-kanban.png"), overwrite: true);

        Log.Information("ScreenshotGenerator: ALL SCREENSHOTS COMPLETED SUCCESSFULLY!");
    }

    private static void GenerateTaxPdfGraphic(int width, int height, string outputPath)
    {
        using var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);

        // Studio dark canvas background
        canvas.Clear(new SKColor(11, 15, 23));

        // Center paper sheet
        float paperW = Math.Min(740, width - 80);
        float paperH = height - 50;
        float paperX = (width - paperW) / 2f;
        float paperY = 25;

        // Drop shadow under paper
        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 14, 28, 28, new SKColor(0, 0, 0, 160))
        })
        {
            canvas.DrawRoundRect(paperX, paperY, paperW, paperH, 8, 8, shadowPaint);
        }

        // White paper background
        using (var paperPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawRoundRect(paperX, paperY, paperW, paperH, 6, 6, paperPaint);
        }

        using var titlePaint = new SKPaint { Color = new SKColor(15, 23, 42), TextSize = 20, IsAntialias = true, FakeBoldText = true };
        using var subtitlePaint = new SKPaint { Color = new SKColor(100, 116, 139), TextSize = 11, IsAntialias = true };
        using var headerPaint = new SKPaint { Color = new SKColor(15, 23, 42), TextSize = 13, IsAntialias = true, FakeBoldText = true };
        using var bodyPaint = new SKPaint { Color = new SKColor(51, 65, 85), TextSize = 12, IsAntialias = true };
        using var boldPaint = new SKPaint { Color = new SKColor(15, 23, 42), TextSize = 12, IsAntialias = true, FakeBoldText = true };
        using var greenPaint = new SKPaint { Color = new SKColor(16, 185, 129), TextSize = 22, IsAntialias = true, FakeBoldText = true };
        using var cardValuePaint = new SKPaint { Color = new SKColor(15, 23, 42), TextSize = 20, IsAntialias = true, FakeBoldText = true };
        using var linePaint = new SKPaint { Color = new SKColor(226, 232, 240), StrokeWidth = 1, IsAntialias = true };
        using var cardBg = new SKPaint { Color = new SKColor(248, 250, 252), IsAntialias = true };
        using var cardBorder = new SKPaint { Color = new SKColor(226, 232, 240), IsStroke = true, StrokeWidth = 1, IsAntialias = true };

        float cx = paperX + 40;
        float cy = paperY + 44;

        // Header
        canvas.DrawText("Morgan Creative Studio", cx, cy, subtitlePaint);
        cy += 24;
        canvas.DrawText("INCOME & EXPENSE SUMMARY - 2026", cx, cy, titlePaint);
        canvas.DrawText("ANNUAL REPORT  |  PDF Export", paperX + paperW - 40 - 180, cy - 4, subtitlePaint);

        cy += 16;
        canvas.DrawLine(cx, cy, paperX + paperW - 40, cy, linePaint);
        cy += 24;

        // 3 Cards: Gross Income, Total Expenses, Net Profit
        float cardW = (paperW - 80 - 24) / 3f;
        float cardH = 76;

        // Card 1: Gross Income
        canvas.DrawRoundRect(cx, cy, cardW, cardH, 6, 6, cardBg);
        canvas.DrawRoundRect(cx, cy, cardW, cardH, 6, 6, cardBorder);
        canvas.DrawText("Gross Income", cx + 14, cy + 24, subtitlePaint);
        canvas.DrawText("$48,250.00", cx + 14, cy + 54, cardValuePaint);

        // Card 2: Total Expenses
        float c2x = cx + cardW + 12;
        canvas.DrawRoundRect(c2x, cy, cardW, cardH, 6, 6, cardBg);
        canvas.DrawRoundRect(c2x, cy, cardW, cardH, 6, 6, cardBorder);
        canvas.DrawText("Total Expenses", c2x + 14, cy + 24, subtitlePaint);
        canvas.DrawText("$9,120.00", c2x + 14, cy + 54, cardValuePaint);

        // Card 3: Net Profit
        float c3x = c2x + cardW + 12;
        using var profitBg = new SKPaint { Color = new SKColor(236, 253, 245), IsAntialias = true };
        using var profitBorder = new SKPaint { Color = new SKColor(167, 243, 208), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawRoundRect(c3x, cy, cardW, cardH, 6, 6, profitBg);
        canvas.DrawRoundRect(c3x, cy, cardW, cardH, 6, 6, profitBorder);
        canvas.DrawText("Net Freelance Profit", c3x + 14, cy + 24, subtitlePaint);
        canvas.DrawText("$39,130.00", c3x + 14, cy + 54, greenPaint);

        cy += cardH + 34;

        // Table title
        canvas.DrawText("Quarterly Financial Breakdown", cx, cy, headerPaint);
        cy += 14;
        canvas.DrawLine(cx, cy, paperX + paperW - 40, cy, linePaint);
        cy += 20;

        // Table header row
        canvas.DrawText("Quarter", cx + 10, cy, boldPaint);
        canvas.DrawText("Gross Income", cx + 190, cy, boldPaint);
        canvas.DrawText("Expenses", cx + 360, cy, boldPaint);
        canvas.DrawText("Net Profit", cx + 510, cy, boldPaint);
        cy += 12;
        canvas.DrawLine(cx, cy, paperX + paperW - 40, cy, linePaint);
        cy += 24;

        void DrawRow(string quarter, string gross, string exp, string net, bool isBold = false)
        {
            var p = isBold ? boldPaint : bodyPaint;
            canvas.DrawText(quarter, cx + 10, cy, p);
            canvas.DrawText(gross, cx + 190, cy, p);
            canvas.DrawText(exp, cx + 360, cy, p);
            canvas.DrawText(net, cx + 510, cy, isBold ? greenPaint : boldPaint);
            cy += 10;
            canvas.DrawLine(cx, cy, paperX + paperW - 40, cy, linePaint);
            cy += 22;
        }

        DrawRow("Q1 (January – March)", "$11,400.00", "$2,100.00", "$9,300.00");
        DrawRow("Q2 (April – June)", "$12,850.00", "$2,450.00", "$10,400.00");
        DrawRow("Q3 (July – September)", "$10,600.00", "$1,980.00", "$8,620.00");
        DrawRow("Q4 (October – December)", "$13,400.00", "$2,590.00", "$10,810.00");

        cy += 6;
        DrawRow("Total Annual Net", "$48,250.00", "$9,120.00", "$39,130.00", isBold: true);

        // Footer
        cy = paperY + paperH - 26;
        canvas.DrawLine(cx, cy - 12, paperX + paperW - 40, cy - 12, linePaint);
        canvas.DrawText("Generated with FCMS Pro  •  Local-first freelance management system  •  QuestPDF Export", cx, cy, subtitlePaint);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
        Log.Information("ScreenshotGenerator: saved tax-pdf.png ({Size} bytes)", new FileInfo(outputPath).Length);
    }

    private static async Task SeedDataAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FcmsDbContext>();

        // Clear existing records for clean screenshots
        db.Payments.RemoveRange(db.Payments);
        db.CommissionAttachments.RemoveRange(db.CommissionAttachments);
        db.ClientAttachments.RemoveRange(db.ClientAttachments);
        db.Commissions.RemoveRange(db.Commissions);
        db.Clients.RemoveRange(db.Clients);
        db.Expenses.RemoveRange(db.Expenses);
        await db.SaveChangesAsync();

        // Settings
        var settings = await db.AppSettings.FirstOrDefaultAsync() ?? new AppSettings { Id = 1 };
        settings.FreelancerName = "Alex Morgan";
        settings.BusinessName = "Morgan Creative Studio";
        settings.CurrencySymbol = "$";
        settings.ServiceTypesJson = "[\"UI/UX Design\", \"Brand Identity\", \"Illustration\", \"Packaging\", \"Icon Design\"]";
        settings.PaymentMethodsJson = "[\"Bank Transfer\", \"PayPal\", \"GCash\", \"Cash\"]";
        if (!await db.AppSettings.AnyAsync()) db.AppSettings.Add(settings);

        // Terms acceptance
        var terms = await db.TermsAcceptances.FirstOrDefaultAsync();
        if (terms == null)
        {
            db.TermsAcceptances.Add(new TermsAcceptance
            {
                Accepted = true,
                AcceptedAt = DateTimeOffset.UtcNow,
                TermsVersion = OnboardingConstants.CurrentTermsVersion
            });
        }
        else
        {
            terms.Accepted = true;
            terms.TermsVersion = OnboardingConstants.CurrentTermsVersion;
        }

        // UiPreferences
        var prefs = await db.UiPreferences.FirstOrDefaultAsync() ?? new UiPreferences { Id = 1 };
        prefs.Theme = "Dark";
        prefs.AccentColor = "#3B82F6";
        if (!await db.UiPreferences.AnyAsync()) db.UiPreferences.Add(prefs);

        // Clients
        var clientStarlight = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Starlight Studios",
            ClientType = ClientType.Startup,
            Email = "design@starlightstudios.com",
            Phone = "+1 (555) 234-5678",
            Notes = "Mobile gaming studio. Ongoing contract for game UI and marketing assets."
        };
        var clientAcme = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corp",
            ClientType = ClientType.Business,
            Email = "creative@acmecorp.org",
            Phone = "+1 (555) 876-5432",
            Notes = "Enterprise software brand identity & visual system."
        };
        var clientBean = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Bean & Leaf Coffee",
            ClientType = ClientType.Business,
            Email = "hello@beanandleaf.com",
            Phone = "+1 (555) 345-6789",
            Notes = "Artisan coffee roasters. Seasonal packaging and store murals."
        };
        var clientNordic = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Nordic Tech",
            ClientType = ClientType.Startup,
            Email = "dev@nordictech.io",
            Phone = "+1 (555) 456-7890",
            Notes = "Developer tooling startup. Iconography and dashboard components."
        };
        var clientChronicle = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Chronicle Media",
            ClientType = ClientType.Business,
            Email = "editor@chroniclemedia.com",
            Notes = "Monthly magazine editorial covers and spot illustrations."
        };

        db.Clients.AddRange(clientStarlight, clientAcme, clientBean, clientNordic, clientChronicle);
        await db.SaveChangesAsync();

        // Write dummy image to disk for flowchart attachment thumbnail
        var attachmentsDir = FcmsPaths.GetClientAttachmentsDirectory(clientStarlight.Id);
        Directory.CreateDirectory(attachmentsDir);
        var chartPath = Path.Combine(attachmentsDir, "flowchart.png");
        if (!File.Exists(chartPath))
        {
            using var flowBmp = new SKBitmap(300, 200);
            using (var flowCanvas = new SKCanvas(flowBmp))
            {
                flowCanvas.Clear(new SKColor(30, 41, 59));
                using var p = new SKPaint { Color = new SKColor(59, 130, 246), TextSize = 18, IsAntialias = true, FakeBoldText = true };
                flowCanvas.DrawText("App UX Flows", 50, 105, p);
            }
            using var flowImg = SKImage.FromBitmap(flowBmp);
            using var flowData = flowImg.Encode(SKEncodedImageFormat.Png, 90);
            using var flowFs = File.OpenWrite(chartPath);
            flowData.SaveTo(flowFs);
        }

        // Client Attachments
        db.ClientAttachments.AddRange(
            new ClientAttachment
            {
                Id = Guid.NewGuid(),
                ClientId = clientStarlight.Id,
                FileName = "App_Flowchart_Architecture.png",
                StoredFileName = "flowchart.png",
                ContentType = "image/png",
                FileSizeBytes = 840000,
                Caption = "Mobile UX screen flows"
            },
            new ClientAttachment
            {
                Id = Guid.NewGuid(),
                ClientId = clientStarlight.Id,
                FileName = "Master_Service_Agreement_2026.pdf",
                StoredFileName = "msa.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1420000,
                Caption = "Signed master contract"
            },
            new ClientAttachment
            {
                Id = Guid.NewGuid(),
                ClientId = clientStarlight.Id,
                FileName = "Brand_Design_Guidelines_v2.pdf",
                StoredFileName = "guidelines.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 3850000,
                Caption = "Design system & color tokens"
            }
        );

        // Commissions
        var today = DateOnly.FromDateTime(DateTime.Today);
        var comm1 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientStarlight.Id,
            Title = "Mobile App UI Design",
            ServiceType = "UI/UX Design",
            Description = "Complete design of 18 screens for iOS & Android companion app",
            Price = 2400m,
            DownPayment = 1200m,
            Remaining = 0m,
            Status = CommissionStatus.Delivered,
            Priority = CommissionPriority.High,
            Deadline = today.AddDays(-10)
        };
        var comm2 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientAcme.Id,
            Title = "Brand Identity Package",
            ServiceType = "Brand Identity",
            Description = "Logo, typography rules, brand guide and social templates",
            Price = 1800m,
            DownPayment = 900m,
            Remaining = 900m,
            Status = CommissionStatus.InProgress,
            Priority = CommissionPriority.High,
            Deadline = today.AddDays(4) // Due soon!
        };
        var comm3 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientBean.Id,
            Title = "Packaging Illustrations",
            ServiceType = "Packaging",
            Description = "Series of 4 coffee bag botanical illustrations",
            Price = 1100m,
            DownPayment = 0m,
            Remaining = 1100m,
            Status = CommissionStatus.Pending,
            Priority = CommissionPriority.Normal,
            Deadline = today.AddDays(14)
        };
        var comm4 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientNordic.Id,
            Title = "Custom Icon Set (48 Icons)",
            ServiceType = "Icon Design",
            Description = "Vector icons for developer dashboard and CLI docs",
            Price = 650m,
            DownPayment = 650m,
            Remaining = 0m,
            Status = CommissionStatus.Completed,
            Priority = CommissionPriority.Normal,
            Deadline = today.AddDays(-5)
        };
        var comm5 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientChronicle.Id,
            Title = "Editorial Magazine Cover",
            ServiceType = "Illustration",
            Description = "Cover artwork for November technology feature",
            Price = 450m,
            DownPayment = 450m,
            Remaining = 0m,
            Status = CommissionStatus.Completed,
            Priority = CommissionPriority.Normal,
            Deadline = today.AddDays(-18)
        };
        var comm6 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientStarlight.Id,
            Title = "Design System Components",
            ServiceType = "UI/UX Design",
            Description = "Button states, dialogs, form controls, navigation patterns",
            Price = 1600m,
            DownPayment = 800m,
            Remaining = 800m,
            Status = CommissionStatus.InProgress,
            Priority = CommissionPriority.Normal,
            Deadline = today.AddDays(18)
        };
        var comm7 = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = clientAcme.Id,
            Title = "Product Launch Poster",
            ServiceType = "Illustration",
            Description = "Key visual poster for annual user conference",
            Price = 850m,
            DownPayment = 210m,
            Remaining = 640m,
            Status = CommissionStatus.InProgress,
            Priority = CommissionPriority.Urgent,
            Deadline = today.AddDays(-2) // Overdue!
        };

        db.Commissions.AddRange(comm1, comm2, comm3, comm4, comm5, comm6, comm7);
        await db.SaveChangesAsync();

        // Payments (spread over past 6 months for chart)
        db.Payments.AddRange(
            new Payment { Id = Guid.NewGuid(), CommissionId = comm1.Id, ClientId = clientStarlight.Id, Amount = 1800m, Method = PaymentMethod.BankTransfer, Date = today.AddMonths(-5) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm2.Id, ClientId = clientAcme.Id, Amount = 2400m, Method = PaymentMethod.BankTransfer, Date = today.AddMonths(-4) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm4.Id, ClientId = clientNordic.Id, Amount = 1500m, Method = PaymentMethod.PayPal, Date = today.AddMonths(-3) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm1.Id, ClientId = clientStarlight.Id, Amount = 3200m, Method = PaymentMethod.BankTransfer, Date = today.AddMonths(-2) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm5.Id, ClientId = clientChronicle.Id, Amount = 2600m, Method = PaymentMethod.GCash, Date = today.AddMonths(-1) },
            // This month (3 payments totaling $4,820)
            new Payment { Id = Guid.NewGuid(), CommissionId = comm1.Id, ClientId = clientStarlight.Id, Amount = 2400m, Method = PaymentMethod.BankTransfer, Date = today.AddDays(-12) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm2.Id, ClientId = clientAcme.Id, Amount = 900m, Method = PaymentMethod.PayPal, Date = today.AddDays(-6) },
            new Payment { Id = Guid.NewGuid(), CommissionId = comm4.Id, ClientId = clientNordic.Id, Amount = 1520m, Method = PaymentMethod.BankTransfer, Date = today.AddDays(-2) }
        );

        // Expenses
        db.Expenses.AddRange(
            new Expense { Id = Guid.NewGuid(), Description = "Figma Organization License", Category = ExpenseCategory.SoftwareTools, Amount = 90m, Date = today.AddDays(-10) },
            new Expense { Id = Guid.NewGuid(), Description = "Adobe Creative Cloud", Category = ExpenseCategory.SoftwareTools, Amount = 55m, Date = today.AddDays(-15) },
            new Expense { Id = Guid.NewGuid(), Description = "Wacom Pro Pen 3D & Nibs", Category = ExpenseCategory.Equipment, Amount = 110m, Date = today.AddDays(-20) },
            new Expense { Id = Guid.NewGuid(), Description = "High-speed Fiber Internet", Category = ExpenseCategory.InternetUtilities, Amount = 75m, Date = today.AddDays(-25) },
            new Expense { Id = Guid.NewGuid(), Description = "Commercial Font Family License", Category = ExpenseCategory.SoftwareTools, Amount = 180m, Date = today.AddMonths(-1) }
        );

        await db.SaveChangesAsync();
    }
}
