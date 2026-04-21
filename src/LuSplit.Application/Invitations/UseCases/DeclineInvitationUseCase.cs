using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Shared.Ports;

namespace LuSplit.Application.Invitations.UseCases;

/// <summary>
/// Declines a group invitation — notifies the control plane that the user has declined.
/// </summary>
public sealed class DeclineInvitationUseCase
{
    private readonly IInvitationPort _invitationPort;
    private readonly IAuthPort _authPort;

    public DeclineInvitationUseCase(IInvitationPort invitationPort, IAuthPort authPort)
    {
        _invitationPort = invitationPort;
        _authPort = authPort;
    }

    public async Task ExecuteAsync(string invitationCode, CancellationToken ct = default)
    {
        var userId = await _authPort.GetCurrentUserIdAsync(ct)
            ?? throw new InvalidOperationException("User is not authenticated.");

        await _invitationPort.DeclineInvitationAsync(invitationCode, userId, ct);
    }
}
