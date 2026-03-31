using Microsoft.Maui.Controls;

namespace LuSplit.App.Features.Groups.Shared;

public partial class GroupHeaderView : ContentView
{
    public static readonly BindableProperty ImagePathProperty =
        BindableProperty.Create(
            nameof(ImagePath),
            typeof(string),
            typeof(GroupHeaderView),
            default(string),
            propertyChanged: OnImagePathChanged);

    public static readonly BindableProperty GroupNameProperty =
        BindableProperty.Create(
            nameof(GroupName),
            typeof(string),
            typeof(GroupHeaderView),
            default(string));

    public static readonly BindableProperty GroupSummaryProperty =
        BindableProperty.Create(
            nameof(GroupSummary),
            typeof(string),
            typeof(GroupHeaderView),
            default(string),
            propertyChanged: OnGroupSummaryChanged);

    public GroupHeaderView()
    {
        InitializeComponent();
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public string? GroupName
    {
        get => (string?)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public string? GroupSummary
    {
        get => (string?)GetValue(GroupSummaryProperty);
        set => SetValue(GroupSummaryProperty, value);
    }

    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);

    public bool HasNoImage => !HasImage;

    public bool HasSummary => !string.IsNullOrWhiteSpace(GroupSummary);

    public ImageSource? BackgroundImageSource =>
        HasImage ? ImageSource.FromFile(ImagePath!) : null;

    private static void OnImagePathChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (GroupHeaderView)bindable;
        view.OnPropertyChanged(nameof(HasImage));
        view.OnPropertyChanged(nameof(HasNoImage));
        view.OnPropertyChanged(nameof(BackgroundImageSource));
    }

    private static void OnGroupSummaryChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (GroupHeaderView)bindable;
        view.OnPropertyChanged(nameof(HasSummary));
    }
}