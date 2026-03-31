using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Split;
using NSubstitute;

namespace LuSplit.App.Tests;

/// <summary>
/// Regression tests covering archived-group read-only behaviour in <see cref="ExpenseDetailsViewModel"/>.
/// </summary>
public sealed class ExpenseDetailsArchivedGroupTests
{
    // ── helpers ───────────────────────────────────────────────────────────

    private const string GroupId = "g-archived";
    private const string ExpenseId = "exp-1";

    private static GroupOverviewModel MakeOverview(bool closed) =>
        new(new GroupModel(GroupId, "USD", closed),
            new GroupSummaryModel(GroupId, 1, 1, 1, 0),
            [new ParticipantModel("p1", GroupId, "eu1", "Alice", "FULL", null)],
            [],
            [],
            [],
            [],
            [],
            new SettlementPlanModel(SettlementMode.Participant, []),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));

    private static ExpenseModel MakeExpense() =>
        new(ExpenseId, GroupId, "Dinner", "p1", 1000, "2025-01-01",
            new SplitDefinition([new FixedSplitComponent(new Dictionary<string, long>(StringComparer.Ordinal) { ["p1"] = 1000 })]),
            null);

    private static IExpenseDetailsDataService ServiceFor(GroupOverviewModel overview, ExpenseModel? expense)
    {
        var svc = Substitute.For<IExpenseDetailsDataService>();
        // group-scoped overload (used when contextGroupId is set)
        svc.GetOverviewAsync(GroupId).Returns(overview);
        svc.GetExpenseAsync(ExpenseId, GroupId).Returns(expense);
        // selected-group overload (not used in archived path, but present for completeness)
        svc.GetOverviewAsync().Returns(overview);
        svc.GetExpenseAsync(ExpenseId).Returns(expense);
        return svc;
    }

    private static ExpenseDetailsViewModel BuildVm(IExpenseDetailsDataService svc)
    {
        var vm = new ExpenseDetailsViewModel(svc);
        vm.SetExpenseId(ExpenseId);
        return vm;
    }

    // ── navigation regression: archived group expense can be loaded ───────

    [Fact]
    public async Task LoadAsync_WithContextGroupId_CallsGroupScopedExpenseOverload()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        await svc.Received(1).GetExpenseAsync(ExpenseId, GroupId);
    }

    [Fact]
    public async Task LoadAsync_WithContextGroupId_LoadsExpenseTitle()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.Equal("Dinner", vm.ExpenseTitle);
    }

    // ── archived state derived from real data ─────────────────────────────

    [Fact]
    public async Task LoadAsync_ArchivedGroup_IsArchivedTrue()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.True(vm.IsArchived);
    }

    [Fact]
    public async Task LoadAsync_ActiveGroup_IsArchivedFalse()
    {
        var svc = ServiceFor(MakeOverview(closed: false), MakeExpense());
        var vm = BuildVm(svc);
        // no contextGroupId set → active group path

        await vm.LoadAsync();

        Assert.False(vm.IsArchived);
    }

    // ── read-only flags when archived ─────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ArchivedGroup_IsReadOnlyTrue()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.True(vm.IsReadOnly);
    }

    [Fact]
    public async Task LoadAsync_ArchivedGroup_CanEditFalse()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task LoadAsync_ArchivedGroup_CanDeleteFalse()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.False(vm.CanDelete);
    }

    [Fact]
    public async Task LoadAsync_ArchivedGroup_ShowArchivedBannerTrue()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.True(vm.ShowArchivedBanner);
    }

    // ── commands unavailable in archived mode ─────────────────────────────

    [Fact]
    public async Task ToggleEditModeCommand_ArchivedGroup_DoesNotEnterEditMode()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);
        await vm.LoadAsync();

        vm.ToggleEditModeCommand.Execute(null);

        Assert.False(vm.IsEditMode);
    }

    [Fact]
    public async Task ConfirmDeleteAsync_ArchivedGroup_DoesNotDelete()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);
        await vm.LoadAsync();

        await vm.ConfirmDeleteAsync();

        await svc.DidNotReceive().DeleteExpenseAsync(Arg.Any<string>());
    }

    // ── viewing still works when archived ────────────────────────────────

    [Fact]
    public async Task LoadAsync_ArchivedGroup_ParticipantRowsPopulated()
    {
        var svc = ServiceFor(MakeOverview(closed: true), MakeExpense());
        var vm = BuildVm(svc);
        vm.SetGroupId(GroupId);

        await vm.LoadAsync();

        Assert.NotEmpty(vm.ParticipantRows);
    }
}
