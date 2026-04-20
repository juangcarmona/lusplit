# Quickstart: Shared Synchronized Groups

**Feature**: 001-shared-sync-groups
**Date**: 2026-04-18

## What This Feature Adds

LuSplit gains the ability to share expense groups across users and devices. A group owner can invite friends, and everyone's expenses stay synchronized automatically. All data is encrypted before leaving the device, and the system works fully offline.

## Key Concepts

| Concept | What It Means |
|---------|--------------|
| **Shared group** | A group that syncs across invited members. Looks and works like a local group, but with a sync indicator. |
| **Local group** | The existing single-device group. Unchanged by this feature. |
| **Control plane** | A small set of Azure Functions that handle invitations, authorization, and key distribution. Does NOT see your expense data. |
| **Operation** | A single change (add/edit/delete an expense). Encrypted and synced as a blob. |
| **Group key** | A secret key that encrypts your group's data. Only group members can decrypt. |

## How It Works (User Perspective)

### Create a shared group
1. Sign in (optional — only needed for sharing)
2. Create a new group or open an existing one
3. Tap "Share this group"
4. The group is now shared — you're the owner

### Invite someone
1. Open the shared group → Settings → Invite
2. A link is generated and the share sheet opens
3. Send the link via any messaging app
4. The friend taps the link, signs in, and joins

### Sync happens automatically
- When you add an expense, it syncs to all members
- When you're offline, changes are saved locally and sync later
- A small icon shows sync status (up to date, pending, or offline)

### Revoke a member
1. Open group settings → Members
2. Tap a member → Remove
3. They lose access immediately. Keys are rotated.

## Architecture Overview

```
┌─────────────┐         ┌──────────────┐         ┌──────────────┐
│  MAUI App   │◄──────►│ Control Plane │◄───────►│  Blob Storage│
│  (Device)   │  HTTPS  │ (Functions)  │ Managed │  (Encrypted) │
│             │         │              │ Identity│              │
│ Local SQLite│         │ Entra Auth   │         │ Per-group    │
│ SecureStore │         │ Invitations  │         │ containers   │
│ Encryption  │         │ Key Distrib. │         │              │
│ Sync Engine │         │ SAS Issuance │         │              │
└─────────────┘         └──────────────┘         └──────────────┘
                               │
                        ┌──────┴───────┐
                        │  Key Vault   │
                        │  (Secrets)   │
                        └──────────────┘
```

**Data flow**:
1. App authenticates with Entra External ID → gets a bearer token
2. App requests a sync token from the control plane → gets a scoped SAS URI (15 min)
3. App uploads/downloads encrypted blobs directly to/from Blob Storage
4. Control plane never sees decrypted expense data

## Project Map

| Project | Role | New or Existing |
|---------|------|----------------|
| `src/LuSplit.Domain` | Operation model, conflict rules, membership concepts | Existing (extend) |
| `src/LuSplit.Application` | Sync use cases, new ports (auth, sync, crypto) | Existing (extend) |
| `src/LuSplit.Infrastructure` | Blob sync adapter, auth adapter, crypto adapter | Existing (extend) |
| `src/LuSplit.App` | Sharing UI, sync status, auth flow, device management | Existing (extend) |
| `src/LuSplit.Contracts` | Shared operation schemas and API types | **New** |
| `src/LuSplit.Functions` | Azure Functions control plane | **New** |
| `infra/` | Bicep modules for all Azure resources | **New** |

## Build & Test

```bash
# Build everything
dotnet build LuSplit.slnx

# Run all tests
dotnet test LuSplit.slnx

# Validate Bicep
az bicep build --file infra/main.bicep

# Deploy infrastructure (dev)
az deployment group create \
  --resource-group lusplit-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

## Security Summary

- **Group data is end-to-end encrypted**: Only members with the group key can read expense data.
- **The control plane cannot read your data**: It only manages keys in wrapped (encrypted) form.
- **Short-lived tokens**: No long-lived secrets on devices. SAS tokens expire in 15 minutes.
- **Key rotation on revocation**: When a member is removed, keys are rotated so they can't read future data.
- **Invitation links are not credentials**: A link alone cannot access group data — authentication is always required.

## What's NOT Included

- Real-time presence or typing indicators
- Push notifications for new expenses
- Web client
- In-app invitation by email/phone (share sheet only)
- Platform attestation (Play Integrity / App Attest) — deferred to hardening phase
