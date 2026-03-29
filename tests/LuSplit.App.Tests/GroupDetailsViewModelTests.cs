using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class GroupDetailsParticipantSorterTests
{
    private static GroupMemberModel Owner(string id, string name, string household) =>
        new(id, name, household, IsOwner: true, "FULL", null);

    private static GroupMemberModel Dependent(string id, string name, string household) =>
        new(id, name, household, IsOwner: false, "FULL", null);

    [Fact]
    public void Sort_EmptyList_ReturnsEmpty()
    {
        var result = GroupDetailsParticipantSorter.Sort([], null);

        Assert.Empty(result);
    }

    [Fact]
    public void Sort_SingleOwner_NoDependsOn()
    {
        var members = new[] { Owner("p1", "Alice", "Alice") };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.Single(result);
        Assert.Null(result[0].DependsOn);
    }

    [Fact]
    public void Sort_AlphabeticalOrder_WhenNoPreferredName()
    {
        var members = new[]
        {
            Owner("p2", "Charlie", "Charlie"),
            Owner("p1", "Alice", "Alice"),
            Owner("p3", "Bob", "Bob"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.Equal(["Alice", "Bob", "Charlie"], result.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void Sort_PreferredNameComesFirst()
    {
        var members = new[]
        {
            Owner("p1", "Alice", "Alice"),
            Owner("p2", "Bob", "Bob"),
            Owner("p3", "Charlie", "Charlie"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, preferredName: "Bob");

        Assert.Equal("Bob", result[0].Name);
    }

    [Fact]
    public void Sort_PreferredNameCaseInsensitive()
    {
        var members = new[]
        {
            Owner("p1", "Alice", "Alice"),
            Owner("p2", "BOB", "BOB"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, preferredName: "bob");

        Assert.Equal("BOB", result[0].Name);
    }

    [Fact]
    public void Sort_DependentMember_DependsOnOwnerName()
    {
        var members = new[]
        {
            Owner("p1", "Alice", "Smith"),
            Dependent("p2", "Bob", "Smith"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        var bobEntry = result.Single(e => e.Name == "Bob");
        Assert.Equal("Alice", bobEntry.DependsOn);
    }

    [Fact]
    public void Sort_IndependentOwner_NoDependsOn()
    {
        var members = new[]
        {
            Owner("p1", "Alice", "Alice"),
            Owner("p2", "Bob", "Bob"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.All(result, e => Assert.Null(e.DependsOn));
    }

    [Fact]
    public void Sort_MultipleHouseholds_EachDependentLinkedToCorrectOwner()
    {
        var members = new[]
        {
            Owner("p1", "Alice", "Smith"),
            Dependent("p2", "AliceJr", "Smith"),
            Owner("p3", "Dave", "Jones"),
            Dependent("p4", "DaveJr", "Jones"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.Equal("Alice", result.Single(e => e.Name == "AliceJr").DependsOn);
        Assert.Equal("Dave", result.Single(e => e.Name == "DaveJr").DependsOn);
    }

    [Fact]
    public void Sort_DependentWithNoMatchingOwner_DependsOnNull()
    {
        // Defensive: dependent whose household has no owner in the list
        var members = new[]
        {
            Dependent("p1", "OrphanedDependent", "MissingHousehold"),
        };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.Null(result[0].DependsOn);
    }

    [Fact]
    public void Sort_PreservesParticipantId()
    {
        var members = new[] { Owner("abc-123", "Alice", "Alice") };

        var result = GroupDetailsParticipantSorter.Sort(members, null);

        Assert.Equal("abc-123", result[0].ParticipantId);
    }
}

public sealed class GroupDetailsViewModelTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static GroupMemberModel Owner(string id, string name, string household) =>
        new(id, name, household, IsOwner: true, "FULL", null);

    private static GroupDetailsModel Details(
        string groupId = "g1",
        string groupName = "My Trip",
        string currency = "USD",
        bool isArchived = false,
        IReadOnlyList<GroupMemberModel>? members = null,
        string? imagePath = null) =>
        new(groupId, groupName, currency, isArchived, members ?? [], imagePath);

    private static IGroupDetailsDataService ServiceReturning(GroupDetailsModel details)
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(details);
        svc.GetGroupDetailsAsync(Arg.Any<string>()).Returns(details);
        return svc;
    }

    private static IGroupDetailsDataService ServiceThrowing(Exception ex)
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().ThrowsAsync(ex);
        svc.GetGroupDetailsAsync(Arg.Any<string>()).ThrowsAsync(ex);
        return svc;
    }

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsGroupName()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(groupName: "Bali 2025")));

        await vm.LoadAsync();

        Assert.Equal("Bali 2025", vm.GroupName);
    }

    [Fact]
    public async Task LoadAsync_ActiveGroup_IsArchivedFalse()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: false)));

        await vm.LoadAsync();

        Assert.False(vm.IsArchived);
    }

    [Fact]
    public async Task LoadAsync_ArchivedGroup_IsArchivedTrue()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: true)));

        await vm.LoadAsync();

        Assert.True(vm.IsArchived);
    }

    [Fact]
    public async Task LoadAsync_SetsGroupImagePath()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(imagePath: "/path/img.jpg")));

        await vm.LoadAsync();

        Assert.Equal("/path/img.jpg", vm.GroupImagePath);
    }

    [Fact]
    public async Task LoadAsync_NullImagePath_GroupImagePathNull()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(imagePath: null)));

        await vm.LoadAsync();

        Assert.Null(vm.GroupImagePath);
    }

    [Fact]
    public async Task LoadAsync_ClearsStatusText()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        vm.StatusText = "old error";

        await vm.LoadAsync();

        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public async Task LoadAsync_ServiceThrows_SetsStatusText()
    {
        var vm = new GroupDetailsViewModel(ServiceThrowing(new InvalidOperationException("db error")));

        await vm.LoadAsync();

        Assert.Equal("db error", vm.StatusText);
    }

    [Fact]
    public async Task LoadAsync_WithOverrideGroupId_CallsOverloadWithId()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync("g42").Returns(Details(groupId: "g42", groupName: "Override"));
        var vm = new GroupDetailsViewModel(svc);
        vm.SetOverrideGroupId("g42");

        await vm.LoadAsync();

        Assert.Equal("Override", vm.GroupName);
        await svc.Received(1).GetGroupDetailsAsync("g42");
    }

    [Fact]
    public async Task LoadAsync_NoOverride_CallsDefaultOverload()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        var vm = new GroupDetailsViewModel(svc);

        await vm.LoadAsync();

        await svc.Received(1).GetGroupDetailsAsync();
    }

    [Fact]
    public async Task LoadAsync_PopulatesParticipants()
    {
        var members = new[] { Owner("p1", "Alice", "Alice"), Owner("p2", "Bob", "Bob") };
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(members: members)));

        await vm.LoadAsync();

        Assert.Equal(2, vm.Participants.Count);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCurrencyOptions()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(currency: "EUR")));

        await vm.LoadAsync();

        Assert.NotEmpty(vm.CurrencyOptions);
    }

    [Fact]
    public async Task LoadAsync_SetsSelectedCurrencyOptionToGroupCurrency()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(currency: "EUR")));

        await vm.LoadAsync();

        Assert.NotNull(vm.SelectedCurrencyOption);
        Assert.Equal("EUR", vm.SelectedCurrencyOption!.Code);
    }

    // ── Derived flags ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CanEdit_ActiveGroup_True()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: false)));

        await vm.LoadAsync();

        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task CanEdit_ArchivedGroup_False()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: true)));

        await vm.LoadAsync();

        Assert.False(vm.CanEdit);
    }

    [Fact]
    public async Task CanArchive_ActiveGroup_True()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: false)));

        await vm.LoadAsync();

        Assert.True(vm.CanArchive);
    }

    [Fact]
    public async Task CanArchive_ArchivedGroup_False()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(isArchived: true)));

        await vm.LoadAsync();

        Assert.False(vm.CanArchive);
    }

    // ── SaveCommand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_EmptyGroupName_SetsValidationStatus()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        vm.GroupName = string.Empty;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_GroupNameRequired", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_NoCurrency_SetsValidationStatus()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        vm.GroupName = "Valid Name";
        vm.SelectedCurrencyOption = null;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_SelectCurrency", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_Valid_CallsUpdateGroupAsync()
    {
        var svc = ServiceReturning(Details(groupId: "g1", groupName: "Old", currency: "USD"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();
        vm.GroupName = "New Name";

        await vm.SaveCommand.ExecuteAsync(null);

        await svc.Received(1).UpdateGroupAsync("g1", "New Name", Arg.Any<string>());
    }

    [Fact]
    public async Task SaveCommand_Valid_FiresSaveCompleted()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        var fired = false;
        vm.SaveCompleted += (_, _) => fired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(fired);
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_SetsStatusText()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        svc.UpdateGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("network error"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("network error", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_DoesNotFireSaveCompleted()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        svc.UpdateGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("error"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();
        var fired = false;
        vm.SaveCompleted += (_, _) => fired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(fired);
    }

    // ── RequestArchiveCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task RequestArchiveCommand_FiresArchiveConfirmationRequested()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        var fired = false;
        vm.ArchiveConfirmationRequested += (_, _) => fired = true;

        vm.RequestArchiveCommand.Execute(null);

        Assert.True(fired);
    }

    [Fact]
    public async Task ConfirmArchiveAsync_CallsArchiveGroupAsync()
    {
        var svc = ServiceReturning(Details(groupId: "g1"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        await vm.ConfirmArchiveAsync();

        await svc.Received(1).ArchiveGroupAsync("g1");
    }

    [Fact]
    public async Task ConfirmArchiveAsync_FiresArchiveCompleted()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        var fired = false;
        vm.ArchiveCompleted += (_, _) => fired = true;

        await vm.ConfirmArchiveAsync();

        Assert.True(fired);
    }

    [Fact]
    public async Task ConfirmArchiveAsync_ServiceThrows_SetsStatusText()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        svc.ArchiveGroupAsync(Arg.Any<string>()).ThrowsAsync(new InvalidOperationException("archive error"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        await vm.ConfirmArchiveAsync();

        Assert.Equal("archive error", vm.StatusText);
    }

    // ── RequestExportCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task RequestExportCommand_FiresExportRequestedWithGroupId()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(groupId: "g99")));
        await vm.LoadAsync();
        string? receivedId = null;
        vm.ExportRequested += (_, id) => receivedId = id;

        vm.RequestExportCommand.Execute(null);

        Assert.Equal("g99", receivedId);
    }

    // ── RequestPhotoChangeCommand ─────────────────────────────────────────────

    [Fact]
    public async Task RequestPhotoChangeCommand_FiresPhotoChangeRequested()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));
        await vm.LoadAsync();
        var fired = false;
        vm.PhotoChangeRequested += (_, _) => fired = true;

        vm.RequestPhotoChangeCommand.Execute(null);

        Assert.True(fired);
    }

    [Fact]
    public void ApplyNewPhoto_SetsGroupImagePath()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details()));

        vm.ApplyNewPhoto("/new/path.jpg");

        Assert.Equal("/new/path.jpg", vm.GroupImagePath);
    }

    [Fact]
    public void ApplyPhotoRemoved_ClearsGroupImagePath()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(imagePath: "/old/img.jpg")));
        vm.ApplyNewPhoto("/old/img.jpg");

        vm.ApplyPhotoRemoved();

        Assert.Null(vm.GroupImagePath);
    }

    // ── AddMemberAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddMemberAsync_CallsAddGroupMemberAsync()
    {
        var svc = ServiceReturning(Details(groupId: "g1"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        await vm.AddMemberAsync("Eve");

        await svc.Received(1).AddGroupMemberAsync("g1", "Eve", null);
    }

    [Fact]
    public async Task AddMemberAsync_ReloadsAfterAdd()
    {
        var svc = ServiceReturning(Details(groupId: "g1", groupName: "First"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        svc.GetGroupDetailsAsync().Returns(Details(groupId: "g1", groupName: "Second"));
        await vm.AddMemberAsync("Eve");

        Assert.Equal("Second", vm.GroupName);
    }

    [Fact]
    public async Task AddMemberAsync_ServiceThrows_SetsStatusText()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        svc.AddGroupMemberAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .ThrowsAsync(new InvalidOperationException("member error"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();

        await vm.AddMemberAsync("Eve");

        Assert.Equal("member error", vm.StatusText);
    }

    // ── UpdateMemberDependencyAsync ───────────────────────────────────────────

    [Fact]
    public async Task UpdateMemberDependencyAsync_NoDependency_CallsWithNullDependsOnId()
    {
        var svc = ServiceReturning(Details(groupId: "g1",
            members: [Owner("p1", "Alice", "Alice"), Owner("p2", "Bob", "Bob")]));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();
        var participant = new ParticipantDraftViewModel("Alice", "p1") { DependsOn = null };

        await vm.UpdateMemberDependencyAsync(participant);

        await svc.Received(1).UpdateGroupMemberAsync("g1", "p1", "Alice", null);
    }

    [Fact]
    public async Task UpdateMemberDependencyAsync_WithDependency_ResolvesOwnerParticipantId()
    {
        var members = new[] { Owner("p1", "Alice", "Alice"), Owner("p2", "Bob", "Bob") };
        var svc = ServiceReturning(Details(groupId: "g1", members: members));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();
        var participant = new ParticipantDraftViewModel("Bob", "p2") { DependsOn = "Alice" };

        await vm.UpdateMemberDependencyAsync(participant);

        await svc.Received(1).UpdateGroupMemberAsync("g1", "p2", "Bob", "p1");
    }

    [Fact]
    public async Task UpdateMemberDependencyAsync_ServiceThrows_ReloadsAndSetsStatus()
    {
        var svc = Substitute.For<IGroupDetailsDataService>();
        svc.GetGroupDetailsAsync().Returns(Details());
        svc.UpdateGroupMemberAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .ThrowsAsync(new InvalidOperationException("update error"));
        var vm = new GroupDetailsViewModel(svc);
        await vm.LoadAsync();
        var participant = new ParticipantDraftViewModel("Alice", "p1");

        await vm.UpdateMemberDependencyAsync(participant);

        Assert.Equal("update error", vm.StatusText);
    }

    // ── GroupId property ──────────────────────────────────────────────────────

    [Fact]
    public async Task GroupId_AfterLoad_MatchesDetailsGroupId()
    {
        var vm = new GroupDetailsViewModel(ServiceReturning(Details(groupId: "g-xyz")));

        await vm.LoadAsync();

        Assert.Equal("g-xyz", vm.GroupId);
    }
}
