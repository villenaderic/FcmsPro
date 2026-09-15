using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Commissions;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

/// <summary>
/// Covers CommissionsListViewModel's filter/search logic and kanban-column
/// grouping - the "kanban logic" the user named as having zero coverage.
///
/// Caveat that applies only to this file: FcmsPro.Avalonia references
/// Avalonia's NuGet packages, which this sandbox's network can't restore, so
/// unlike the pure-Core test files in this project, I could not compile-check
/// these against the real ViewModel. I traced every member (constructor
/// params, RelayCommand-generated command names, ObservableProperty names)
/// against the actual source rather than guessing, but this file carries
/// more risk than the rest - run `dotnet test` locally first.
///
/// Also note: CommissionsListViewModel's constructor fires an unawaited
/// `_ = LoadAsync()` (a real pattern in the production code, not a test
/// artifact). Every test below awaits LoadCommand.ExecuteAsync(null) itself
/// immediately after construction specifically to get a deterministic
/// second load to assert against, rather than relying on that fire-and-forget
/// call's timing.
/// </summary>
public class CommissionsListViewModelKanbanTests
{
    private static (CommissionsListViewModel vm, Mock<ICommissionRepository> commissions) BuildViewModel(List<Commission> seedData)
    {
        var uow = new Mock<IUnitOfWork>();
        var commissions = new Mock<ICommissionRepository>();
        var settings = new Mock<ISettingsRepository>();
        var clients = new Mock<IClientRepository>();

        uow.SetupGet(u => u.Commissions).Returns(commissions.Object);
        uow.SetupGet(u => u.Settings).Returns(settings.Object);
        uow.SetupGet(u => u.Clients).Returns(clients.Object);

        commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(seedData);
        settings.Setup(s => s.GetUiPreferencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UiPreferences());

        var dialogService = new DialogService();
        var navigation = new NavigationService();

        var vm = new CommissionsListViewModel(new CommissionService(uow.Object, new InvoiceService(uow.Object)), uow.Object, dialogService, navigation);
        return (vm, commissions);
    }

    private static Commission MakeCommission(string title, CommissionStatus status, bool isDeleted = false) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = Guid.NewGuid(),
        Title = title,
        Status = status,
        IsDeleted = isDeleted,
        DateAdded = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ApplyFilterToRows_GroupsEachCommissionIntoItsStatusColumn()
    {
        var seed = new List<Commission>
        {
            MakeCommission("Sketch A", CommissionStatus.Pending),
            MakeCommission("Sketch B", CommissionStatus.InProgress),
            MakeCommission("Sketch C", CommissionStatus.InProgress),
            MakeCommission("Sketch D", CommissionStatus.Delivered)
        };
        var (vm, _) = BuildViewModel(seed);

        await vm.LoadCommand.ExecuteAsync(null);

        var newColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.Pending);
        var inProgressColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.InProgress);
        var deliveredColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.Delivered);

        Assert.Single(newColumn.Items);
        Assert.Equal(2, inProgressColumn.Items.Count);
        Assert.Single(deliveredColumn.Items);
    }

    [Fact]
    public async Task ApplyFilterToRows_ExcludesSoftDeletedCommissions()
    {
        var seed = new List<Commission>
        {
            MakeCommission("Visible", CommissionStatus.Pending),
            MakeCommission("Trashed", CommissionStatus.Pending, isDeleted: true)
        };
        var (vm, _) = BuildViewModel(seed);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Rows);
        Assert.Equal("Visible", vm.Rows[0].Title);

        var newColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.Pending);
        Assert.Single(newColumn.Items); // deleted item must not leak into the kanban column either
    }

    [Fact]
    public async Task StatusFilter_NarrowsBothTableRowsAndKanbanColumns()
    {
        var seed = new List<Commission>
        {
            MakeCommission("A", CommissionStatus.Pending),
            MakeCommission("B", CommissionStatus.InProgress),
            MakeCommission("C", CommissionStatus.Delivered)
        };
        var (vm, _) = BuildViewModel(seed);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.StatusFilter = CommissionStatus.InProgress;

        Assert.Single(vm.Rows);
        Assert.Equal("B", vm.Rows[0].Title);
        // Filtering is table-row-level (Rows), not column removal - the
        // kanban columns still exist for every status, just empty for
        // statuses that don't match. Confirm the matching column still has
        // its item and a non-matching column is empty.
        var inProgressColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.InProgress);
        var newColumn = vm.KanbanColumns.Single(c => c.Status == CommissionStatus.Pending);
        Assert.Single(inProgressColumn.Items);
        Assert.Empty(newColumn.Items);
    }

    [Fact]
    public async Task SearchQuery_MatchesTitleCaseInsensitively()
    {
        var seed = new List<Commission>
        {
            MakeCommission("Dragon Portrait", CommissionStatus.Pending),
            MakeCommission("Cat Sticker Pack", CommissionStatus.Pending)
        };
        var (vm, _) = BuildViewModel(seed);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchQuery = "DRAGON";

        Assert.Single(vm.Rows);
        Assert.Equal("Dragon Portrait", vm.Rows[0].Title);
    }

    [Fact]
    public async Task SearchQuery_AlsoMatchesServiceType()
    {
        var commission = MakeCommission("Untitled", CommissionStatus.Pending);
        commission.ServiceType = "Character Illustration";
        var (vm, _) = BuildViewModel(new List<Commission> { commission });
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchQuery = "illustration";

        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task Rows_AreOrderedByDateAddedDescending()
    {
        var older = MakeCommission("Older", CommissionStatus.Pending);
        older.DateAdded = DateTimeOffset.UtcNow.AddDays(-5);
        var newer = MakeCommission("Newer", CommissionStatus.Pending);
        newer.DateAdded = DateTimeOffset.UtcNow;

        var (vm, _) = BuildViewModel(new List<Commission> { older, newer });
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Newer", vm.Rows[0].Title);
        Assert.Equal("Older", vm.Rows[1].Title);
    }

    [Fact]
    public async Task HasNoResults_TrueOnlyWhenLoadedAndEmpty()
    {
        var (vm, _) = BuildViewModel(new List<Commission>());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoResults);
    }

    [Fact]
    public async Task KanbanColumns_ExistForEveryStatusEvenWithNoCommissions()
    {
        // Columns are seeded from the enum at construction time (see the
        // KanbanColumns field initializer), not derived from loaded data -
        // an empty commission list should still produce one column per
        // CommissionStatus value, all empty, not zero columns.
        var (vm, _) = BuildViewModel(new List<Commission>());
        await vm.LoadCommand.ExecuteAsync(null);

        var expectedStatusCount = Enum.GetValues<CommissionStatus>().Length;
        Assert.Equal(expectedStatusCount, vm.KanbanColumns.Count);
        Assert.All(vm.KanbanColumns, c => Assert.Empty(c.Items));
    }
}
