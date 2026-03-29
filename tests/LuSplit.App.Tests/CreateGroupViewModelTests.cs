using LuSplit.App.Pages;
using LuSplit.App.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public class CreateGroupViewModelTests
{
    private static ICreateGroupDataService MockDataService() => Substitute.For<ICreateGroupDataService>();

    private static CreateGroupViewModel BuildVm(ICreateGroupDataService? dataService = null)
        => new(dataService ?? MockDataService());

    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsStep1()
    {
        var vm = BuildVm();
        Assert.True(vm.IsStep1);
        Assert.False(vm.IsStep2);
    }

    [Fact]
    public void InitialState_CurrencyOptionsPopulated()
    {
        var vm = BuildVm();
        Assert.NotEmpty(vm.CurrencyOptions);
    }

    [Fact]
    public void InitialState_SelectedCurrencyOptionIsNotNull()
    {
        var vm = BuildVm();
        Assert.NotNull(vm.SelectedCurrencyOption);
    }

    // ── ContinueCommand — validation ────────────────────────────────────────

    [Fact]
    public void ContinueCommand_EmptyGroupName_SetsValidationStatus()
    {
        var vm = BuildVm();
        vm.GroupName = "";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();

        vm.ContinueCommand.Execute(null);

        Assert.Equal("Validation_GroupNameRequired", vm.StatusText);
        Assert.True(vm.IsStep1);
    }

    [Fact]
    public void ContinueCommand_WhitespaceGroupName_SetsValidationStatus()
    {
        var vm = BuildVm();
        vm.GroupName = "   ";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();

        vm.ContinueCommand.Execute(null);

        Assert.Equal("Validation_GroupNameRequired", vm.StatusText);
    }

    [Fact]
    public void ContinueCommand_NoCurrency_SetsValidationStatus()
    {
        var vm = BuildVm();
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = null;

        vm.ContinueCommand.Execute(null);

        Assert.Equal("Validation_SelectCurrency", vm.StatusText);
        Assert.True(vm.IsStep1);
    }

    // ── ContinueCommand — step advancement ────────────────────────────────

    [Fact]
    public void ContinueCommand_ValidInput_AdvancesToStep2()
    {
        var vm = BuildVm();
        vm.GroupName = "Vacation";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();

        vm.ContinueCommand.Execute(null);

        Assert.False(vm.IsStep1);
        Assert.True(vm.IsStep2);
    }

    [Fact]
    public void ContinueCommand_ValidInput_ClearsStatusText()
    {
        var vm = BuildVm();
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.StatusText = "Some previous error";

        vm.ContinueCommand.Execute(null);

        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public void ContinueCommand_ValidInput_EnsuresCurrentUserInParticipants()
    {
        var vm = BuildVm();
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();

        vm.ContinueCommand.Execute(null);

        Assert.Single(vm.Participants);
        Assert.False(vm.Participants[0].CanRemove);
    }

    // ── Participant management ─────────────────────────────────────────────

    [Fact]
    public void AddParticipant_AddsToCollection()
    {
        var vm = BuildVm();

        vm.AddParticipant("Alice");

        Assert.Single(vm.Participants);
        Assert.Equal("Alice", vm.Participants[0].Name);
    }

    [Fact]
    public void RemoveParticipant_RemovesFromCollection()
    {
        var vm = BuildVm();
        vm.AddParticipant("Alice");
        var alice = vm.Participants[0];

        vm.RemoveParticipant(alice);

        Assert.Empty(vm.Participants);
    }

    [Fact]
    public void OnDependencyChanged_DoesNotThrow()
    {
        var vm = BuildVm();
        vm.AddParticipant("Alice");

        var exception = Record.Exception(() => vm.OnDependencyChanged(vm.Participants[0]));

        Assert.Null(exception);
    }

    // ── EnsureCurrentUserParticipant ────────────────────────────────────────

    [Fact]
    public void EnsureCurrentUserParticipant_EmptyList_AddsUserFirst()
    {
        var vm = BuildVm();

        vm.EnsureCurrentUserParticipant();

        Assert.Single(vm.Participants);
        Assert.False(vm.Participants[0].CanRemove);
    }

    [Fact]
    public void EnsureCurrentUserParticipant_CalledTwice_DoesNotDuplicate()
    {
        var vm = BuildVm();

        vm.EnsureCurrentUserParticipant();
        vm.EnsureCurrentUserParticipant();

        Assert.Single(vm.Participants);
    }

    [Fact]
    public void EnsureCurrentUserParticipant_UserAlreadyFirstWithCanRemoveFalse_NoChange()
    {
        var vm = BuildVm();
        vm.EnsureCurrentUserParticipant();
        var originalParticipant = vm.Participants[0];

        vm.EnsureCurrentUserParticipant();

        Assert.Same(originalParticipant, vm.Participants[0]);
    }

    [Fact]
    public void EnsureCurrentUserParticipant_UserNotFirst_PromotesToFront()
    {
        var vm = BuildVm();
        // Determine the auto name (stub returns empty preferred name, so fallback "Me")
        vm.AddParticipant("Bob");
        vm.AddParticipant("Me"); // matches fallback from LocalizationHelperStub

        vm.EnsureCurrentUserParticipant();

        Assert.Equal("Me", vm.Participants[0].Name);
        Assert.False(vm.Participants[0].CanRemove);
    }

    [Fact]
    public void EnsureCurrentUserParticipant_UserFoundWithCanRemoveTrue_SetsCanRemoveFalse()
    {
        var vm = BuildVm();
        vm.AddParticipant("Me"); // canRemove defaults to true

        vm.EnsureCurrentUserParticipant();

        Assert.False(vm.Participants[0].CanRemove);
    }

    // ── CreateCommand — validation ─────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_NoParticipants_SetsValidationStatus()
    {
        var vm = BuildVm();

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Validation_AddAtLeastOnePerson", vm.StatusText);
    }

    // ── CreateCommand — success ────────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_ValidInput_CallsCreateGroupAsync()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        var vm = BuildVm(dataService);
        vm.GroupName = "Vacation";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Bob");

        await vm.CreateCommand.ExecuteAsync(null);

        await dataService.Received(1).CreateGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GroupDraftMember>>());
    }

    [Fact]
    public async Task CreateCommand_ValidInput_FiresGroupCreated()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        var vm = BuildVm(dataService);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Alice");

        var fired = false;
        vm.GroupCreated += (_, _) => fired = true;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.True(fired);
    }

    [Fact]
    public async Task CreateCommand_ValidInput_PassesTrimmedGroupName()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        var vm = BuildVm(dataService);
        vm.GroupName = "  Trip  ";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Alice");

        await vm.CreateCommand.ExecuteAsync(null);

        await dataService.Received(1).CreateGroupAsync(
            "Trip", Arg.Any<string>(), Arg.Any<IReadOnlyList<GroupDraftMember>>());
    }

    // ── CreateCommand — failure ────────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_ServiceThrows_SetsStatusText()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("Server error"));
        var vm = BuildVm(dataService);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Alice");

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Server error", vm.StatusText);
    }

    [Fact]
    public async Task CreateCommand_ServiceThrows_DoesNotFireGroupCreated()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ThrowsAsyncForAnyArgs(new InvalidOperationException("fail"));
        var vm = BuildVm(dataService);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Alice");

        var fired = false;
        vm.GroupCreated += (_, _) => fired = true;

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.False(fired);
    }

    // ── Draft member mapping ───────────────────────────────────────────────

    [Fact]
    public async Task CreateCommand_IndependentParticipant_HouseholdNameIsOwnName()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        IReadOnlyList<GroupDraftMember>? capturedDrafts = null;
        await dataService.CreateGroupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<GroupDraftMember>>(d => capturedDrafts = d));

        var vm = BuildVm(dataService);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Alice");

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(capturedDrafts);
        var alice = capturedDrafts!.FirstOrDefault(d => d.Name == "Alice");
        Assert.NotNull(alice);
        Assert.Equal("Alice", alice!.HouseholdName);
    }

    [Fact]
    public async Task CreateCommand_DependentParticipant_HouseholdNameIsResponsibleName()
    {
        var dataService = MockDataService();
        dataService.CreateGroupAsync(default!, default!, default!).ReturnsForAnyArgs(string.Empty);
        IReadOnlyList<GroupDraftMember>? capturedDrafts = null;
        await dataService.CreateGroupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<GroupDraftMember>>(d => capturedDrafts = d));

        var vm = BuildVm(dataService);
        vm.GroupName = "Trip";
        vm.SelectedCurrencyOption = vm.CurrencyOptions.First();
        vm.AddParticipant("Charlie");
        vm.Participants[0].DependsOn = "Alice";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotNull(capturedDrafts);
        var charlie = capturedDrafts!.FirstOrDefault(d => d.Name == "Charlie");
        Assert.NotNull(charlie);
        Assert.Equal("Alice", charlie!.HouseholdName);
    }
}
