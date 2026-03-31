using System.Collections.ObjectModel;
using LuSplit.App.Services.Presentation;

namespace LuSplit.App.Features.Activity;

public sealed class ActivityCompactDayGroupViewModel : ObservableCollection<CompactEventEntryViewModel>
{
    public string Title { get; }

    public ActivityCompactDayGroupViewModel(string title, IEnumerable<CompactEventEntryViewModel> items)
        : base(items)
    {
        Title = title;
    }
}
