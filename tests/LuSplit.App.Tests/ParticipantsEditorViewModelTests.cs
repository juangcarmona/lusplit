using System.Collections.ObjectModel;
using LuSplit.App.Pages;
using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Tests;

public class ParticipantsEditorViewModelTests
{
    private static ParticipantDraftViewModel P(string name, string? dependsOn = null, bool canRemove = true)
    {
        var vm = new ParticipantDraftViewModel(name, canRemove: canRemove);
        vm.DependsOn = dependsOn;
        return vm;
    }

    private static ObservableCollection<ParticipantDraftViewModel> List(params ParticipantDraftViewModel[] items)
        => new(items);

    // ── Add validation ────────────────────────────────────────────────────────

    [Fact]
    public void Add_EmptyName_SetsValidationStatus()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List() };
        vm.NewParticipantName = "   ";

        vm.AddCommand.Execute(null);

        Assert.Equal(AppResources.Validation_PersonNameRequired, vm.StatusText);
    }

    [Fact]
    public void Add_EmptyName_DoesNotFireAddRequested()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List() };
        var fired = false;
        vm.AddParticipantRequested += (_, _) => fired = true;

        vm.AddCommand.Execute(null);

        Assert.False(fired);
    }

    [Fact]
    public void Add_DuplicateName_SetsValidationStatus()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List(P("Alice")) };
        vm.NewParticipantName = "alice"; // case-insensitive check

        vm.AddCommand.Execute(null);

        Assert.Equal(AppResources.Validation_PersonNameMustBeUnique, vm.StatusText);
    }

    [Fact]
    public void Add_ValidName_FiresAddParticipantRequested_WithTrimmedName()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List() };
        string? received = null;
        vm.AddParticipantRequested += (_, name) => received = name;
        vm.NewParticipantName = "  Bob  ";

        vm.AddCommand.Execute(null);

        Assert.Equal("Bob", received);
    }

    [Fact]
    public void Add_ValidName_ClearsNameAndStatus()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List() };
        vm.StatusText = "old error";
        vm.NewParticipantName = "Alice";

        vm.AddCommand.Execute(null);

        Assert.Equal(string.Empty, vm.NewParticipantName);
        Assert.Equal(string.Empty, vm.StatusText);
    }

    // ── HasStatusText ─────────────────────────────────────────────────────────

    [Fact]
    public void HasStatusText_TrueWhenStatusTextHasContent()
    {
        var vm = new ParticipantsEditorViewModel();
        vm.StatusText = "error";

        Assert.True(vm.HasStatusText);
    }

    [Fact]
    public void HasStatusText_FalseWhenStatusTextEmpty()
    {
        var vm = new ParticipantsEditorViewModel();
        vm.StatusText = string.Empty;

        Assert.False(vm.HasStatusText);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ClearsDependentsOfRemovedParticipant()
    {
        var alice = P("Alice");
        var bob = P("Bob", dependsOn: "Alice");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob) };

        vm.RemoveCommand.Execute(alice);

        Assert.Null(bob.DependsOn);
    }

    [Fact]
    public void Remove_FiresRemoveParticipantRequested()
    {
        var alice = P("Alice");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice) };
        ParticipantDraftViewModel? received = null;
        vm.RemoveParticipantRequested += (_, p) => received = p;

        vm.RemoveCommand.Execute(alice);

        Assert.Same(alice, received);
    }

    [Fact]
    public void Remove_NullParticipants_DoesNotThrow()
    {
        var vm = new ParticipantsEditorViewModel { Participants = null };
        var ex = Record.Exception(() => vm.RemoveCommand.Execute(P("Alice")));
        Assert.Null(ex);
    }

    // ── RequestDependencySelection ────────────────────────────────────────────

    [Fact]
    public void RequestDependencySelection_NullParticipants_DoesNotFireEvent()
    {
        var vm = new ParticipantsEditorViewModel { Participants = null };
        var fired = false;
        vm.DependencySelectionRequested += (_, _) => fired = true;

        vm.RequestDependencySelectionCommand.Execute("Alice");

        Assert.False(fired);
    }

    [Fact]
    public void RequestDependencySelection_UnknownParticipantName_DoesNotFireEvent()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List(P("Alice")) };
        var fired = false;
        vm.DependencySelectionRequested += (_, _) => fired = true;

        vm.RequestDependencySelectionCommand.Execute("Unknown");

        Assert.False(fired);
    }

    [Fact]
    public void RequestDependencySelection_IncludesIndependentOptionFirst()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List(P("Alice"), P("Bob")) };
        DependencySelectionArgs? args = null;
        vm.DependencySelectionRequested += (_, a) => args = a;

        vm.RequestDependencySelectionCommand.Execute("Alice");

        Assert.NotNull(args);
        Assert.Equal(AppResources.GroupDetails_DependencyIndependent, args.Options[0]);
    }

    [Fact]
    public void RequestDependencySelection_ExcludesSelf_FromEligibleResponsibles()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List(P("Alice"), P("Bob")) };
        DependencySelectionArgs? args = null;
        vm.DependencySelectionRequested += (_, a) => args = a;

        vm.RequestDependencySelectionCommand.Execute("Alice");

        Assert.NotNull(args);
        Assert.DoesNotContain("Alice", args.Options);
    }

    [Fact]
    public void RequestDependencySelection_ExcludesAlreadyDependentParticipants()
    {
        var alice = P("Alice");
        var bob = P("Bob", dependsOn: "Alice"); // Bob is a dependent
        var charlie = P("Charlie");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob, charlie) };
        DependencySelectionArgs? args = null;
        vm.DependencySelectionRequested += (_, a) => args = a;

        vm.RequestDependencySelectionCommand.Execute("Alice");

        Assert.NotNull(args);
        Assert.DoesNotContain("Bob", args.Options);
        Assert.Contains("Charlie", args.Options);
    }

    // ── ApplyDependencySelection ──────────────────────────────────────────────

    [Fact]
    public void ApplyDependencySelection_Independent_ClearsDependsOn()
    {
        var alice = P("Alice", dependsOn: "Bob");
        var bob = P("Bob");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob) };

        vm.ApplyDependencySelection("Alice", AppResources.GroupDetails_DependencyIndependent);

        Assert.Null(alice.DependsOn);
    }

    [Fact]
    public void ApplyDependencySelection_ValidResponsible_SetsDependsOn()
    {
        var alice = P("Alice");
        var bob = P("Bob");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob) };

        vm.ApplyDependencySelection("Alice", "Bob");

        Assert.Equal("Bob", alice.DependsOn);
    }

    [Fact]
    public void ApplyDependencySelection_ResponsibleNotInList_SetsResponsibleNotFoundError()
    {
        var alice = P("Alice");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice) };

        vm.ApplyDependencySelection("Alice", "NonExistent");

        Assert.Equal(AppResources.Validation_ResponsiblePersonNotFound, vm.StatusText);
    }

    [Fact]
    public void ApplyDependencySelection_AlreadyDependentParticipant_SetsResponsibleNotFoundError()
    {
        // Bob depends on Alice. Trying to assign Bob as Charlie's responsible is
        // ineligible (Bob has DependsOn != null) and should set the not-found error.
        var alice = P("Alice");
        var bob = P("Bob", dependsOn: "Alice"); // dependent — not eligible
        var charlie = P("Charlie");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob, charlie) };

        vm.ApplyDependencySelection("Charlie", "Bob");

        Assert.Equal(AppResources.Validation_ResponsiblePersonNotFound, vm.StatusText);
    }

    [Fact]
    public void ApplyDependencySelection_Success_FiresDependencyChanged()
    {
        var alice = P("Alice");
        var bob = P("Bob");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob) };
        ParticipantDraftViewModel? received = null;
        vm.DependencyChanged += (_, p) => received = p;

        vm.ApplyDependencySelection("Alice", "Bob");

        Assert.Same(alice, received);
    }

    [Fact]
    public void ApplyDependencySelection_Success_ClearsStatus()
    {
        var alice = P("Alice");
        var bob = P("Bob");
        var vm = new ParticipantsEditorViewModel { Participants = List(alice, bob) };
        vm.StatusText = "old error";

        vm.ApplyDependencySelection("Alice", AppResources.GroupDetails_DependencyIndependent);

        Assert.Equal(string.Empty, vm.StatusText);
    }

    // ── ReorderParticipants ───────────────────────────────────────────────────

    [Fact]
    public void ReorderParticipants_MovesRootBeforeItsDependent()
    {
        var bob = P("Bob", dependsOn: "Alice"); // Bob is a dependent
        var alice = P("Alice");                 // Alice is a root
        var list = List(bob, alice);            // wrong order: Bob first
        var vm = new ParticipantsEditorViewModel { Participants = list };

        vm.ReorderParticipants();

        Assert.Equal("Alice", list[0].Name);
        Assert.Equal("Bob", list[1].Name);
    }

    [Fact]
    public void ReorderParticipants_SingleItem_DoesNotThrow()
    {
        var vm = new ParticipantsEditorViewModel { Participants = List(P("Alice")) };
        var ex = Record.Exception(() => vm.ReorderParticipants());
        Assert.Null(ex);
    }

    [Fact]
    public void ReorderParticipants_NullParticipants_DoesNotThrow()
    {
        var vm = new ParticipantsEditorViewModel { Participants = null };
        var ex = Record.Exception(() => vm.ReorderParticipants());
        Assert.Null(ex);
    }

    // ── Regression: Participants injection (binding fix) ─────────────────────

    [Fact]
    public void Participants_WhenSet_IsStoredAndVisibleToValidation()
    {
        // Regression: the collection injected from the parent page's ViewModel must
        // be seen by duplicate-name validation. Previously, due to the BindingContext
        // bug, validation always ran against null and never caught duplicates.
        var vm = new ParticipantsEditorViewModel();
        vm.Participants = List(P("Alice"));
        vm.NewParticipantName = "Alice";

        vm.AddCommand.Execute(null);

        Assert.Equal(AppResources.Validation_PersonNameMustBeUnique, vm.StatusText);
    }

    [Fact]
    public void Participants_WhenSet_CollectionIsReflectedInValidation()
    {
        // Regression: after injection of a real collection from the host ViewModel,
        // duplicate-name validation must see the injected items.
        var vm = new ParticipantsEditorViewModel();
        vm.Participants = List(P("Alice"));
        vm.NewParticipantName = "Alice";

        vm.AddCommand.Execute(null);

        Assert.Equal(AppResources.Validation_PersonNameMustBeUnique, vm.StatusText);
    }
}
