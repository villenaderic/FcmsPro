using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers GlobalSearchService: the "/" global search window's backing
/// service, and the first place in the app that searches across entity
/// types at once rather than one already-loaded list page at a time.
/// </summary>
public class GlobalSearchServiceTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IClientRepository> clients, Mock<ICommissionRepository> commissions,
        Mock<IExpenseRepository> expenses)
        BuildMockUow()
    {
        var uow = new Mock<IUnitOfWork>();
        var clients = new Mock<IClientRepository>();
        var commissions = new Mock<ICommissionRepository>();
        var expenses = new Mock<IExpenseRepository>();

        uow.SetupGet(u => u.Clients).Returns(clients.Object);
        uow.SetupGet(u => u.Commissions).Returns(commissions.Object);
        uow.SetupGet(u => u.Expenses).Returns(expenses.Object);

        clients.Setup(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Client>());
        commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Commission>());
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Expense>());

        return (uow, clients, commissions, expenses);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")] // single character - below the 2-char minimum
    public async Task Search_BelowMinimumLength_ReturnsEmptyWithoutQueryingAnything(string query)
    {
        var (uow, clients, commissions, expenses) = BuildMockUow();
        var service = new GlobalSearchService(uow.Object);

        var results = await service.SearchAsync(query);

        Assert.Empty(results);
        clients.Verify(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        commissions.Verify(c => c.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_MatchesClientByName()
    {
        var (uow, clients, _, _) = BuildMockUow();
        var client = new Client { Id = Guid.NewGuid(), Name = "Jordan Alvarez", Email = "jordan@example.com" };
        clients.Setup(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Client> { client });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync("jordan");

        var match = Assert.Single(results);
        Assert.Equal(GlobalSearchResultType.Client, match.Type);
        Assert.Equal(client.Id, match.Id);
        Assert.Equal(client.Id, match.ClientId); // Client results deep-link via their own Id
        Assert.Equal("Jordan Alvarez", match.Title);
    }

    [Fact]
    public async Task Search_ExcludesSoftDeletedClients()
    {
        var (uow, clients, _, _) = BuildMockUow();
        clients.Setup(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Client>
            {
                new() { Id = Guid.NewGuid(), Name = "Deleted Client", IsDeleted = true }
            });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync("deleted");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_MatchesCommissionByServiceTypeNotJustTitle()
    {
        var (uow, _, commissions, _) = BuildMockUow();
        var commission = new Commission
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Title = "Untitled piece",
            ServiceType = "Character Illustration",
            Status = CommissionStatus.InProgress
        };
        commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Commission> { commission });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync("illustration");

        var match = Assert.Single(results);
        Assert.Equal(GlobalSearchResultType.Commission, match.Type);
        Assert.Equal(commission.ClientId, match.ClientId); // used to deep-link via CommissionDetail
    }


    [Fact]
    public async Task Search_MatchesExpenseByDescription_AndHasNoClientId()
    {
        var (uow, _, _, expenses) = BuildMockUow();
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Description = "Adobe Creative Cloud subscription",
            Category = ExpenseCategory.SoftwareTools,
            Amount = 20m
        };
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Expense> { expense });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync("adobe");

        var match = Assert.Single(results);
        Assert.Equal(GlobalSearchResultType.Expense, match.Type);
        Assert.Null(match.ClientId); // expenses have no client relationship
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var (uow, clients, _, _) = BuildMockUow();
        clients.Setup(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Client> { new() { Id = Guid.NewGuid(), Name = "ALLCAPS CLIENT" } });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync("allcaps");

        Assert.Single(results);
    }

    [Fact]
    public async Task Search_CombinesMatchesAcrossMultipleEntityTypes()
    {
        var (uow, clients, commissions, expenses) = BuildMockUow();
        var sharedTerm = "retainer";

        clients.Setup(c => c.SearchAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Client>()); // client repo's own SearchAsync doesn't match on this term
        commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Commission>
        {
            new() { Id = Guid.NewGuid(), ClientId = Guid.NewGuid(), Title = "Monthly retainer work" }
        });
        expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Expense>
        {
            new() { Id = Guid.NewGuid(), Description = "Retainer for design tool" }
        });

        var service = new GlobalSearchService(uow.Object);
        var results = await service.SearchAsync(sharedTerm);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Type == GlobalSearchResultType.Commission);
        Assert.Contains(results, r => r.Type == GlobalSearchResultType.Expense);
    }
}
