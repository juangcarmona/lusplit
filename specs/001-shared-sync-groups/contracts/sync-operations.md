# Sync Operations Contract

**Feature**: 001-shared-sync-groups
**Date**: 2026-04-18

## Overview

Sync operations are the unit of data exchange between devices in a shared group. Each operation is an immutable, versioned, encrypted record stored as an individual blob in the group's Azure Blob Storage container. This document defines the operation schema, blob naming conventions, snapshot format, and sync protocol behavior.

---

## Operation Schema

Each operation, before encryption, has the following logical structure:

```json
{
  "operationId": "string (UUID)",
  "groupId": "string",
  "deviceId": "string",
  "userId": "string",
  "hlcTimestamp": "long (hybrid logical clock value)",
  "operationType": "string (enum)",
  "entityId": "string (ID of the affected entity)",
  "payload": { },
  "keyVersion": "int",
  "createdAt": "ISO 8601 datetime (device wall clock, display only)"
}
```

### Operation Types

| Type | EntityId refers to | Payload contains |
|------|-------------------|-----------------|
| `AddExpense` | New expense ID | Full expense data (amount, payer, description, split definition, category, date) |
| `EditExpense` | Existing expense ID | Changed fields only (delta) |
| `DeleteExpense` | Existing expense ID | Empty (tombstone) |
| `AddParticipant` | New participant ID | Participant data (name, economic unit) |
| `EditParticipant` | Existing participant ID | Changed fields only |
| `RecordPayment` | New transfer ID | Transfer data (from, to, amount) |
| `EditPayment` | Existing transfer ID | Changed fields only |
| `DeletePayment` | Existing transfer ID | Empty (tombstone) |
| `AddTransfer` | New transfer ID | Manual transfer data |
| `EditTransfer` | Existing transfer ID | Changed fields only |
| `DeleteTransfer` | Existing transfer ID | Empty (tombstone) |

### Payload Schemas (pre-encryption)

#### AddExpense Payload
```json
{
  "amount": "decimal",
  "currency": "string (ISO 4217)",
  "description": "string",
  "payerId": "string (participant ID)",
  "date": "ISO 8601 date",
  "category": "string (enum, optional)",
  "splitDefinition": {
    "type": "Equal | Fixed | Remainder",
    "components": [
      {
        "participantId": "string",
        "type": "Fixed | Remainder",
        "amount": "decimal (for Fixed)"
      }
    ]
  }
}
```

#### EditExpense Payload (delta only)
```json
{
  "amount": "decimal (if changed)",
  "description": "string (if changed)",
  "payerId": "string (if changed)",
  "date": "ISO 8601 date (if changed)",
  "category": "string (if changed)",
  "splitDefinition": { "... (if changed)" }
}
```

#### AddParticipant Payload
```json
{
  "name": "string",
  "economicUnitId": "string (optional)"
}
```

#### RecordPayment / AddTransfer Payload
```json
{
  "fromParticipantId": "string",
  "toParticipantId": "string",
  "amount": "decimal",
  "currency": "string (ISO 4217)",
  "date": "ISO 8601 date",
  "transferType": "string (enum)"
}
```

---

## Encryption Envelope

Each operation is stored as an encrypted blob. The blob content is:

```
[4 bytes: key version (int32, big-endian)]
[12 bytes: AES-GCM nonce]
[N bytes: AES-GCM ciphertext (encrypted JSON operation)]
[16 bytes: AES-GCM authentication tag]
```

- The key version header is unencrypted so the reader knows which group key to use for decryption.
- The nonce is generated fresh for every operation using a cryptographically secure random generator.
- The authentication tag provides integrity verification.

---

## Blob Naming Convention

All blobs live in the group's container (`group-{groupId}`).

| Blob type | Path pattern | Example |
|-----------|-------------|---------|
| Operation | `ops/{hlcTimestamp}_{deviceId}.enc` | `ops/1713450000001_d8f2a3b1.enc` |
| Snapshot | `snapshots/{hlcTimestamp}.enc` | `snapshots/1713450000100.enc` |
| Membership metadata | `meta/membership.enc` | `meta/membership.enc` |

### Naming Rules

