using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.Queries;
using LuSplit.Application.Invitations.Queries;
using LuSplit.Application.Revocation.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Features.Members;

public sealed partial class MemberListViewModel : ObservableObject
{
    private readonly IGroupMemberPort _memberPort;
    private readonly GetGroupMembersQuery _getMembersQuery;
    private readonly GetPendingInvitationsQuery _getPendingInvitationsQuery;
    private readonly RevokeMemberUseCase _revokeUseCase;
    private readonly IAuthPort _authPort;
    private readonly ISharedGroupStateRepository _sharedGroupStateRepository;

    private string _groupId = string.Empty;
    private string? _currentUserId;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isOwner;

    public ObservableCollection<GroupMemberModel> Members { get; } = new();
    public ObservableCollection<PendingInvitationDto> PendingInvitations { get; } = new();

    /// <summary>Raised when the owner wants to navigate to the invite flow. Arg = groupId.</summary>
    public event EventHandler<string>? InviteRequested;

    public MemberListViewModel(
        IGroupMemberPort memberPort,
        GetGroupMembersQuery getMembersQuery,
        GetPendingInvitationsQuery getPendingInvitationsQuery,
        RevokeMemberUseCase revokeUseCase,
        IAuthPort authPort,
        ISharedGroupStateRepository sharedGroupStateRepository)
    {
        _memberPort = memberPort;
        _getMembersQuery = getMembersQuery;
        _getPendingInvitationsQuery = getPendingInvitationsQuery;
        _revokeUseCase = revokeUseCase;
        _authPort = authPort;
        _sharedGroupStateRepository = sharedGroupStateRepository;
    }

    public void Initialize(string groupId) => _groupId = groupId;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _currentUserId = await _authPort.GetCurrentUserIdAsync(CancellationToken.None);

            var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(_groupId, CancellationToken.None);
            IsOwner = sharedState is not null &&
                      string.Equals(sharedState.OwnerId, _currentUserId, StringComparison.OrdinalIgnoreCase);

            var members = await _getMembersQuery.ExecuteAsync(_groupId, sharedState?.OwnerId ?? string.Empty, null, CancellationToken.None);
            Members.Clear();
            foreach (var member in members)
                Members.Add(member);

            if (IsOwner)
            {
                var pending = await _getPendingInvitationsQuery.ExecuteAsync(_groupId, _currentUserId!, CancellationToken.None);
                PendingInvitations.Clear();
                foreach (var inv in pending)
                    PendingInvitations.Add(inv);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RevokeMemberAsync(GroupMemberModel member)
    {
        if (member is null || !IsOwner) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _revokeUseCase.ExecuteAsync(_groupId, member.UserId, _currentUserId!, CancellationToken.None);
            Members.Remove(member);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task TransferOwnershipAsync(GroupMemberModel member)
    {
        if (member is null || !IsOwner) return;
        // Implemented in T103+ key rotation phase
    }

    [RelayCommand]
    private void NavigateToInvite()
    {
        if (IsOwner && !string.IsNullOrWhiteSpace(_groupId))
            InviteRequested?.Invoke(this, _groupId);
    }
}
