using System.Text.Json;
using FcmsPro.Core.Backup;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers BackupService - flagged as having zero automated tests despite
/// running unattended on every app close (MainWindow.Closing) with a
/// deliberate "swallow errors, never block shutdown" policy. That policy is
/// exactly the kind of thing worth pinning down with a test: it's easy to
/// silently break the try/catch and not notice, since the code "still runs"
/// either way in manual testing.
/// </summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public BackupServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "fcms-backup-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static Mock<IUnitOfWork> BuildMockUow()
    {
        var uow = new Mock<IUnitOfWork>();
        var clients = new Mock<IClientRepository>();
        var commissions = new Mock<ICommissionRepository>();
        var payments = new Mock<IPaymentRepository>();
        var invoices = new Mock<IInvoiceRepository>();
        var quotes = new Mock<IQuoteRepository>();
        var expenses = new Mock<IExpenseRepository>();
        var templates = new Mock<ITemplateRepository>();
        var auditLogs = new Mock<IAuditLogRepository>();
        var settings = new Mock<ISettingsRepository>();

        uow.SetupGet(u => u.Clients).Returns(clients.Object);
        uow.SetupGet(u => u.Commissions).Returns(commissions.Object);
        uow.SetupGet(u => u.Payments).Returns(payments.Object);
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.SetupGet(u => u.Quotes).Returns(quotes.Object);
        uow.SetupGet(u => u.Expenses).Returns(expenses.Object);
        uow.SetupGet(u => u.Templates).Returns(templates.Object);
        uow.SetupGet(u => u.AuditLogs).Returns(auditLogs.Object);
        uow.SetupGet(u => u.Settings).Returns(settings.Object);

        commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Commission>());
        clients.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Client>());
        payments.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Payment>());
        invoices.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Invoice>());
        quotes.Setup(q => q.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Quote>());
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Expense>());
        templates.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CommissionTemplate>());
        settings.Setup(s => s.GetGoalSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new GoalSettings());
        settings.Setup(s => s.GetAppSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AppSettings());
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((work, _) => work());

        return uow;
    }

    [Fact]
    public async Task ExportAllAsync_ProducesValidJsonWithMetadata()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);

        var json = await service.ExportAllAsync();
        var parsed = JsonSerializer.Deserialize<BackupExport>(json);

        Assert.NotNull(parsed);
        Assert.Equal("FcmsPro", parsed!.Meta.App);
        Assert.Equal(1, parsed.Meta.Version);
    }

    [Fact]
    public async Task ExportAllAsync_LogsAnAuditEntry()
    {
        var uow = BuildMockUow();
        var auditLogs = Mock.Get(uow.Object.AuditLogs);
        var service = new BackupService(uow.Object);

        await service.ExportAllAsync();

        auditLogs.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l => l.Message.Contains("backup", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteRotatingBackupAsync_WritesATimestampedFile()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);

        var path = await service.WriteRotatingBackupAsync(_tempRoot, keepCount: 5);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.StartsWith("auto-", Path.GetFileName(path));
    }

    [Fact]
    public async Task WriteRotatingBackupAsync_KeepsOnlyTheNewestNFiles()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);
        Directory.CreateDirectory(_tempRoot);

        // Pre-seed 5 older backup files (alphabetically-sortable names, same
        // convention WriteRotatingBackupAsync itself uses) before writing a
        // 6th through the service, with keepCount: 3.
        for (var i = 0; i < 5; i++)
        {
            var name = $"auto-2026010{i}-000000.json";
            File.WriteAllText(Path.Combine(_tempRoot, name), "{}");
            await Task.Delay(5); // avoid identical filename collisions across fast iterations
        }

        await service.WriteRotatingBackupAsync(_tempRoot, keepCount: 3);

        var remaining = Directory.GetFiles(_tempRoot, "auto-*.json");
        Assert.Equal(3, remaining.Length);
    }

    [Fact]
    public async Task WriteRotatingBackupAsync_KeepsNewestFilesNotOldest()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);
        Directory.CreateDirectory(_tempRoot);

        var oldestFile = Path.Combine(_tempRoot, "auto-20200101-000000.json");
        var newestFile = Path.Combine(_tempRoot, "auto-20991231-000000.json");
        File.WriteAllText(oldestFile, "{}");
        File.WriteAllText(newestFile, "{}");

        await service.WriteRotatingBackupAsync(_tempRoot, keepCount: 2);

        // keepCount 2 + the one just written = 3 files should all survive,
        // but if rotation ever gets the sort direction backwards, this is
        // the test that would catch it once keepCount drops below that.
        Assert.True(File.Exists(newestFile));
    }

    [Fact]
    public async Task WriteRotatingBackupAsync_NeverThrowsEvenOnFailure()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);

        // An invalid path (null byte) makes Directory.CreateDirectory throw
        // internally - WriteRotatingBackupAsync's documented contract is to
        // swallow that and return null, never propagate, since this runs
        // during app shutdown where an unhandled exception could block the
        // window from closing at all.
        var result = await service.WriteRotatingBackupAsync("\0invalid", keepCount: 5);

        Assert.Null(result);
    }

    [Fact]
    public async Task ImportAllAsync_ReplaceMode_RemovesExistingBeforeAdding()
    {
        var uow = BuildMockUow();
        var clients = Mock.Get(uow.Object.Clients);
        var existingClient = new Client { Id = Guid.NewGuid(), Name = "Old Client" };
        clients.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Client> { existingClient });

        var service = new BackupService(uow.Object);
        var export = new BackupExport
        {
            Clients = new List<Client> { new() { Id = Guid.NewGuid(), Name = "New Client" } }
        };
        var json = JsonSerializer.Serialize(export);

        await service.ImportAllAsync(json, ImportMode.Replace);

        clients.Verify(c => c.Remove(existingClient), Times.Once);
        clients.Verify(c => c.AddAsync(It.Is<Client>(cl => cl.Name == "New Client"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAllAsync_MergeMode_DoesNotRemoveExisting()
    {
        var uow = BuildMockUow();
        var clients = Mock.Get(uow.Object.Clients);
        var existingClient = new Client { Id = Guid.NewGuid(), Name = "Keep Me" };
        clients.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Client> { existingClient });

        var service = new BackupService(uow.Object);
        var export = new BackupExport
        {
            Clients = new List<Client> { new() { Id = Guid.NewGuid(), Name = "Imported Client" } }
        };
        var json = JsonSerializer.Serialize(export);

        await service.ImportAllAsync(json, ImportMode.Merge);

        clients.Verify(c => c.Remove(It.IsAny<Client>()), Times.Never);
        clients.Verify(c => c.AddAsync(It.Is<Client>(cl => cl.Name == "Imported Client"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAllAsync_MergeMode_NeverTouchesSettings()
    {
        // Matches documented PWA-compatibility behavior: settings are only
        // ever restored in Replace mode, never merged.
        var uow = BuildMockUow();
        var settings = Mock.Get(uow.Object.Settings);
        var service = new BackupService(uow.Object);
        var export = new BackupExport { Goals = new GoalSettings(), SettingsJson = JsonSerializer.Serialize(new AppSettings()) };

        await service.ImportAllAsync(JsonSerializer.Serialize(export), ImportMode.Merge);

        settings.Verify(s => s.SaveGoalSettingsAsync(It.IsAny<GoalSettings>(), It.IsAny<CancellationToken>()), Times.Never);
        settings.Verify(s => s.SaveAppSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportAllAsync_ReplaceMode_RestoresSettings()
    {
        var uow = BuildMockUow();
        var settings = Mock.Get(uow.Object.Settings);
        var service = new BackupService(uow.Object);
        var export = new BackupExport { Goals = new GoalSettings(), SettingsJson = JsonSerializer.Serialize(new AppSettings()) };

        await service.ImportAllAsync(JsonSerializer.Serialize(export), ImportMode.Replace);

        settings.Verify(s => s.SaveGoalSettingsAsync(It.IsAny<GoalSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        settings.Verify(s => s.SaveAppSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAllAsync_InvalidJson_ThrowsRatherThanSilentlyNoOp()
    {
        var uow = BuildMockUow();
        var service = new BackupService(uow.Object);

        await Assert.ThrowsAnyAsync<Exception>(() => service.ImportAllAsync("not valid json", ImportMode.Merge));
    }

    [Fact]
    public async Task ImportAllAsync_NullEntityList_IsSkippedNotCleared()
    {
        // A field genuinely absent from an older/partial backup file (e.g.
        // Templates never written) should leave existing data alone even in
        // Replace mode - only fields actually present in the import get
        // replaced. This is what ImportEntitiesAsync's `if (incoming is
        // null) return;` guard is for.
        //
        // Note this can't be forced via an omitted JSON property alone:
        // BackupExport's List<T> properties all have `= new()` initializers,
        // which System.Text.Json preserves for any property absent from the
        // JSON (the object is constructed - running those initializers -
        // then only present JSON fields overwrite them). So plain "{}" JSON
        // actually deserializes every list to *empty*, not null. To
        // exercise the null-guard for real, the null has to be explicit in
        // the JSON, which is what serializing a BackupExport with
        // Templates = null! (forced past the non-nullable annotation)
        // produces below.
        var uow = BuildMockUow();
        var templates = Mock.Get(uow.Object.Templates);
        var existingTemplate = new CommissionTemplate { Id = Guid.NewGuid() };
        templates.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CommissionTemplate> { existingTemplate });

        var service = new BackupService(uow.Object);
        var export = new BackupExport { Templates = null! };
        var json = JsonSerializer.Serialize(export);
        Assert.Contains("\"Templates\":null", json); // sanity-check the premise before trusting the assertions below

        await service.ImportAllAsync(json, ImportMode.Replace);

        templates.Verify(t => t.Remove(It.IsAny<CommissionTemplate>()), Times.Never);
        templates.Verify(t => t.AddAsync(It.IsAny<CommissionTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
