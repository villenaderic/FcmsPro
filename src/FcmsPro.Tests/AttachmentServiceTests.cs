using FcmsPro.Core.Entities;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers AttachmentService after its generalization beyond Commission -
/// exercises the shared AddCoreAsync/RemoveCoreAsync logic through each of
/// the four AddForXAsync entry points, plus validation (extension, size,
/// missing source file) which only needs to be proven once since it's the
/// same code path underneath all four.
/// </summary>
public class AttachmentServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public AttachmentServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "fcms-attachment-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private string CreateSourceFile(string fileName, int sizeBytes = 100)
    {
        var path = Path.Combine(_tempRoot, fileName);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        return path;
    }

    private static Mock<IUnitOfWork> BuildMockUow(
        out Mock<IClientAttachmentRepository> clientAttachments,
        out Mock<IInvoiceAttachmentRepository> invoiceAttachments,
        out Mock<IQuoteAttachmentRepository> quoteAttachments,
        out Mock<ICommissionAttachmentRepository> commissionAttachments)
    {
        var uow = new Mock<IUnitOfWork>();
        clientAttachments = new Mock<IClientAttachmentRepository>();
        invoiceAttachments = new Mock<IInvoiceAttachmentRepository>();
        quoteAttachments = new Mock<IQuoteAttachmentRepository>();
        commissionAttachments = new Mock<ICommissionAttachmentRepository>();

        uow.SetupGet(u => u.ClientAttachments).Returns(clientAttachments.Object);
        uow.SetupGet(u => u.InvoiceAttachments).Returns(invoiceAttachments.Object);
        uow.SetupGet(u => u.QuoteAttachments).Returns(quoteAttachments.Object);
        uow.SetupGet(u => u.CommissionAttachments).Returns(commissionAttachments.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return uow;
    }

    [Fact]
    public async Task AddForClientAsync_CopiesFileAndPersistsMetadata()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var clientId = Guid.NewGuid();
        var source = CreateSourceFile("contract.pdf", 500);
        var targetDir = Path.Combine(_tempRoot, "target");

        var result = await service.AddForClientAsync(clientId, source, targetDir);

        Assert.Equal(clientId, result.ClientId);
        Assert.Equal("contract.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(500, result.FileSizeBytes);
        Assert.EndsWith(".pdf", result.StoredFileName);
        Assert.NotEqual("contract.pdf", result.StoredFileName); // stored under a fresh Guid name, not the original
        Assert.True(File.Exists(Path.Combine(targetDir, result.StoredFileName)));
        clientAttachments.Verify(r => r.AddAsync(It.IsAny<ClientAttachment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddForInvoiceAsync_AcceptsImageFiles()
    {
        var uow = BuildMockUow(out _, out var invoiceAttachments, out _, out _);
        var service = new AttachmentService(uow.Object);
        var invoiceId = Guid.NewGuid();
        var source = CreateSourceFile("mockup.png");
        var targetDir = Path.Combine(_tempRoot, "target");

        var result = await service.AddForInvoiceAsync(invoiceId, source, targetDir);

        Assert.Equal(invoiceId, result.InvoiceId);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public async Task AddForQuoteAsync_SetsCaptionWhenProvided()
    {
        var uow = BuildMockUow(out _, out _, out var quoteAttachments, out _);
        var service = new AttachmentService(uow.Object);
        var quoteId = Guid.NewGuid();
        var source = CreateSourceFile("reference.jpg");
        var targetDir = Path.Combine(_tempRoot, "target");

        var result = await service.AddForQuoteAsync(quoteId, source, targetDir, caption: "Client's reference sketch");

        Assert.Equal("Client's reference sketch", result.Caption);
    }

    [Fact]
    public async Task AddAsync_RejectsUnsupportedExtension()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var source = CreateSourceFile("malware.exe");

        await Assert.ThrowsAsync<AttachmentService.AttachmentValidationException>(
            () => service.AddForClientAsync(Guid.NewGuid(), source, Path.Combine(_tempRoot, "target")));

        clientAttachments.Verify(r => r.AddAsync(It.IsAny<ClientAttachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_RejectsFileOverSizeLimit()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var source = CreateSourceFile("huge.png", sizeBytes: 11 * 1024 * 1024); // 11 MB, over the 10 MB limit

        var ex = await Assert.ThrowsAsync<AttachmentService.AttachmentValidationException>(
            () => service.AddForClientAsync(Guid.NewGuid(), source, Path.Combine(_tempRoot, "target")));

        Assert.Contains("10 MB", ex.Message);
        clientAttachments.Verify(r => r.AddAsync(It.IsAny<ClientAttachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_RejectsMissingSourceFile()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var missingPath = Path.Combine(_tempRoot, "does-not-exist.png");

        await Assert.ThrowsAsync<AttachmentService.AttachmentValidationException>(
            () => service.AddForClientAsync(Guid.NewGuid(), missingPath, Path.Combine(_tempRoot, "target")));
    }

    [Fact]
    public async Task AddAsync_AcceptsPdf_ForAllEntityTypes()
    {
        // .pdf was added when this service was generalized beyond Commission
        // (a signed contract or reference document) - confirm it's accepted
        // on every AddFor*Async entry point, not just the new ones.
        var uow = BuildMockUow(out _, out _, out _, out var commissionAttachments);
        var service = new AttachmentService(uow.Object);
        var source = CreateSourceFile("reference-sheet.pdf");

        var result = await service.AddForCommissionAsync(Guid.NewGuid(), source, Path.Combine(_tempRoot, "target"));

        Assert.Equal("application/pdf", result.ContentType);
        commissionAttachments.Verify(r => r.AddAsync(It.IsAny<CommissionAttachment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_DeletesMetadataAndOnDiskFile()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var targetDir = Path.Combine(_tempRoot, "target");
        var added = await service.AddForClientAsync(Guid.NewGuid(), CreateSourceFile("doc.pdf"), targetDir);
        var storedPath = Path.Combine(targetDir, added.StoredFileName);
        Assert.True(File.Exists(storedPath));

        await service.RemoveAsync(added, targetDir);

        Assert.False(File.Exists(storedPath));
        clientAttachments.Verify(r => r.Remove(added), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_MissingOnDiskFile_DoesNotThrow()
    {
        var uow = BuildMockUow(out var clientAttachments, out _, out _, out _);
        var service = new AttachmentService(uow.Object);
        var attachment = new ClientAttachment { ClientId = Guid.NewGuid(), StoredFileName = "already-gone.pdf" };

        // File was never actually created on disk - RemoveAsync should still
        // succeed since the DB row is the source of truth, not the file.
        await service.RemoveAsync(attachment, Path.Combine(_tempRoot, "target"));

        clientAttachments.Verify(r => r.Remove(attachment), Times.Once);
    }
}
