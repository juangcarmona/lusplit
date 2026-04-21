namespace LuSplit.Domain.Invitations;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled,
    Expired
}

public sealed record Invitation(
    string InvitationId,
    string GroupId,
    string CreatedByUserId,
    string Token,
    string TokenHash,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RedeemedAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool CanTransitionTo(InvitationStatus next) => (Status, next) switch
    {
        (InvitationStatus.Pending, InvitationStatus.Accepted)   => true,
        (InvitationStatus.Pending, InvitationStatus.Declined)   => true,
        (InvitationStatus.Pending, InvitationStatus.Cancelled)  => true,
        (InvitationStatus.Pending, InvitationStatus.Expired)    => true,
        _ => false
    };
}
