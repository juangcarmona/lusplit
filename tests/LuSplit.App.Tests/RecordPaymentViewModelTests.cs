using CommunityToolkit.Mvvm.Input;
using LuSplit.App.Pages;
using LuSplit.App.Services;
using LuSplit.Application.Models;
using LuSplit.Domain.Expenses;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.App.Tests;

public sealed class RecordPaymentViewModelTests
{
    // ---- helpers ----

    private static ParticipantModel P(string id, string name) =>
        new(id, "g1", "eu1", name, "default", null);

    private static GroupOverviewModel OverviewWith(params ParticipantModel[] participants) =>
        new(new GroupModel("g1", "USD", false),
            new GroupSummaryModel("g1", participants.Length, 0, 0, 0),
            participants,
            [],
            [],
            [],
            [],
            [],
            new SettlementPlanModel(SettlementMode.Participant, []),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));

    private static GroupOverviewModel OverviewWithTransfer(
        ParticipantModel from, ParticipantModel to, long amountMinor) =>
        new(new GroupModel("g1", "USD", false),
            new GroupSummaryModel("g1", 2, 0, 0, 0),
            [from, to],
            [],
            [],
            [],
            [],
            [],
            new SettlementPlanModel(SettlementMode.Participant,
                [new SettlementTransferModel(from.Id, to.Id, amountMinor)]),
            new SettlementPlanModel(SettlementMode.EconomicUnitOwner, []));

    private static IRecordPaymentDataService ServiceReturning(GroupOverviewModel overview)
    {
        var svc = Substitute.For<IRecordPaymentDataService>();
        svc.GetOverviewAsync().Returns(overview);
        return svc;
    }

    private static IRecordPaymentDataService ServiceThrowing(Exception ex)
    {
        var svc = Substitute.For<IRecordPaymentDataService>();
        svc.GetOverviewAsync().ThrowsAsync(ex);
        return svc;
    }

    // ---- LoadAsync: selection defaults ----

    [Fact]
    public async Task LoadAsync_TwoParticipants_SelectsFirstAsFrom()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));

        await vm.LoadAsync();

        Assert.Equal("Alice", vm.SelectedFromName);
    }

    [Fact]
    public async Task LoadAsync_TwoParticipants_SelectsSecondAsTo()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));

        await vm.LoadAsync();

        Assert.Equal("Bob", vm.SelectedToName);
    }

    [Fact]
    public async Task LoadAsync_TwoParticipants_PopulatesPersonNames()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));

        await vm.LoadAsync();

        Assert.Equal(["Alice", "Bob"], vm.PersonNames);
    }

    // ---- LoadAsync: settlement suggestion ----

    [Fact]
    public async Task LoadAsync_WithSettlementSuggestion_UsesFromParticipant()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWithTransfer(bob, alice, 500)));

        await vm.LoadAsync();

        Assert.Equal("Bob", vm.SelectedFromName);
    }

    [Fact]
    public async Task LoadAsync_WithSettlementSuggestion_UsesToParticipant()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWithTransfer(bob, alice, 500)));

        await vm.LoadAsync();

        Assert.Equal("Alice", vm.SelectedToName);
    }

    // ---- LoadAsync: prefill overrides suggestion ----

    [Fact]
    public async Task LoadAsync_PrefillPayerOverridesSuggestion()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWithTransfer(bob, alice, 500)));
        vm.SetPrefill("p1", null, null, null, null);

        await vm.LoadAsync();

        Assert.Equal("Alice", vm.SelectedFromName);
    }

    [Fact]
    public async Task LoadAsync_PrefillReceiverOverridesSuggestion()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWithTransfer(bob, alice, 500)));
        vm.SetPrefill(null, "p2", null, null, null);

        await vm.LoadAsync();

        Assert.Equal("Bob", vm.SelectedToName);
    }

    // ---- LoadAsync: prefill amount ----

    [Fact]
    public async Task LoadAsync_WithPrefillAmount_SetsAmountText()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        vm.SetPrefill(null, null, 1250, null, null);

        await vm.LoadAsync();

        Assert.Equal("12.50", vm.AmountText);
    }

    [Fact]
    public async Task LoadAsync_NoPrefillAmount_AmountTextEmpty()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));

        await vm.LoadAsync();

        Assert.Equal(string.Empty, vm.AmountText);
    }

    // ---- LoadAsync: quick mode ----

    [Fact]
    public async Task LoadAsync_AllPrefilled_IsQuickModeTrue()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        vm.SetPrefill("p1", "p2", 1000, "USD", null);

        await vm.LoadAsync();

        Assert.True(vm.IsQuickMode);
    }

    [Fact]
    public async Task LoadAsync_NoPrefill_IsQuickModeFalse()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));

        await vm.LoadAsync();

        Assert.False(vm.IsQuickMode);
    }

    [Fact]
    public async Task LoadAsync_ZeroAmountPrefill_IsQuickModeFalse()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        vm.SetPrefill("p1", "p2", 0, "USD", null);

        await vm.LoadAsync();

        Assert.False(vm.IsQuickMode);
    }

    [Fact]
    public async Task LoadAsync_QuickMode_QuickSummaryTextContainsParticipantNames()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        vm.SetPrefill("p1", "p2", 1000, "USD", null);

        await vm.LoadAsync();

        Assert.Contains("Alice", vm.QuickSummaryText);
        Assert.Contains("Bob", vm.QuickSummaryText);
    }

    // ---- SaveCommand: validation ----

    [Fact]
    public async Task SaveCommand_NoParticipantsLoaded_SetsValidationStatus()
    {
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith()));

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_ChooseBothPeople", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_SamePersonSelected_SetsValidationStatus()
    {
        var alice = P("p1", "Alice");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice)));
        await vm.LoadAsync();
        vm.SelectedFromName = "Alice";
        vm.SelectedToName = "Alice";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_DifferentPeople", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_InvalidAmountText_SetsValidationStatus()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        await vm.LoadAsync();
        vm.AmountText = "not-a-number";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_InvalidAmount", vm.StatusText);
    }

    [Fact]
    public async Task SaveCommand_ZeroAmount_SetsValidationStatus()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var vm = new RecordPaymentViewModel(ServiceReturning(OverviewWith(alice, bob)));
        await vm.LoadAsync();
        vm.AmountText = "0";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Validation_InvalidAmount", vm.StatusText);
    }

    // ---- SaveCommand: valid save ----

    [Fact]
    public async Task SaveCommand_Valid_CallsAddPaymentAsync()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var svc = ServiceReturning(OverviewWith(alice, bob));
        var vm = new RecordPaymentViewModel(svc);
        await vm.LoadAsync();
        vm.AmountText = "10.00";

        await vm.SaveCommand.ExecuteAsync(null);

        await svc.Received(1).AddPaymentAsync("p1", "p2", 1000, Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SaveCommand_Valid_FiresPaymentSaved()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var svc = ServiceReturning(OverviewWith(alice, bob));
        var vm = new RecordPaymentViewModel(svc);
        await vm.LoadAsync();
        vm.AmountText = "5.00";
        var fired = false;
        vm.PaymentSaved += (_, _) => fired = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(fired);
    }

    [Fact]
    public async Task SaveCommand_Valid_FiresPaymentSavedWithOrigin()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var svc = ServiceReturning(OverviewWith(alice, bob));
        var vm = new RecordPaymentViewModel(svc);
        vm.SetPrefill(null, null, null, null, "settlement");
        await vm.LoadAsync();
        vm.AmountText = "5.00";
        string? receivedOrigin = "initial";
        vm.PaymentSaved += (_, origin) => receivedOrigin = origin;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("settlement", receivedOrigin);
    }

    [Fact]
    public async Task SaveCommand_Valid_NullOriginWhenNotSet()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var svc = ServiceReturning(OverviewWith(alice, bob));
        var vm = new RecordPaymentViewModel(svc);
        await vm.LoadAsync();
        vm.AmountText = "5.00";
        string? receivedOrigin = "initial";
        vm.PaymentSaved += (_, origin) => receivedOrigin = origin;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(receivedOrigin);
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_SetsStatusText()
    {
        var alice = P("p1", "Alice");
        var bob = P("p2", "Bob");
        var svc = Substitute.For<IRecordPaymentDataService>();
        svc.GetOverviewAsync().Returns(OverviewWith(alice, bob));
        svc.AddPaymentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<DateTime>())
            .ThrowsAsync(new InvalidOperationException("something went wrong"));
        var vm = new RecordPaymentViewModel(svc);
        await vm.LoadAsync();
        vm.AmountText = "5.00";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("something went wrong", vm.StatusText);
    }
}
