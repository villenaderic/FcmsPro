using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FcmsPro.Data;

public static class FcmsPaths
{
    /// <summary>
    /// Resolves the per-OS app data directory:
    ///   Windows: %APPDATA%\FcmsPro
    ///   macOS:   ~/Library/Application Support/FcmsPro
    ///   Linux:   ~/.local/share/FcmsPro (XDG_DATA_HOME if set)
    /// Created if it doesn't exist.
    /// </summary>
    public static string GetAppDataDirectory()
    {
        var baseDir = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        // On Linux, ApplicationData maps to ~/.config by default under .NET;
        // XDG_DATA_HOME (or ~/.local/share) is more appropriate for app data.
        if (OperatingSystem.IsLinux())
        {
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrWhiteSpace(xdgData)
                ? xdgData
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        var dir = Path.Combine(baseDir, "FcmsPro");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetDatabasePath() => Path.Combine(GetAppDataDirectory(), "fcms.db");

    public static string GetLogsDirectory()
    {
        var dir = Path.Combine(GetAppDataDirectory(), "logs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetBackupsDirectory()
    {
        var dir = Path.Combine(GetAppDataDirectory(), "backups");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Per-commission subfolder for CommissionAttachment files (reference
    /// images, delivered artwork, etc.) - one folder per commission so
    /// deleting a commission's attachments is a single directory delete
    /// rather than hunting individual files by Id.
    /// </summary>
    public static string GetAttachmentsDirectory(Guid commissionId)
    {
        var dir = Path.Combine(GetAppDataDirectory(), "attachments", commissionId.ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Same one-folder-per-entity pattern as GetAttachmentsDirectory above,
    /// generalized to Client/Invoice/Quote attachments (round 8 originally
    /// only covered Commission). Nested under "attachments/clients|invoices/quotes/"
    /// rather than reusing the flat "attachments/{id}" layout Commission uses -
    /// that flat layout is left exactly as-is for backward compatibility with
    /// any already-uploaded commission attachments, and namespacing the new
    /// ones by entity type also rules out a same-Guid collision between e.g.
    /// a commission and an unrelated invoice sharing an id (astronomically
    /// unlikely with GUIDs, but free to rule out entirely here).
    /// </summary>
    public static string GetClientAttachmentsDirectory(Guid clientId) =>
        GetNamespacedAttachmentsDirectory("clients", clientId);

    public static string GetInvoiceAttachmentsDirectory(Guid invoiceId) =>
        GetNamespacedAttachmentsDirectory("invoices", invoiceId);

    public static string GetQuoteAttachmentsDirectory(Guid quoteId) =>
        GetNamespacedAttachmentsDirectory("quotes", quoteId);

    private static string GetNamespacedAttachmentsDirectory(string entityFolder, Guid entityId)
    {
        var dir = Path.Combine(GetAppDataDirectory(), "attachments", entityFolder, entityId.ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public static class FcmsDbContextOptionsFactory
{
    public static DbContextOptions<FcmsDbContext> Create(string? dbPathOverride = null)
    {
        var dbPath = dbPathOverride ?? FcmsPaths.GetDatabasePath();
        var builder = new DbContextOptionsBuilder<FcmsDbContext>();
        // WAL mode set via a raw pragma the first time the connection opens -
        // see FcmsDbContext startup call in App composition root (Phase 3 Avalonia).
        builder.UseSqlite($"Data Source={dbPath};Cache=Shared");
        return builder.Options;
    }
}

/// <summary>
/// Used only by `dotnet ef migrations add ...` at design time - not referenced
/// by the running app, which uses FcmsDbContextOptionsFactory + DI instead.
/// </summary>
public class FcmsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FcmsDbContext>
{
    public FcmsDbContext CreateDbContext(string[] args)
    {
        var options = FcmsDbContextOptionsFactory.Create(dbPathOverride: "design_time.db");
        return new FcmsDbContext(options);
    }
}
