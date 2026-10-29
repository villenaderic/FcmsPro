using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace VideoMaker;

public record Scene(
    string Id,
    string Category,
    string Title,
    string Narration,
    string? ScreenshotFile,
    SceneType Type,
    string? SubtitleUrl = null
);

public enum SceneType
{
    WebBrowser,
    InstallSteps,
    Screenshot,
    Finances,
    Themes,
    Privacy,
    Outro
}

public static class Program
{
    private const int FrameWidth = 1920;
    private const int FrameHeight = 1080;

    public static void Main(string[] args)
    {
        Console.WriteLine("=== FCMS Pro Enhanced Video Frame Generator (Roderic Villena • Philippine Peso) ===");

        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var buildDir = Path.Combine(rootDir, "build-video");
        var framesDir = Path.Combine(buildDir, "frames");
        var screenshotsDir = Path.Combine(rootDir, "site", "assets", "img", "screenshots");
        var logoPath = Path.Combine(rootDir, "src", "FcmsPro.Avalonia", "Assets", "app-icon-source.png");
        if (!File.Exists(logoPath))
        {
            logoPath = Path.Combine(rootDir, "site", "assets", "img", "icon-512.png");
        }

        Directory.CreateDirectory(buildDir);
        Directory.CreateDirectory(framesDir);

        Console.WriteLine("Generating high-resolution Global Search graphic...");
        GenerateSearchScreenshot(screenshotsDir, rootDir);

        var scenes = new List<Scene>
        {
            new Scene(
                "scene_01_hook_web",
                "RODERIC VILLENA",
                "MEET THE FREELANCER • LOCAL-FIRST SYSTEM",
                "Hi everyone, I'm Roderic Villena! I've been a freelancer since 2023, and I've been using FCMS Pro every single day for over 4 months to run my client commissions. If you're tired of paying monthly subscriptions just to track invoices on slow, bloated web apps, you're going to love this.",
                Path.Combine(buildDir, "web_01_hero.png"),
                SceneType.WebBrowser,
                "https://villenaderic.github.io/FcmsPro/"
            ),
            new Scene(
                "scene_02_web_features",
                "OFFICIAL WEBSITE",
                "ZERO SUBSCRIPTIONS • 100% PRIVATE",
                "Right on the official website at villenaderic dot github dot io slash FcmsPro, you can explore the entire project. As a Filipino freelancer, having native Philippine Peso support was essential for me. Everything is local-first: no accounts, zero cloud tracking, and your data never leaves your computer.",
                Path.Combine(buildDir, "web_02_features.png"),
                SceneType.WebBrowser,
                "https://villenaderic.github.io/FcmsPro/#features"
            ),
            new Scene(
                "scene_03_download_page",
                "DOWNLOAD & PLATFORMS",
                "WINDOWS, MACOS & LINUX PACKAGES",
                "Installing FCMS Pro takes less than sixty seconds. The Download page automatically detects your operating system — whether you're on Windows, macOS, or Linux — and provides a lightweight installer or portable package ready to run.",
                Path.Combine(buildDir, "web_03_download.png"),
                SceneType.WebBrowser,
                "https://villenaderic.github.io/FcmsPro/download/"
            ),
            new Scene(
                "scene_04_install_walkthrough",
                "INSTALLATION WALKTHROUGH",
                "FAST 60-SECOND OFFLINE SETUP",
                "Setup is virtually effortless. Just download the installer, click run, and FCMS Pro instantly provisions an encrypted-ready local SQLite database in your user directory. No database servers to configure, no email verification, and no setup headaches.",
                null,
                SceneType.InstallSteps
            ),
            new Scene(
                "scene_05_dashboard",
                "ACTUAL APP • LIVE PULSE",
                "DASHBOARD IN PHILIPPINE PESOS",
                "Once you launch the app, you're greeted by the Dashboard. Here's my live business pulse in Philippine Pesos: tracking twenty thousand pesos in sample demo income, active project balances, overdue alerts, and an interactive income trend trajectory.",
                Path.Combine(screenshotsDir, "dashboard.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_06_commissions",
                "ACTUAL APP • WORKFLOW",
                "COMMISSIONS PIPELINE & KANBAN",
                "Managing project deadlines has never been smoother. Move your commissions across customizable stages from Lead and In Progress to Delivered. View your pipeline in a detailed table or an intuitive Kanban board, complete with down payments, balances, and urgency countdowns.",
                Path.Combine(screenshotsDir, "commissions.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_07_clients",
                "ACTUAL APP • CRM",
                "CLIENT CRM & DOCUMENT STORAGE",
                "Never lose track of a client again. The built-in CRM lets you store full contact records, lifetime earnings in pesos, and active project counts. You can even attach contracts, creative briefs, and project files directly to each client's profile.",
                Path.Combine(screenshotsDir, "clients.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_08_finances",
                "ACTUAL APP • FINANCES",
                "PAYMENTS & RECURRING EXPENSES",
                "Take total control of your cash flow. Record client payments across multiple methods to automatically update project balances. Categorize business expenses and track recurring subscriptions to see your true net profit.",
                null,
                SceneType.Finances
            ),
            new Scene(
                "scene_09_analytics",
                "ACTUAL APP • INSIGHTS",
                "FINANCIAL ANALYTICS & REVENUE",
                "Gain powerful insights into your freelance business with built-in analytics. Review month-over-month revenue growth, identify your highest-earning clients, and see exactly where your expenses go without ever touching a spreadsheet.",
                Path.Combine(screenshotsDir, "analytics.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_10_search",
                "ACTUAL APP • SHORTCUT",
                "LIGHTNING GLOBAL SEARCH ( / )",
                "Need to find something in a split second? Just hit the forward slash key anywhere in the app to summon Global Search. Instantly search across all your clients, commissions, and transactions at lightning speed.",
                Path.Combine(screenshotsDir, "search.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_11_reports",
                "ACTUAL APP • TAX SEASON",
                "ONE-CLICK TAX PDF EXPORT (PESOS)",
                "When tax season arrives, don't stress. FCMS Pro generates publication-quality quarterly and annual financial summary PDFs in Philippine Pesos with one click, cleanly breaking down gross revenue, deductible expenses, and net profit ready for your accountant.",
                Path.Combine(screenshotsDir, "tax-pdf.png"),
                SceneType.Screenshot
            ),
            new Scene(
                "scene_12_themes",
                "ACTUAL APP • STYLES",
                "DARK & LIGHT THEMES & ACCENTS",
                "Work comfortably day or night. Switch between a sleek Dark theme and a crisp Light theme with a single toggle, customize your accent color, and configure Philippine Pesos or any international currency as your default.",
                null,
                SceneType.Themes
            ),
            new Scene(
                "scene_13_privacy",
                "CORE ARCHITECTURE",
                "100% OFFLINE PRIVACY & BACKUPS",
                "Best of all, you get absolute peace of mind. Because FCMS Pro is local-first, your financial records stay exclusively on your device with automatic rotating backups every time you exit. You're never locked in, and you can export your entire database anytime.",
                null,
                SceneType.Privacy
            ),
            new Scene(
                "scene_14_outro",
                "GET IN TOUCH • RODERIC VILLENA",
                "HIRE FOR WEBSITES, SOFTWARE & TOOLS",
                "Stop paying monthly fees for cloud trackers. Upgrade your freelance business today with FCMS Pro. Download it for free at villenaderic dot github dot io slash FcmsPro. And if you're a business or individual looking for custom websites, software applications, or specialized business tools, feel free to contact me directly on Messenger — let's build something great together. Happy freelancing!",
                null,
                SceneType.Outro
            )
        };

        Console.WriteLine($"Rendering {scenes.Count} high-resolution slides with official logo to {framesDir}...");
        foreach (var scene in scenes)
        {
            var framePath = Path.Combine(framesDir, $"{scene.Id}.png");
            Console.WriteLine($"  -> Rendering {scene.Id} ({scene.Title})...");
            using var bitmap = RenderSceneFrame(scene, rootDir, screenshotsDir, logoPath);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = File.Create(framePath);
            data.SaveTo(fs);
        }

        Console.WriteLine("\nAll frame slides rendered successfully!");
    }

    private static SKBitmap RenderSceneFrame(Scene scene, string rootDir, string screenshotsDir, string logoPath)
    {
        var bmp = new SKBitmap(FrameWidth, FrameHeight);
        using var canvas = new SKCanvas(bmp);

        // 1. Studio Deep Navy/Black Gradient Canvas
        using (var bgShader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(FrameWidth, FrameHeight),
            new[] { new SKColor(7, 10, 19), new SKColor(11, 17, 32), new SKColor(6, 9, 18) },
            null,
            SKShaderTileMode.Clamp))
        using (var bgPaint = new SKPaint { Shader = bgShader })
        {
            canvas.DrawRect(0, 0, FrameWidth, FrameHeight, bgPaint);
        }

        // Ambient Studio Glows
        using (var glow1 = new SKPaint
        {
            Color = new SKColor(37, 99, 235, 28),
            ImageFilter = SKImageFilter.CreateBlur(130, 130)
        })
        {
            canvas.DrawCircle(220, 140, 260, glow1);
        }

        using (var glow2 = new SKPaint
        {
            Color = new SKColor(99, 102, 241, 24),
            ImageFilter = SKImageFilter.CreateBlur(150, 150)
        })
        {
            canvas.DrawCircle(FrameWidth - 260, FrameHeight - 200, 320, glow2);
        }

        // Subtle geometric grid dots
        using (var dotPaint = new SKPaint { Color = new SKColor(255, 255, 255, 8), IsAntialias = true })
        {
            for (int x = 40; x < FrameWidth; x += 60)
            {
                for (int y = 40; y < FrameHeight; y += 60)
                {
                    canvas.DrawCircle(x, y, 1.2f, dotPaint);
                }
            }
        }

        // 2. Header Bar with ACTUAL OFFICIAL LOGO
        RenderHeader(canvas, scene, logoPath);

        // 3. Central Visual Content
        switch (scene.Type)
        {
            case SceneType.WebBrowser:
                RenderBrowserWindow(canvas, scene.ScreenshotFile!, scene.SubtitleUrl ?? "https://villenaderic.github.io/FcmsPro/", logoPath);
                break;
            case SceneType.InstallSteps:
                RenderInstallStepsContent(canvas);
                break;
            case SceneType.Finances:
                RenderFinancesContent(canvas);
                break;
            case SceneType.Themes:
                RenderThemesContent(canvas, screenshotsDir);
                break;
            case SceneType.Privacy:
                RenderPrivacyContent(canvas);
                break;
            case SceneType.Outro:
                RenderOutroContent(canvas, rootDir, logoPath);
                break;
            case SceneType.Screenshot:
            default:
                if (!string.IsNullOrEmpty(scene.ScreenshotFile) && File.Exists(scene.ScreenshotFile))
                {
                    RenderScreenshotWindow(canvas, scene.ScreenshotFile, scene.Title);
                }
                break;
        }

        // 4. Subtitle Caption Bar at Bottom
        RenderSubtitleBar(canvas, scene.Narration);

        return bmp;
    }

    private static void RenderHeader(SKCanvas canvas, Scene scene, string logoPath)
    {
        float barY = 46;

        // Actual Official Brand Logo
        if (File.Exists(logoPath))
        {
            using var logoBmp = SKBitmap.Decode(logoPath);
            if (logoBmp != null)
            {
                var logoRect = new SKRect(70, barY, 70 + 40, barY + 40);
                using var logoClip = new SKPath();
                logoClip.AddRoundRect(logoRect, 10, 10);
                canvas.Save();
                canvas.ClipPath(logoClip, antialias: true);
                canvas.DrawBitmap(logoBmp, logoRect);
                canvas.Restore();

                using var logoBorder = new SKPaint { Color = new SKColor(59, 130, 246, 160), IsStroke = true, StrokeWidth = 1.2f, IsAntialias = true };
                canvas.DrawRoundRect(logoRect, 10, 10, logoBorder);
            }
        }
        else
        {
            using var iconBg = new SKPaint { Color = new SKColor(37, 99, 235), IsAntialias = true };
            canvas.DrawRoundRect(70, barY, 40, 40, 10, 10, iconBg);
        }

        // App Brand Name & Subtext
        using (var brandPaint = new SKPaint { Color = SKColors.White, TextSize = 21, FakeBoldText = true, IsAntialias = true })
        {
            canvas.DrawText("FCMS Pro", 122, barY + 28, brandPaint);
        }
        using (var tagPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 13, IsAntialias = true })
        {
            canvas.DrawText("OFFICIAL PROJECT DEMO • RODERIC VILLENA", 232, barY + 27, tagPaint);
        }

        // Right side: Category Badge & Title
        float rightX = FrameWidth - 70;
        using var badgeTextPaint = new SKPaint { Color = new SKColor(56, 189, 248), TextSize = 12, FakeBoldText = true, IsAntialias = true };
        float badgeWidth = badgeTextPaint.MeasureText(scene.Category) + 24;
        float badgeX = rightX - badgeWidth;

        using (var badgeBg = new SKPaint { Color = new SKColor(14, 165, 233, 30), IsAntialias = true })
        using (var badgeBorder = new SKPaint { Color = new SKColor(56, 189, 248, 110), IsStroke = true, StrokeWidth = 1, IsAntialias = true })
        {
            var badgeRect = new SKRect(badgeX, barY + 6, rightX, barY + 34);
            canvas.DrawRoundRect(badgeRect, 6, 6, badgeBg);
            canvas.DrawRoundRect(badgeRect, 6, 6, badgeBorder);
            canvas.DrawText(scene.Category, badgeX + 12, barY + 25, badgeTextPaint);
        }

        // Category Section Title
        using var titlePaint = new SKPaint { Color = SKColors.White, TextSize = 17, FakeBoldText = true, IsAntialias = true };
        float titleWidth = titlePaint.MeasureText(scene.Title);
        canvas.DrawText(scene.Title, badgeX - titleWidth - 22, barY + 28, titlePaint);

        // Header separator line
        using var linePaint = new SKPaint { Color = new SKColor(51, 65, 85, 120), StrokeWidth = 1, IsAntialias = true };
        canvas.DrawLine(70, barY + 54, FrameWidth - 70, barY + 54, linePaint);
    }

    private static void RenderBrowserWindow(SKCanvas canvas, string imagePath, string currentUrl, string logoPath)
    {
        using var original = SKBitmap.Decode(imagePath);
        if (original == null) return;

        float winW = 1480;
        float winH = 750;
        float winX = (FrameWidth - winW) / 2f;
        float winY = 120;
        float chromeH = 72;

        // Window Shadow
        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 240),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 24, 52, 52, new SKColor(0, 0, 0, 220))
        })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, shadowPaint);
        }

        // Window Frame Background
        using (var frameBg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, frameBg);
        }

        // Chrome Header Background
        using (var chromePaint = new SKPaint { Color = new SKColor(20, 29, 48), IsAntialias = true })
        {
            var headerRect = new SKRect(winX, winY, winX + winW, winY + chromeH);
            using var clip = new SKPath();
            clip.AddRoundRect(new SKRect(winX, winY, winX + winW, winY + winH), 14, 14);
            canvas.Save();
            canvas.ClipPath(clip, antialias: true);
            canvas.DrawRect(headerRect, chromePaint);
            canvas.Restore();
        }

        // 3 Window Control Dots (macOS style)
        using (var dotRed = new SKPaint { Color = new SKColor(239, 68, 68), IsAntialias = true })
        using (var dotYellow = new SKPaint { Color = new SKColor(245, 158, 11), IsAntialias = true })
        using (var dotGreen = new SKPaint { Color = new SKColor(16, 185, 129), IsAntialias = true })
        {
            canvas.DrawCircle(winX + 24, winY + 22, 6, dotRed);
            canvas.DrawCircle(winX + 44, winY + 22, 6, dotYellow);
            canvas.DrawCircle(winX + 64, winY + 22, 6, dotGreen);
        }

        // Browser Tab
        float tabX = winX + 90;
        float tabW = 300;
        float tabH = 32;
        using (var tabBg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        {
            canvas.DrawRoundRect(tabX, winY + 8, tabW, tabH, 8, 8, tabBg);
        }

        // Tab Logo Icon
        if (File.Exists(logoPath))
        {
            using var tabLogoBmp = SKBitmap.Decode(logoPath);
            if (tabLogoBmp != null)
            {
                var tabLogoRect = new SKRect(tabX + 10, winY + 16, tabX + 26, winY + 32);
                canvas.DrawBitmap(tabLogoBmp, tabLogoRect);
            }
        }

        using (var tabText = new SKPaint { Color = new SKColor(226, 232, 240), TextSize = 13, FakeBoldText = true, IsAntialias = true })
        {
            canvas.DrawText("FCMS Pro — Official Website", tabX + 34, winY + 29, tabText);
        }

        // Browser Address Bar
        float addrX = winX + 120;
        float addrY = winY + 42;
        float addrW = winW - 240;
        float addrH = 26;

        using (var addrBg = new SKPaint { Color = new SKColor(11, 17, 32), IsAntialias = true })
        using (var addrBorder = new SKPaint { Color = new SKColor(51, 65, 85), IsStroke = true, StrokeWidth = 1, IsAntialias = true })
        {
            var addrRect = new SKRect(addrX, addrY, addrX + addrW, addrY + addrH);
            canvas.DrawRoundRect(addrRect, 6, 6, addrBg);
            canvas.DrawRoundRect(addrRect, 6, 6, addrBorder);
        }

        // Lock icon & URL
        using (var lockPaint = new SKPaint { Color = new SKColor(16, 185, 129), TextSize = 12, FakeBoldText = true, IsAntialias = true })
        {
            canvas.DrawText("🔒 https://", addrX + 12, addrY + 18, lockPaint);
        }
        using (var urlPaint = new SKPaint { Color = new SKColor(241, 245, 249), TextSize = 12, FakeBoldText = true, IsAntialias = true })
        {
            string hostPart = currentUrl.Replace("https://", "");
            canvas.DrawText(hostPart, addrX + 76, addrY + 18, urlPaint);
        }

        // Webpage Viewport Content
        float contentY = winY + chromeH;
        var destRect = new SKRect(winX, contentY, winX + winW, winY + winH);

        using (var contentClip = new SKPath())
        {
            contentClip.AddRoundRect(new SKRect(winX, winY, winX + winW, winY + winH), 14, 14);
            canvas.Save();
            canvas.ClipPath(contentClip, antialias: true);
            canvas.DrawBitmap(original, destRect);
            canvas.Restore();
        }

        // Window Outer Border
        using (var winBorder = new SKPaint
        {
            Color = new SKColor(59, 130, 246, 120),
            IsStroke = true,
            StrokeWidth = 1.5f,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, winBorder);
        }
    }

    private static void RenderScreenshotWindow(SKCanvas canvas, string imagePath, string windowTitle)
    {
        using var original = SKBitmap.Decode(imagePath);
        if (original == null) return;

        float winW = 1440;
        float winH = 750;
        float winX = (FrameWidth - winW) / 2f;
        float winY = 120;
        float titleBarH = 40;

        // Window Shadow
        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 240),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 24, 52, 52, new SKColor(0, 0, 0, 220))
        })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, shadowPaint);
        }

        // Window Frame Background
        using (var frameBg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, frameBg);
        }

        // Window Title Bar
        using (var titleBarPaint = new SKPaint { Color = new SKColor(22, 32, 54), IsAntialias = true })
        {
            var titleBarRect = new SKRect(winX, winY, winX + winW, winY + titleBarH);
            using var clip = new SKPath();
            clip.AddRoundRect(new SKRect(winX, winY, winX + winW, winY + winH), 14, 14);
            canvas.Save();
            canvas.ClipPath(clip, antialias: true);
            canvas.DrawRect(titleBarRect, titleBarPaint);
            canvas.Restore();
        }

        // 3 Window Control Dots (macOS style)
        using (var dotRed = new SKPaint { Color = new SKColor(239, 68, 68), IsAntialias = true })
        using (var dotYellow = new SKPaint { Color = new SKColor(245, 158, 11), IsAntialias = true })
        using (var dotGreen = new SKPaint { Color = new SKColor(16, 185, 129), IsAntialias = true })
        {
            canvas.DrawCircle(winX + 24, winY + 20, 6, dotRed);
            canvas.DrawCircle(winX + 44, winY + 20, 6, dotYellow);
            canvas.DrawCircle(winX + 64, winY + 20, 6, dotGreen);
        }

        // Window Title Text
        using (var winTitlePaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 13, FakeBoldText = true, IsAntialias = true })
        {
            string displayTitle = $"FCMS Pro  —  {windowTitle}";
            float tw = winTitlePaint.MeasureText(displayTitle);
            canvas.DrawText(displayTitle, winX + (winW - tw) / 2f, winY + 25, winTitlePaint);
        }

        // Window Content (Screenshot)
        float contentY = winY + titleBarH;
        var destRect = new SKRect(winX, contentY, winX + winW, winY + winH);

        using (var contentClip = new SKPath())
        {
            contentClip.AddRoundRect(new SKRect(winX, winY, winX + winW, winY + winH), 14, 14);
            canvas.Save();
            canvas.ClipPath(contentClip, antialias: true);
            canvas.DrawBitmap(original, destRect);
            canvas.Restore();
        }

        // Window Outer Border
        using (var winBorder = new SKPaint
        {
            Color = new SKColor(71, 85, 105, 140),
            IsStroke = true,
            StrokeWidth = 1.5f,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(winX, winY, winW, winH, 14, 14, winBorder);
        }
    }

    private static void RenderInstallStepsContent(SKCanvas canvas)
    {
        float cy = 150;

        // Headline
        using (var headPaint = new SKPaint { Color = SKColors.White, TextSize = 38, FakeBoldText = true, IsAntialias = true })
        {
            string h = "Effortless 60-Second Setup • Zero Configuration";
            float w = headPaint.MeasureText(h);
            canvas.DrawText(h, (FrameWidth - w) / 2f, cy + 30, headPaint);
        }

        using (var subheadPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 18, IsAntialias = true })
        {
            string sh = "No database servers to install • No registration • No cloud credentials required";
            float w = subheadPaint.MeasureText(sh);
            canvas.DrawText(sh, (FrameWidth - w) / 2f, cy + 65, subheadPaint);
        }

        // 3 Step Cards Side-by-Side
        float cardW = 440;
        float cardH = 460;
        float gap = 40;
        float startX = (FrameWidth - (3 * cardW + 2 * gap)) / 2f;
        float cardY = cy + 110;

        var steps = new[]
        {
            new {
                Step = "01",
                Title = "Download Package",
                Sub = "From Official Website",
                Detail = "Get FCMS-Pro-Setup.exe for Windows, .dmg for macOS, or .deb/.AppImage for Linux directly from GitHub Releases.",
                Pill = "14 MB Lightweight",
                PillColor = new SKColor(59, 130, 246)
            },
            new {
                Step = "02",
                Title = "1-Click Local Setup",
                Sub = "Zero Cloud Lock-in",
                Detail = "Run the installer. It provisions an encrypted-ready local SQLite database in your OS AppData folder automatically in under 5 seconds.",
                Pill = "100% Offline Database",
                PillColor = new SKColor(16, 185, 129)
            },
            new {
                Step = "03",
                Title = "Launch & Command",
                Sub = "Start Tracking Today",
                Detail = "Open FCMS Pro and start managing clients, commissions, and revenue immediately. No emails, no passwords, no recurring paywalls.",
                Pill = "Ready Out-of-the-Box",
                PillColor = new SKColor(245, 158, 11)
            }
        };

        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (cardW + gap);
            var s = steps[i];

            // Card Shadow
            using (var shadow = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 160),
                ImageFilter = SKImageFilter.CreateDropShadow(0, 16, 32, 32, new SKColor(0, 0, 0, 180))
            })
            {
                canvas.DrawRoundRect(x, cardY, cardW, cardH, 16, 16, shadow);
            }

            // Card Background & Border
            using (var bg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
            using (var border = new SKPaint { Color = new SKColor(51, 65, 85, 160), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
            {
                canvas.DrawRoundRect(x, cardY, cardW, cardH, 16, 16, bg);
                canvas.DrawRoundRect(x, cardY, cardW, cardH, 16, 16, border);
            }

            // Step Number Badge
            using (var stepBg = new SKPaint { Color = s.PillColor.WithAlpha(40), IsAntialias = true })
            using (var stepBorder = new SKPaint { Color = s.PillColor, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
            {
                canvas.DrawRoundRect(x + 30, cardY + 30, 56, 56, 12, 12, stepBg);
                canvas.DrawRoundRect(x + 30, cardY + 30, 56, 56, 12, 12, stepBorder);
            }
            using (var stepText = new SKPaint { Color = s.PillColor, TextSize = 24, FakeBoldText = true, IsAntialias = true })
            {
                canvas.DrawText(s.Step, x + 44, cardY + 67, stepText);
            }

            // Title & Subtitle
            using (var titlePaint = new SKPaint { Color = SKColors.White, TextSize = 24, FakeBoldText = true, IsAntialias = true })
            {
                canvas.DrawText(s.Title, x + 30, cardY + 130, titlePaint);
            }
            using (var subPaint = new SKPaint { Color = s.PillColor, TextSize = 14, FakeBoldText = true, IsAntialias = true })
            {
                canvas.DrawText(s.Sub, x + 30, cardY + 155, subPaint);
            }

            // Details Paragraph
            using (var detailPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 15, IsAntialias = true })
            {
                DrawWrappedText(canvas, s.Detail, x + 30, cardY + 200, cardW - 60, 24, detailPaint);
            }

            // Bottom Feature Pill
            using (var pillBg = new SKPaint { Color = new SKColor(30, 41, 59), IsAntialias = true })
            using (var pillText = new SKPaint { Color = new SKColor(226, 232, 240), TextSize = 13, FakeBoldText = true, IsAntialias = true })
            {
                canvas.DrawRoundRect(x + 30, cardY + cardH - 60, cardW - 60, 36, 8, 8, pillBg);
                float pw = pillText.MeasureText(s.Pill);
                canvas.DrawText(s.Pill, x + (cardW - pw) / 2f, cardY + cardH - 37, pillText);
            }
        }
    }

    private static void RenderThemesContent(SKCanvas canvas, string screenshotsDir)
    {
        float cy = 120;
        float cardW = 680;
        float cardH = 670;
        float gap = 60;
        float startX = (FrameWidth - (2 * cardW + gap)) / 2f;

        string darkPath = Path.Combine(screenshotsDir, "dashboard.png");
        string lightPath = Path.Combine(screenshotsDir, "dashboard-light.png");

        // Left: Dark Theme
        RenderThemeCard(canvas, startX, cy, cardW, cardH, "Dark Theme (Default)", "Sleek, high-contrast, easy on the eyes during late night creative sessions.", darkPath, new SKColor(37, 99, 235));

        // Right: Light Theme
        RenderThemeCard(canvas, startX + cardW + gap, cy, cardW, cardH, "Light Theme", "Crisp, clean, publication-ready view perfect for daylight workspaces.", lightPath, new SKColor(16, 185, 129));
    }

    private static void RenderThemeCard(SKCanvas canvas, float x, float y, float w, float h, string title, string sub, string imgPath, SKColor accent)
    {
        // Shadow & Card Frame
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 200), ImageFilter = SKImageFilter.CreateDropShadow(0, 18, 40, 40, new SKColor(0, 0, 0, 220)) })
        using (var bg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        using (var border = new SKPaint { Color = accent.WithAlpha(120), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawRoundRect(x, y, w, h, 14, 14, shadow);
            canvas.DrawRoundRect(x, y, w, h, 14, 14, bg);
            canvas.DrawRoundRect(x, y, w, h, 14, 14, border);
        }

        // Header
        using (var tPaint = new SKPaint { Color = SKColors.White, TextSize = 24, FakeBoldText = true, IsAntialias = true })
        {
            canvas.DrawText(title, x + 24, y + 42, tPaint);
        }
        using (var subPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 13, IsAntialias = true })
        {
            canvas.DrawText(sub, x + 24, y + 68, subPaint);
        }

        // Image
        if (File.Exists(imgPath))
        {
            using var bmp = SKBitmap.Decode(imgPath);
            if (bmp != null)
            {
                var dest = new SKRect(x + 16, y + 85, x + w - 16, y + h - 16);
                using var clip = new SKPath();
                clip.AddRoundRect(dest, 10, 10);
                canvas.Save();
                canvas.ClipPath(clip, antialias: true);
                canvas.DrawBitmap(bmp, dest);
                canvas.Restore();

                using var imgBorder = new SKPaint { Color = new SKColor(51, 65, 85, 160), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
                canvas.DrawRoundRect(dest, 10, 10, imgBorder);
            }
        }
    }

    private static void RenderFinancesContent(SKCanvas canvas)
    {
        float cy = 120;
        float cardW = 680;
        float cardH = 670;
        float gap = 60;
        float startX = (FrameWidth - (2 * cardW + gap)) / 2f;

        // Card 1: Payments & Income in Philippine Pesos (₱)
        RenderCard(canvas, startX, cy, cardW, cardH, "Payments & Automatic Balance Deduction (₱)", new SKColor(16, 185, 129), () =>
        {
            float y = cy + 80;
            DrawFinancesItem(canvas, startX + 30, y, cardW - 60, "Bank Transfer (50% Down Payment)", "Brand Identity Redesign - Acme Corp", "+₱1,200.00", "Completed", new SKColor(16, 185, 129));
            y += 105;
            DrawFinancesItem(canvas, startX + 30, y, cardW - 60, "GCash / Maya (Milestone Payment)", "Web App UI Design - Nexus Tech", "+₱850.00", "Completed", new SKColor(16, 185, 129));
            y += 105;
            DrawFinancesItem(canvas, startX + 30, y, cardW - 60, "PayPal (Final Balance Settlement)", "Mobile App Icons - Studio Beta", "+₱450.00", "Completed", new SKColor(16, 185, 129));
            y += 105;
            DrawFinancesItem(canvas, startX + 30, y, cardW - 60, "Pending Down Payment", "E-Commerce Replatform - Aurora Labs", "₱600.00", "Awaiting Client", new SKColor(245, 158, 11));

            // Summary Footer
            y += 120;
            using var boxBg = new SKPaint { Color = new SKColor(30, 41, 59), IsAntialias = true };
            canvas.DrawRoundRect(startX + 30, y, cardW - 60, 80, 10, 10, boxBg);

            using var sumLabel = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 14, FakeBoldText = true, IsAntialias = true };
            using var sumVal = new SKPaint { Color = new SKColor(52, 211, 153), TextSize = 28, FakeBoldText = true, IsAntialias = true };
            canvas.DrawText("TOTAL RECORDED INCOME (THIS MONTH)", startX + 50, y + 32, sumLabel);
            canvas.DrawText("₱4,820.00", startX + 50, y + 66, sumVal);
        });

        // Card 2: Expenses & Recurring Subscriptions in Philippine Pesos (₱)
        RenderCard(canvas, startX + cardW + gap, cy, cardW, cardH, "Business Expenses & Recurring Write-offs (₱)", new SKColor(239, 68, 68), () =>
        {
            float y = cy + 80;
            DrawFinancesItem(canvas, startX + cardW + gap + 30, y, cardW - 60, "Adobe Creative Cloud", "Software / Monthly Subscription", "-₱3,050.00", "Recurring", new SKColor(239, 68, 68));
            y += 105;
            DrawFinancesItem(canvas, startX + cardW + gap + 30, y, cardW - 60, "Figma Professional Plan", "Design Tools / Monthly Subscription", "-₱850.00", "Recurring", new SKColor(239, 68, 68));
            y += 105;
            DrawFinancesItem(canvas, startX + cardW + gap + 30, y, cardW - 60, "Domain & Cloud Hosting", "Infrastructure / Tax Deductible", "-₱2,150.00", "Tax Write-off", new SKColor(59, 130, 246));
            y += 105;
            DrawFinancesItem(canvas, startX + cardW + gap + 30, y, cardW - 60, "Ergonomic Workspace Equipment", "Office Hardware / Tax Deductible", "-₱10,500.00", "One-Time", new SKColor(245, 158, 11));

            // Summary Footer
            y += 120;
            using var boxBg = new SKPaint { Color = new SKColor(30, 41, 59), IsAntialias = true };
            canvas.DrawRoundRect(startX + cardW + gap + 30, y, cardW - 60, 80, 10, 10, boxBg);

            using var sumLabel = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 14, FakeBoldText = true, IsAntialias = true };
            using var sumVal = new SKPaint { Color = new SKColor(96, 165, 250), TextSize = 28, FakeBoldText = true, IsAntialias = true };
            canvas.DrawText("TRUE NET PROFIT (INCOME MINUS EXPENSES)", startX + cardW + gap + 50, y + 32, sumLabel);
            canvas.DrawText("+₱4,522.61", startX + cardW + gap + 50, y + 66, sumVal);
        });
    }

    private static void DrawFinancesItem(SKCanvas canvas, float x, float y, float w, string title, string sub, string amount, string badge, SKColor badgeColor)
    {
        using var itemBg = new SKPaint { Color = new SKColor(22, 32, 54), IsAntialias = true };
        canvas.DrawRoundRect(x, y, w, 82, 8, 8, itemBg);

        using var titlePaint = new SKPaint { Color = SKColors.White, TextSize = 17, FakeBoldText = true, IsAntialias = true };
        using var subPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 13, IsAntialias = true };
        using var amtPaint = new SKPaint { Color = SKColors.White, TextSize = 20, FakeBoldText = true, IsAntialias = true };

        canvas.DrawText(title, x + 20, y + 32, titlePaint);
        canvas.DrawText(sub, x + 20, y + 58, subPaint);

        float amtW = amtPaint.MeasureText(amount);
        canvas.DrawText(amount, x + w - amtW - 20, y + 34, amtPaint);

        // Status Badge
        using var bText = new SKPaint { Color = badgeColor, TextSize = 11, FakeBoldText = true, IsAntialias = true };
        float bw = bText.MeasureText(badge) + 16;
        float bx = x + w - bw - 20;
        using var bBg = new SKPaint { Color = badgeColor.WithAlpha(30), IsAntialias = true };
        canvas.DrawRoundRect(bx, y + 46, bw, 22, 4, 4, bBg);
        canvas.DrawText(badge, bx + 8, y + 62, bText);
    }

    private static void RenderCard(SKCanvas canvas, float x, float y, float w, float h, string title, SKColor accent, Action drawBody)
    {
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 200), ImageFilter = SKImageFilter.CreateDropShadow(0, 18, 40, 40, new SKColor(0, 0, 0, 220)) })
        using (var bg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        using (var border = new SKPaint { Color = new SKColor(51, 65, 85, 140), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawRoundRect(x, y, w, h, 14, 14, shadow);
            canvas.DrawRoundRect(x, y, w, h, 14, 14, bg);
            canvas.DrawRoundRect(x, y, w, h, 14, 14, border);
        }

        // Header Banner
        using var hPaint = new SKPaint { Color = SKColors.White, TextSize = 20, FakeBoldText = true, IsAntialias = true };
        canvas.DrawText(title, x + 24, y + 42, hPaint);

        using var line = new SKPaint { Color = new SKColor(51, 65, 85, 120), StrokeWidth = 1, IsAntialias = true };
        canvas.DrawLine(x, y + 60, x + w, y + 60, line);

        drawBody();
    }

    private static void RenderPrivacyContent(SKCanvas canvas)
    {
        float cy = 160;

        using (var titlePaint = new SKPaint { Color = SKColors.White, TextSize = 42, FakeBoldText = true, IsAntialias = true })
        {
            string t = "100% Offline • Local-First Architecture";
            float w = titlePaint.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, cy, titlePaint);
        }

        using (var subPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 20, IsAntialias = true })
        {
            string st = "Your financial data stays exclusively on your machine — zero cloud lock-in.";
            float w = subPaint.MeasureText(st);
            canvas.DrawText(st, (FrameWidth - w) / 2f, cy + 40, subPaint);
        }

        float cardW = 340;
        float cardH = 340;
        float gap = 30;
        float startX = (FrameWidth - (4 * cardW + 3 * gap)) / 2f;
        float cardY = cy + 110;

        var items = new[]
        {
            new { Title = "Local SQLite Engine", Desc = "Everything is saved to a fast, standalone SQLite database file stored right inside your local user directory.", Tag = "Fast & Private", Color = new SKColor(59, 130, 246) },
            new { Title = "Rotating Backups", Desc = "Automated rotating backups trigger whenever you exit, keeping your financial records safe from hardware failure.", Tag = "Automatic on Exit", Color = new SKColor(16, 185, 129) },
            new { Title = "Full JSON Export", Desc = "One-click export and import allows you to move your entire workspace to a new computer or laptop in seconds.", Tag = "Zero Vendor Lock-in", Color = new SKColor(245, 158, 11) },
            new { Title = "Zero Telemetry", Desc = "No tracking pixels, no analytics pings, no cloud accounts, and no third-party servers peering into your revenue.", Tag = "100% Confidential", Color = new SKColor(168, 85, 247) },
        };

        for (int i = 0; i < 4; i++)
        {
            float x = startX + i * (cardW + gap);
            var item = items[i];

            using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 160), ImageFilter = SKImageFilter.CreateDropShadow(0, 14, 30, 30, new SKColor(0, 0, 0, 180)) };
            using var bg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true };
            using var border = new SKPaint { Color = new SKColor(51, 65, 85, 160), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };

            canvas.DrawRoundRect(x, cardY, cardW, cardH, 14, 14, shadow);
            canvas.DrawRoundRect(x, cardY, cardW, cardH, 14, 14, bg);
            canvas.DrawRoundRect(x, cardY, cardW, cardH, 14, 14, border);

            using var tPaint = new SKPaint { Color = SKColors.White, TextSize = 20, FakeBoldText = true, IsAntialias = true };
            canvas.DrawText(item.Title, x + 24, cardY + 50, tPaint);

            using var dPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 15, IsAntialias = true };
            DrawWrappedText(canvas, item.Desc, x + 24, cardY + 95, cardW - 48, 24, dPaint);

            using var tagText = new SKPaint { Color = item.Color, TextSize = 12, FakeBoldText = true, IsAntialias = true };
            float tw = tagText.MeasureText(item.Tag) + 16;
            using var tagBg = new SKPaint { Color = item.Color.WithAlpha(30), IsAntialias = true };
            canvas.DrawRoundRect(x + 24, cardY + cardH - 50, tw, 28, 6, 6, tagBg);
            canvas.DrawText(item.Tag, x + 32, cardY + cardH - 31, tagText);
        }
    }

    private static void RenderOutroContent(SKCanvas canvas, string rootDir, string logoPath)
    {
        float cy = 120;
        float cardW = 1380;
        float cardH = 670;
        float cardX = (FrameWidth - cardW) / 2f;

        using (var shadow = new SKPaint
        {
            Color = new SKColor(37, 99, 235, 140),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 24, 48, 48, new SKColor(37, 99, 235, 120))
        })
        {
            canvas.DrawRoundRect(cardX, cy, cardW, cardH, 20, 20, shadow);
        }

        using (var bg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        {
            canvas.DrawRoundRect(cardX, cy, cardW, cardH, 20, 20, bg);
        }
        using (var border = new SKPaint { Color = new SKColor(59, 130, 246, 120), IsStroke = true, StrokeWidth = 2, IsAntialias = true })
        {
            canvas.DrawRoundRect(cardX, cy, cardW, cardH, 20, 20, border);
        }

        // Draw Official Logo in Center Top of Outro Card
        float logoSize = 68;
        float logoX = (FrameWidth - logoSize) / 2f;
        float logoY = cy + 28;
        if (File.Exists(logoPath))
        {
            using var logoBmp = SKBitmap.Decode(logoPath);
            if (logoBmp != null)
            {
                var logoRect = new SKRect(logoX, logoY, logoX + logoSize, logoY + logoSize);
                using var clip = new SKPath();
                clip.AddRoundRect(logoRect, 16, 16);
                canvas.Save();
                canvas.ClipPath(clip, antialias: true);
                canvas.DrawBitmap(logoBmp, logoRect);
                canvas.Restore();

                using var lBorder = new SKPaint { Color = new SKColor(59, 130, 246, 160), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
                canvas.DrawRoundRect(logoRect, 16, 16, lBorder);
            }
        }

        float textY = logoY + logoSize + 36;

        using (var h1 = new SKPaint { Color = SKColors.White, TextSize = 36, FakeBoldText = true, IsAntialias = true })
        {
            string t = "Need Custom Websites, Software Applications, or Tools?";
            float w = h1.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, textY, h1);
        }

        textY += 32;

        using (var h2 = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 18, IsAntialias = true })
        {
            string t = "Work Directly with Roderic Villena • Freelancer since 2023 & Creator of FCMS Pro";
            float w = h2.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, textY, h2);
        }

        textY += 36;

        // Big Prominent Messenger Call-to-Action Card for Clients
        float msgW = 1080;
        float msgH = 140;
        float msgX = (FrameWidth - msgW) / 2f;

        using (var msgBg = new SKPaint { Color = new SKColor(14, 165, 233, 24), IsAntialias = true })
        using (var msgBorder = new SKPaint { Color = new SKColor(56, 189, 248), IsStroke = true, StrokeWidth = 2, IsAntialias = true })
        {
            canvas.DrawRoundRect(msgX, textY, msgW, msgH, 16, 16, msgBg);
            canvas.DrawRoundRect(msgX, textY, msgW, msgH, 16, 16, msgBorder);
        }

        using (var msgTitle = new SKPaint { Color = new SKColor(56, 189, 248), TextSize = 25, FakeBoldText = true, IsAntialias = true })
        {
            string t = "Contact Me Directly on Facebook Messenger: Roderic Villena";
            float w = msgTitle.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, textY + 44, msgTitle);
        }

        using (var msgSub = new SKPaint { Color = new SKColor(226, 232, 240), TextSize = 17, IsAntialias = true })
        {
            string t = "Let's build your next custom web app, desktop solution, or automated business system!";
            float w = msgSub.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, textY + 80, msgSub);
        }

        using (var msgTag = new SKPaint { Color = new SKColor(16, 185, 129), TextSize = 15, FakeBoldText = true, IsAntialias = true })
        {
            string t = "Open for Client Work & Custom Software Development";
            float w = msgTag.MeasureText(t);
            canvas.DrawText(t, (FrameWidth - w) / 2f, textY + 114, msgTag);
        }

        textY += msgH + 40;

        // Download Project Link Box
        float boxW = 860;
        float boxH = 74;
        float boxX = (FrameWidth - boxW) / 2f;

        using (var boxBg = new SKPaint { Color = new SKColor(37, 99, 235, 35), IsAntialias = true })
        using (var boxBorder = new SKPaint { Color = new SKColor(59, 130, 246, 180), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawRoundRect(boxX, textY, boxW, boxH, 12, 12, boxBg);
            canvas.DrawRoundRect(boxX, textY, boxW, boxH, 12, 12, boxBorder);
        }

        using (var dlPaint = new SKPaint { Color = new SKColor(56, 189, 248), TextSize = 28, FakeBoldText = true, IsAntialias = true })
        {
            string url = "Download FCMS Pro: https://villenaderic.github.io/FcmsPro/";
            float w = dlPaint.MeasureText(url);
            canvas.DrawText(url, (FrameWidth - w) / 2f, textY + 46, dlPaint);
        }

        textY += boxH + 34;

        using (var ghPaint = new SKPaint { Color = new SKColor(226, 232, 240), TextSize = 20, FakeBoldText = true, IsAntialias = true })
        {
            string repo = "GitHub: github.com/villenaderic/FcmsPro  •  100% Free & Open Source";
            float w = ghPaint.MeasureText(repo);
            canvas.DrawText(repo, (FrameWidth - w) / 2f, textY, ghPaint);
        }
    }

    private static void RenderSubtitleBar(SKCanvas canvas, string narration)
    {
        float barW = 1620;
        float barH = 80;
        float barX = (FrameWidth - barW) / 2f;
        float barY = FrameHeight - barH - 75; // Safe title margin 75px from bottom edge (clear of player controls)

        // Subtitle Badge Glow & Shadow
        using (var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 200),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 10, 24, 24, new SKColor(0, 0, 0, 220))
        })
        {
            canvas.DrawRoundRect(barX, barY, barW, barH, 18, 18, shadow);
        }

        // Subtitle Badge Background & Blue Accent Glow Border
        using (var bg = new SKPaint { Color = new SKColor(10, 15, 28, 245), IsAntialias = true })
        using (var border = new SKPaint { Color = new SKColor(59, 130, 246, 120), IsStroke = true, StrokeWidth = 1.8f, IsAntialias = true })
        {
            canvas.DrawRoundRect(barX, barY, barW, barH, 18, 18, bg);
            canvas.DrawRoundRect(barX, barY, barW, barH, 18, 18, border);
        }

        // Dynamically choose font size so text never overflows
        float fontSize = narration.Length > 200 ? 17f : (narration.Length > 120 ? 19f : 22f);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = fontSize,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Break into lines fitting inside barW - 80
        var words = narration.Split(' ');
        var lines = new List<string>();
        string current = "";

        foreach (var word in words)
        {
            string test = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (textPaint.MeasureText(test) <= barW - 80)
            {
                current = test;
            }
            else
            {
                if (!string.IsNullOrEmpty(current)) lines.Add(current);
                current = word;
            }
        }
        if (!string.IsNullOrEmpty(current)) lines.Add(current);

        float lineSpacing = fontSize + 8;
        float totalTextH = lines.Count * lineSpacing;
        float startY = barY + (barH - totalTextH) / 2f + fontSize;

        for (int i = 0; i < lines.Count; i++)
        {
            float tw = textPaint.MeasureText(lines[i]);
            canvas.DrawText(lines[i], (FrameWidth - tw) / 2f, startY + (i * lineSpacing), textPaint);
        }
    }

    private static void DrawWrappedText(SKCanvas canvas, string text, float x, float y, float maxWidth, float lineHeight, SKPaint paint)
    {
        var words = text.Split(' ');
        var currentLine = "";
        float curY = y;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var width = paint.MeasureText(testLine);
            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                canvas.DrawText(currentLine, x, curY, paint);
                currentLine = word;
                curY += lineHeight;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            canvas.DrawText(currentLine, x, curY, paint);
        }
    }

    private static void GenerateSearchScreenshot(string screenshotsDir, string rootDir)
    {
        var dashPath = Path.Combine(screenshotsDir, "dashboard.png");
        if (!File.Exists(dashPath)) return;

        using var dashBmp = SKBitmap.Decode(dashPath);
        if (dashBmp == null) return;

        int width = dashBmp.Width;
        int height = dashBmp.Height;

        using var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);

        // 1. Draw Dashboard Background
        canvas.DrawBitmap(dashBmp, 0, 0);

        // 2. Dim Scrim
        using (var scrim = new SKPaint { Color = new SKColor(0, 0, 0, 165), IsAntialias = true })
        {
            canvas.DrawRect(0, 0, width, height, scrim);
        }

        // 3. Centered Search Modal Window
        float modalW = Math.Min(620, width - 60);
        float modalH = Math.Min(430, height - 60);
        float modalX = (width - modalW) / 2f;
        float modalY = (height - modalH) / 2f;

        // Drop shadow
        using (var shadow = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 220),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 16, 36, 36, new SKColor(0, 0, 0, 230))
        })
        {
            canvas.DrawRoundRect(modalX, modalY, modalW, modalH, 14, 14, shadow);
        }

        // Modal Background
        using (var modalBg = new SKPaint { Color = new SKColor(15, 23, 42), IsAntialias = true })
        using (var modalBorder = new SKPaint { Color = new SKColor(59, 130, 246, 180), IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
        {
            canvas.DrawRoundRect(modalX, modalY, modalW, modalH, 14, 14, modalBg);
            canvas.DrawRoundRect(modalX, modalY, modalW, modalH, 14, 14, modalBorder);
        }

        // 4. Search Input Box
        float inputX = modalX + 18;
        float inputY = modalY + 18;
        float inputW = modalW - 36;
        float inputH = 46;

        using (var inputBg = new SKPaint { Color = new SKColor(30, 41, 59), IsAntialias = true })
        using (var inputBorder = new SKPaint { Color = new SKColor(59, 130, 246), IsStroke = true, StrokeWidth = 1.6f, IsAntialias = true })
        {
            canvas.DrawRoundRect(inputX, inputY, inputW, inputH, 8, 8, inputBg);
            canvas.DrawRoundRect(inputX, inputY, inputW, inputH, 8, 8, inputBorder);
        }

        // Search text & blinking cursor
        using (var labelPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 14, IsAntialias = true })
        {
            canvas.DrawText("Search:", inputX + 14, inputY + 28, labelPaint);
        }
        using (var queryText = new SKPaint { Color = SKColors.White, TextSize = 16, FakeBoldText = true, IsAntialias = true })
        {
            canvas.DrawText("Logo", inputX + 74, inputY + 29, queryText);
        }
        using (var cursor = new SKPaint { Color = new SKColor(56, 189, 248), StrokeWidth = 2, IsAntialias = true })
        {
            canvas.DrawLine(inputX + 118, inputY + 14, inputX + 118, inputY + 32, cursor);
        }

        // ESC shortcut badge
        using (var escBg = new SKPaint { Color = new SKColor(51, 65, 85), IsAntialias = true })
        using (var escText = new SKPaint { Color = new SKColor(226, 232, 240), TextSize = 10, FakeBoldText = true, IsAntialias = true })
        {
            var escRect = new SKRect(inputX + inputW - 46, inputY + 12, inputX + inputW - 12, inputY + 34);
            canvas.DrawRoundRect(escRect, 4, 4, escBg);
            canvas.DrawText("ESC", escRect.Left + 7, escRect.Top + 15, escText);
        }

        // 5. Results List
        float curY = inputY + inputH + 16;

        // Section: COMMISSIONS
        DrawSearchSectionHeader(canvas, inputX, ref curY, "COMMISSIONS", new SKColor(96, 165, 250));
        DrawSearchResultItem(canvas, inputX, ref curY, inputW, "Logo Redesign & Brand Assets", "Sample Client C • ₱3,000.00 • In Progress", "In Progress", new SKColor(59, 130, 246), isSelected: true);
        DrawSearchResultItem(canvas, inputX, ref curY, inputW, "Logo Style Sheet & Vector Exports", "Sample Client D • ₱2,150.00 • Delivered", "Delivered", new SKColor(16, 185, 129), isSelected: false);

        // Section: CLIENTS
        DrawSearchSectionHeader(canvas, inputX, ref curY, "CLIENTS", new SKColor(52, 211, 153));
        DrawSearchResultItem(canvas, inputX, ref curY, inputW, "LogoCraft Creative Studio", "Active Client • 3 Commissions • Manila, PH", "Client", new SKColor(168, 85, 247), isSelected: false);

        // Section: EXPENSES
        DrawSearchSectionHeader(canvas, inputX, ref curY, "EXPENSES", new SKColor(251, 146, 60));
        DrawSearchResultItem(canvas, inputX, ref curY, inputW, "Logo Typography Commercial License", "Design Software & Assets • ₱1,500.00", "Expense", new SKColor(249, 115, 22), isSelected: false);

        // 6. Bottom Helper Hint
        using (var hintPaint = new SKPaint { Color = new SKColor(100, 116, 139), TextSize = 11, IsAntialias = true })
        using (var divPaint = new SKPaint { Color = new SKColor(30, 41, 59), StrokeWidth = 1 })
        {
            canvas.DrawLine(modalX + 18, modalY + modalH - 26, modalX + modalW - 18, modalY + modalH - 26, divPaint);
            string hint = "Press Up/Down to navigate • Enter to select • Esc to close";
            float hw = hintPaint.MeasureText(hint);
            canvas.DrawText(hint, modalX + (modalW - hw) / 2f, modalY + modalH - 10, hintPaint);
        }

        var targets = new[]
        {
            Path.Combine(screenshotsDir, "search.png"),
            Path.Combine(rootDir, "docs", "assets", "img", "screenshots", "search.png"),
            Path.Combine(rootDir, "_site", "assets", "img", "screenshots", "search.png")
        };

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        foreach (var t in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(t)!);
            using var fs = File.Create(t);
            data.SaveTo(fs);
            Console.WriteLine($"  -> Saved crisp Global Search screenshot to {t}");
        }
    }

    private static void DrawSearchSectionHeader(SKCanvas canvas, float x, ref float y, string title, SKColor color)
    {
        using var paint = new SKPaint { Color = color, TextSize = 10, FakeBoldText = true, IsAntialias = true };
        canvas.DrawText(title, x + 6, y + 10, paint);
        y += 16;
    }

    private static void DrawSearchResultItem(SKCanvas canvas, float x, ref float y, float width, string title, string subtitle, string pillText, SKColor pillColor, bool isSelected)
    {
        float itemH = 46;
        var rect = new SKRect(x, y, x + width, y + itemH);

        if (isSelected)
        {
            using var selBg = new SKPaint { Color = new SKColor(37, 99, 235, 45), IsAntialias = true };
            using var selBorder = new SKPaint { Color = new SKColor(59, 130, 246, 140), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
            using var selBar = new SKPaint { Color = new SKColor(59, 130, 246), IsAntialias = true };
            canvas.DrawRoundRect(rect, 6, 6, selBg);
            canvas.DrawRoundRect(rect, 6, 6, selBorder);
            canvas.DrawRoundRect(new SKRect(x, y + 6, x + 3.5f, y + itemH - 6), 2, 2, selBar);
        }

        using var titlePaint = new SKPaint { Color = SKColors.White, TextSize = 13, FakeBoldText = true, IsAntialias = true };
        using var subPaint = new SKPaint { Color = new SKColor(148, 163, 184), TextSize = 11, IsAntialias = true };

        canvas.DrawText(title, x + 16, y + 18, titlePaint);
        canvas.DrawText(subtitle, x + 16, y + 36, subPaint);

        // Pill on the right
        using var pBg = new SKPaint { Color = new SKColor(pillColor.Red, pillColor.Green, pillColor.Blue, 35), IsAntialias = true };
        using var pBorder = new SKPaint { Color = new SKColor(pillColor.Red, pillColor.Green, pillColor.Blue, 180), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
        using var pText = new SKPaint { Color = pillColor, TextSize = 10, FakeBoldText = true, IsAntialias = true };

        float pw = pText.MeasureText(pillText) + 16;
        float ph = 22;
        var pRect = new SKRect(x + width - pw - 12, y + (itemH - ph) / 2f, x + width - 12, y + (itemH + ph) / 2f);
        canvas.DrawRoundRect(pRect, 4, 4, pBg);
        canvas.DrawRoundRect(pRect, 4, 4, pBorder);
        canvas.DrawText(pillText, pRect.Left + 8, pRect.Top + 15, pText);

        y += itemH + 6;
    }
}
