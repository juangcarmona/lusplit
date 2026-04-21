namespace LuSplit.Contracts.ControlPlane;

// ── Device registration ──────────────────────────────────────────────────────

public sealed record RegisterDeviceRequest(
    string DeviceId,
    string DeviceName,
    string Platform,
    byte[] PublicKey);

public sealed record RegisterDeviceResponse(string DeviceId);

// ── Group management ─────────────────────────────────────────────────────────

public sealed record CreateGroupRequest(
    string GroupId,
    string OwnerId,
    string OwnerDeviceId,
    int InitialKeyVersion,
    IReadOnlyList<WrappedKeyEntryDto> WrappedKeys);

public sealed record CreateGroupResponse(string GroupId, string ContainerName);

public sealed record GroupInfoResponse(
    string GroupId,
    string OwnerId,
    int CurrentKeyVersion,
    DateTimeOffset CreatedAt);

// ── Sync token ───────────────────────────────────────────────────────────────

public sealed record SyncTokenRequest(string GroupId, string DeviceId);

public sealed record SyncTokenResponse(
    string SasToken,
    string ContainerName,
    DateTimeOffset ExpiresAt);

// ── Invitations ──────────────────────────────────────────────────────────────

public sealed record CreateInvitationRequest(
    string GroupId,
    string InvitedByUserId,
    string InvitedByDeviceId);

public sealed record CreateInvitationResponse(
    string InvitationId,
    string InvitationCode,
    DateTimeOffset ExpiresAt);

public sealed record InvitationInfoResponse(
    string InvitationId,
    string GroupId,
    string GroupName,
    string InvitedByDisplayName,
    DateTimeOffset ExpiresAt,
    string Status);

public sealed record AcceptInvitationRequest(
    string InvitationCode,
    string AcceptingUserId,
    string AcceptingDeviceId,
    byte[] DevicePublicKey);

public sealed record AcceptInvitationResponse(
    string GroupId,
    string ContainerName,
    IReadOnlyList<WrappedKeyEntryDto> WrappedKeys);

public sealed record PendingInvitationDto(
    string InvitationId,
    string InvitationCode,
    DateTimeOffset ExpiresAt,
    string Status);

public sealed record ListPendingInvitationsResponse(IReadOnlyList<PendingInvitationDto> Invitations);

// ── Key distribution ─────────────────────────────────────────────────────────

public sealed record WrappedKeyEntryDto(string DeviceId, byte[] WrappedKey);

public sealed record GetGroupKeysResponse(
    string GroupId,
    int KeyVersion,
    IReadOnlyList<WrappedKeyEntryDto> WrappedKeys);

public sealed record UploadRotatedKeyRequest(
    int NewKeyVersion,
    IReadOnlyList<WrappedKeyEntryDto> WrappedKeys);

public sealed record GetWrappedKeysForDeviceResponse(
    IReadOnlyList<GroupKeyVersionDto> KeyVersions);

public sealed record GroupKeyVersionDto(int KeyVersion, byte[] WrappedKey);

// ── Member management ────────────────────────────────────────────────────────

public sealed record ListMembersResponse(IReadOnlyList<MemberDto> Members);

public sealed record MemberDto(
    string UserId,
    string DeviceId,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record RevokeMemberRequest(string UserId, string RevokedByUserId);

public sealed record TransferOwnershipRequest(string NewOwnerUserId, string CallerUserId);

// ── Device management ────────────────────────────────────────────────────────

public sealed record DeviceDto(
    string DeviceId,
    string DeviceName,
    string Platform,
    DateTimeOffset RegisteredAt,
    bool IsRevoked);

public sealed record ListDevicesResponse(IReadOnlyList<DeviceDto> Devices);
