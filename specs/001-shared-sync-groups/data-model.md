# Data Model: Shared Synchronized Groups

**Feature**: 001-shared-sync-groups
**Date**: 2026-04-18
**Source**: [spec.md](spec.md) Key Entities + [research.md](research.md)

## Entity Overview

```
┌──────────┐       1    ┌──────────┐
│   User   │───────────*│  Device  │
└──────────┘            └──────────┘
     │ 1                     │
     │                       │ holds keypair
     │                       │
     ├──── owns ──── 0..*  ┌──────────────┐
     │                     │ SharedGroup   │
     ├──── member ── 0..*  │              │
     │                     └──────────────┘
     │                       │ 1
     │                       │
     │                  ┌────┴────┐
     │              0..*│GroupKey │ (key chain)
     │                  └─────────┘
     │                       │
     │                  ┌────┴────────┐
     │              0..*│ Operation   │
     │                  └─────────────┘
     │                       │
     │                  ┌────┴────────┐
     │              0..*│ActivityEntry│
     │                  └─────────────┘
     │
     └──── invited ── 0..*  ┌──────────┐
                            │Invitation│
                            └──────────┘
```

## Entities

### User

Represents an authenticated person in Microsoft Entra External ID.

| Field | Type | Description |
|-------|------|-------------|
| UserId | string (Entra object ID) | Unique, immutable identity from the identity provider |
| DisplayName | string | Friendly name shown in member lists and activity entries |
| PublicKey | byte[] | RSA public key for asymmetric key wrapping (uploaded at registration) |
| CreatedAt | DateTimeOffset | When the user account was first registered with LuSplit |

**Validation rules**:
- DisplayName is required, max 100 characters, no control characters.
- PublicKey must be a valid RSA public key (minimum 2048 bits).
- UserId is immutable after creation.

**Relationships**: Has 0..* Devices. May be owner of 0..* SharedGroups. May be member of 0..* SharedGroups.

---

### Device

Represents a registered physical device bound to a user account.

| Field | Type | Description |
|-------|------|-------------|
| DeviceId | string (app-generated UUID) | Unique device identifier generated on first sign-in |
| UserId | string | Owner of this device |
| DeviceName | string | Human-readable name (e.g., "Juan's Pixel 9") |
| PublicKey | byte[] | Device-bound RSA public key for per-device key wrapping |
| RegisteredAt | DateTimeOffset | When this device was registered |
| LastSeenAt | DateTimeOffset | Last successful control-plane interaction |
| IsRevoked | bool | Whether the device has been revoked |
| RevokedAt | DateTimeOffset? | When the device was revoked, if applicable |

**Validation rules**:
- DeviceId must be a valid UUID.
- DeviceName is required, max 80 characters.
- PublicKey must be a valid RSA public key (minimum 2048 bits).
- A revoked device cannot be un-revoked; a new registration is required.

**Relationships**: Belongs to exactly 1 User. Holds 0..* SyncCursors (one per shared group).

---

### SharedGroup

Extends the existing local `Group` entity with sharing, membership, and sync metadata. The existing `Group` fields (name, currency, participants, etc.) are unchanged.

| Field | Type | Description |
|-------|------|-------------|
| GroupId | string (existing group ID) | Matches the local group's identifier |
| RemoteContainerName | string | Blob Storage container name (`group-{groupId}`) |
| OwnerId | string (UserId) | The user who owns this shared group |
| IsShared | bool | Whether this group has been shared (true) or is local-only (false) |
| SharedAt | DateTimeOffset? | When the group was shared or converted |
| CurrentKeyVersion | int | Version number of the current active group key |
| SyncStatus | enum | UpToDate, Syncing, PendingLocalChanges, SyncError |
| IsReadOnly | bool | True if owner left/deleted without transferring ownership |

**Validation rules**:
- A shared group must have exactly one OwnerId.
- CurrentKeyVersion must be >= 1 for shared groups.
- IsShared is one-way: once true, it cannot be reverted to false.

**Relationships**: Owned by 1 User. Has 0..* Members (User). Has 1..* GroupKeys (key chain). Has 0..* Operations. Has 0..* Invitations.

---

### GroupMembership

Junction entity representing a user's membership in a shared group.

| Field | Type | Description |
|-------|------|-------------|
| GroupId | string | The shared group |
| UserId | string | The member |
| Role | enum (Owner, Member) | Authorization level |
| JoinedAt | DateTimeOffset | When the user joined |
| IsRevoked | bool | Whether membership has been revoked |
| RevokedAt | DateTimeOffset? | When the membership was revoked |

**Validation rules**:
- Exactly one membership with Role=Owner per group.
- A revoked membership cannot be un-revoked.
- A user cannot have duplicate active memberships in the same group.

---

### GroupKey

A symmetric encryption key for a shared group, versioned for key rotation.

| Field | Type | Description |
|-------|------|-------------|
| GroupId | string | The shared group this key belongs to |
| KeyVersion | int | Monotonically increasing version number |
| CreatedAt | DateTimeOffset | When this key version was generated |
| CreatedByDeviceId | string | The device that generated this key |
| WrappedKeys | WrappedKeyEntry[] | The group key wrapped for each authorized device |

### WrappedKeyEntry

