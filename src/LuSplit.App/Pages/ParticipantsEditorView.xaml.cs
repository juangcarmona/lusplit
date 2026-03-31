using System.Collections.ObjectModel;
using LuSplit.App.Resources.Localization;

namespace LuSplit.App.Pages;

public partial class ParticipantsEditorView : ContentView
{
    private readonly ParticipantsEditorViewModel _viewModel;

    // ── BindableProperties ───────────────────────────────────────────────────

    public static readonly BindableProperty ParticipantsProperty =
        BindableProperty.Create(nameof(Participants), typeof(ObservableCollection<ParticipantDraftViewModel>),
            typeof(ParticipantsEditorView), null, propertyChanged: OnParticipantsChanged);

    public static readonly BindableProperty CanEditProperty =
        BindableProperty.Create(nameof(CanEdit), typeof(bool), typeof(ParticipantsEditorView), true,
            propertyChanged: OnCanEditChanged);

    public static readonly BindableProperty HostPageProperty =
        BindableProperty.Create(nameof(HostPage), typeof(Page), typeof(ParticipantsEditorView), null);

    public ObservableCollection<ParticipantDraftViewModel>? Participants
    {
        get => (ObservableCollection<ParticipantDraftViewModel>?)GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }

    public bool CanEdit
    {
        get => (bool)GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public Page? HostPage
    {
        get => (Page?)GetValue(HostPageProperty);
        set => SetValue(HostPageProperty, value);
    }

    // ── Parent-facing events forwarded from the ViewModel ────────────────────

    /// <summary>Fired after name validation passes. Host is responsible for appending the participant to the collection.</summary>
    public event EventHandler<string>? AddParticipantRequested
    {
        add => _viewModel.AddParticipantRequested += value;
        remove => _viewModel.AddParticipantRequested -= value;
    }

    /// <summary>Fired after dependents are cleared. Host is responsible for removing the item from the collection.</summary>
    public event EventHandler<ParticipantDraftViewModel>? RemoveParticipantRequested
    {
        add => _viewModel.RemoveParticipantRequested += value;
        remove => _viewModel.RemoveParticipantRequested -= value;
    }

    /// <summary>Fired after the VM's <see cref="ParticipantDraftViewModel.DependsOn"/> has already been updated. Host
    /// persists the change (API call) for edit-mode; create-mode ignores this event.</summary>
    public event EventHandler<ParticipantDraftViewModel>? DependencyChanged
    {
        add => _viewModel.DependencyChanged += value;
        remove => _viewModel.DependencyChanged -= value;
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    public ParticipantsEditorView()
    {
        _viewModel = new ParticipantsEditorViewModel();
        InitializeComponent();
        // Set BindingContext on the inner layout, NOT on the ContentView.
        // The ContentView must inherit the page's BindingContext so that
        // parent XAML bindings like Participants="{Binding Participants}" resolve
        // against the page's ViewModel rather than the inner ViewModel.
        InnerLayout.BindingContext = _viewModel;

        _viewModel.DependencySelectionRequested += OnDependencySelectionRequested;
    }

    // ── BindableProperty change callbacks ────────────────────────────────────

    private static void OnParticipantsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ParticipantsEditorView view)
        {
            var collection = newValue as ObservableCollection<ParticipantDraftViewModel>;
            view._viewModel.Participants = collection;
            view.ParticipantsList.ItemsSource = collection;
        }
    }

    private static void OnCanEditChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ParticipantsEditorView view && newValue is bool canEdit)
            view._viewModel.CanEdit = canEdit;
    }

    // ── Dependency selection dialog (MAUI-specific, stays in code-behind) ────

    private async void OnDependencySelectionRequested(object? sender, DependencySelectionArgs args)
    {
        var hostPage = HostPage;
        if (hostPage is null) return;

        var selected = await hostPage.DisplayActionSheetAsync(
            AppResources.GroupDetails_DependsOnLabel,
            AppResources.Common_Cancel,
            null,
            args.Options.ToArray());

        if (string.IsNullOrEmpty(selected) ||
            string.Equals(selected, AppResources.Common_Cancel, StringComparison.Ordinal))
            return;

        _viewModel.ApplyDependencySelection(args.ParticipantName, selected);
    }
}
