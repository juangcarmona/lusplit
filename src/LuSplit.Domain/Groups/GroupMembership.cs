namespace LuSplit.Domain.Groups;

public enum MemberRole { Owner, Member }

public sealed record GroupMembership(
    string GroupId,
    string UserId,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    bool IsRevoked,
    DateTimeOffset? RevokedAt);
