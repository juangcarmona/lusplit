namespace LuSplit.App.Pages;

/// <summary>
/// Test stub for ParticipantDraftViewModel. Removes MAUI BindableObject and Thickness dependencies
/// while preserving the API surface used by GroupDetailsViewModel and GroupDetailsParticipantSorter.
/// </summary>
public sealed class ParticipantDraftViewModel
{
    public string? ParticipantId { get; }
    public string Name { get; }
    public bool CanRemove { get; }
    public string? DependsOn { get; set; }
    public bool IsDependent => !string.IsNullOrWhiteSpace(DependsOn);

    public ParticipantDraftViewModel(string name, string? participantId = null, bool canRemove = true)
    {
        Name = name;
        ParticipantId = participantId;
        CanRemove = canRemove;
    }

    public void Notify() { }
}
