using System.Collections.ObjectModel;
using LuSplit.App.Services;

namespace LuSplit.App.Pages;

public sealed class ActivityCompactDayGroupViewModel : ObservableCollection<CompactEventEntryViewModel>
{
    public string Title { get; }

    public ActivityCompactDayGroupViewModel(string title, IEnumerable<CompactEventEntryViewModel> items)
        : base(items)
    {
        Title = title;
    }
}