- HLC timestamps are zero-padded to 20 digits for lexicographic sort order.
- DeviceId in operation blob names is truncated to 8 characters (first segment of UUID) for path brevity. The full deviceId is inside the encrypted payload.
- Blobs are immutable after creation. No in-place updates.
- Listing blobs in `ops/` sorted lexicographically yields causal order.

---

## Snapshot Schema

A snapshot is a full encrypted dump of the group's current state at a point in time.

```json
{
  "snapshotId": "string (UUID)",
  "groupId": "string",
  "createdByDeviceId": "string",
  "hlcTimestamp": "long",
  "keyVersion": "int",
  "state": {
    "participants": [ "... full participant list" ],
    "expenses": [ "... full expense list" ],
    "transfers": [ "... full transfer list" ],
    "economicUnits": [ "... full economic unit list" ]
  }
}
```

The snapshot is encrypted using the same envelope format as operations.

### Snapshot Policy

- A device creates a snapshot after applying 100+ operations since the last snapshot.
- The 3 most recent snapshots are retained. Older snapshots are deleted during compaction.
- Snapshot creation acquires a lightweight lock via the control plane (`POST /groups/{groupId}/snapshot-lock`) to prevent concurrent snapshot creation. Lock duration: 60 seconds.

---

## Sync Protocol Flow

### Pull (download remote changes)

```
1. Client requests sync token from control plane (POST /groups/{groupId}/sync-token)
2. Control plane verifies membership, returns scoped SAS URI (15-min TTL)
3. Client lists blobs in ops/ with prefix filter > last synced HLC timestamp
4. Client downloads new operation blobs
5. Client decrypts each operation using the appropriate group key (from key version header)
6. Client applies operations to local SQLite in HLC order
7. Client resolves conflicts (LWW by HLC for same-entity edits; delete wins over edit)
8. Client updates local SyncCursor with the latest applied HLC timestamp
9. Client generates ActivityEntries for applied operations
```

### Push (upload local changes)

```
1. Client collects pending local operations (not yet uploaded)
2. Client assigns HLC timestamps: max(local_hlc, last_known_remote_hlc) + 1
3. Client encrypts each operation with the current group key
4. Client requests sync token if needed (reuse if still valid)
5. Client uploads encrypted operation blobs to ops/
6. Client marks local operations as uploaded
7. If operation count since last snapshot >= 100, client creates a snapshot
```

### Initial Sync (new device / new member)

```
1. Client requests wrapped group keys from control plane (GET /groups/{groupId}/keys)
2. Client unwraps group keys using device private key
3. Client stores unwrapped keys in SecureStorage
4. Client requests sync token
5. Client downloads latest snapshot (if exists)
6. Client decrypts and applies snapshot to local SQLite
7. Client downloads operations newer than the snapshot's HLC timestamp
8. Client applies operations (same as Pull step 5-9)
```

---

## Conflict Resolution Rules

| Scenario | Resolution | Deterministic? |
|----------|-----------|---------------|
| Two devices add different expenses | Both accepted (commutative) | Yes |
| Two devices edit different fields of the same expense | Both field changes applied (merge) | Yes |
| Two devices edit the same field of the same expense | Higher HLC timestamp wins (LWW) | Yes |
| One device edits, another deletes the same expense | Delete wins (tombstone) | Yes |
| Two devices delete the same expense | Idempotent (both deletions resolve to same state) | Yes |
| Two devices add the same participant name | Both accepted as separate participants (unique IDs) | Yes |

### Conflict Visibility

- When LWW resolves a same-field edit conflict, the "losing" device sees an ActivityEntry: "Expense updated by [winner's display name]".
- When the user opens the affected expense after a conflict resolution, a lightweight review prompt is shown.
- The review prompt is informational only — the user can edit the expense normally if the resolution was incorrect.

---

## Idempotency Contract

- Every operation has a unique `operationId`.
- Applying the same `operationId` twice to local state MUST produce the same result as applying it once.
- The sync engine MUST track applied `operationId` values and skip duplicates.
- Upload of an operation blob with a name that already exists in Blob Storage MUST be treated as a no-op (the blob is immutable and identical).