| Field | Type | Description |
|-------|------|-------------|
| DeviceId | string | Target device |
| WrappedKeyBlob | byte[] | Group key encrypted with the device's public key (RSA-OAEP) |

**Validation rules**:
- KeyVersion must be strictly greater than all previous versions for the same group.
- WrappedKeys must include an entry for every non-revoked device of every non-revoked member.
- The raw (unwrapped) group key is never stored on the control plane.

---

### Invitation

A time-limited, single-use token for joining a shared group.

| Field | Type | Description |
|-------|------|-------------|
| InvitationId | string (UUID) | Unique identifier |
| GroupId | string | The target group |
| CreatedByUserId | string | The owner who created this invitation |
| Token | string | Cryptographically random token embedded in the invitation link |
| CreatedAt | DateTimeOffset | When the invitation was created |
| ExpiresAt | DateTimeOffset | When the invitation expires (default: CreatedAt + 72 hours) |
| Status | enum (Pending, Accepted, Declined, Cancelled, Expired) | Current state |
| AcceptedByUserId | string? | The user who accepted, if accepted |
| AcceptedAt | DateTimeOffset? | When the invitation was accepted |

**Validation rules**:
- Token must be cryptographically random, minimum 32 bytes (URL-safe base64 encoded).
- ExpiresAt must be after CreatedAt.
- Status transitions: Pending → Accepted, Pending → Declined, Pending → Cancelled, Pending → Expired. No other transitions allowed.
- An accepted invitation cannot be reused.
- Only the group owner can create or cancel invitations.

**State transitions**:
```
Pending ──┬── accept() ──→ Accepted
          ├── decline() ──→ Declined
          ├── cancel() ───→ Cancelled
          └── expire() ───→ Expired (automatic on ExpiresAt)
```

---

### Operation

An atomic, versioned, immutable change to group state. The unit of synchronization.

| Field | Type | Description |
|-------|------|-------------|
| OperationId | string (UUID) | Unique identifier |
| GroupId | string | The group this operation belongs to |
| DeviceId | string | The device that created this operation |
| UserId | string | The user who created this operation |
| HlcTimestamp | long | Hybrid Logical Clock timestamp for causal ordering |
| OperationType | enum | AddExpense, EditExpense, DeleteExpense, AddParticipant, EditParticipant, RecordPayment, EditPayment, DeletePayment, AddTransfer, EditTransfer, DeleteTransfer |
| EntityId | string | The ID of the entity being modified |
| Payload | byte[] (encrypted) | The operation data, encrypted with the current group key |
| KeyVersion | int | Which group key version was used to encrypt the payload |
| CreatedAt | DateTimeOffset | Wall-clock time on the device (for display only, not ordering) |

**Validation rules**:
- OperationId must be globally unique.
- HlcTimestamp must be monotonically increasing per device.
- Payload is opaque to the control plane and Blob Storage — only the group key holder can decrypt.
- OperationType + EntityId + Payload together define the mutation. Operations are idempotent: applying the same OperationId twice has no additional effect.

---

### SyncCursor

Per-device, per-group marker for incremental sync.

| Field | Type | Description |
|-------|------|-------------|
| DeviceId | string | The device |
| GroupId | string | The shared group |
| LastSyncedHlcTimestamp | long | The HLC timestamp of the last successfully applied remote operation |
| LastSyncedAt | DateTimeOffset | Wall-clock time of last successful sync |

**Stored locally on-device only** — not sent to remote storage or the control plane.

---

### ActivityEntry

A human-readable record for the group activity history.

| Field | Type | Description |
|-------|------|-------------|
| EntryId | string (UUID) | Unique identifier |
| GroupId | string | The group |
| UserId | string | The user who caused the activity |
| DisplayName | string | Friendly name at time of entry (denormalized) |
| EntryType | enum | ExpenseAdded, ExpenseEdited, ExpenseDeleted, PaymentRecorded, MemberJoined, MemberRevoked, OwnershipTransferred, ConflictResolved, KeyRotated |
| Description | string | Human-readable message (e.g., "Alex added an expense") |
| RelatedEntityId | string? | The entity involved, if applicable |
| OccurredAt | DateTimeOffset | When the activity happened |

**Validation rules**:
- Description must use LuSplit voice and tone (friendly, no jargon).
- ActivityEntries are derived from Operations during sync application — they are not synced as separate operations.

---

## Storage Location Summary

| Entity | Stored Where | Notes |
|--------|-------------|-------|
| User | Control plane (Entra External ID + Functions metadata store) | Profile and public key |
| Device | Control plane (Functions metadata store) | Registration and public key |
| SharedGroup (metadata) | Control plane + local SQLite | Membership, key versions on control plane; full group data locally |
| GroupMembership | Control plane (Functions metadata store) | Source of truth for authorization |
| GroupKey (wrapped) | Control plane (Functions metadata store) | Wrapped keys only; raw key never stored server-side |
| Invitation | Control plane (Functions metadata store) | Lifecycle managed by control plane |
| Operation | Azure Blob Storage (encrypted) + local SQLite | Encrypted blobs remotely; decrypted operations applied locally |
| SyncCursor | Local SQLite only | Device-local state |
| ActivityEntry | Local SQLite only | Derived from operations, not synced independently |
