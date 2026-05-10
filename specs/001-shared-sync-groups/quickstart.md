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

### Create a group
1. Tap **Create group**
2. Choose **Local** or **Shared**
3. If **Local**: name the group, add participants, done
4. If **Shared**: sign in (if not already), name the group, group is created as shared — you land on **Invite people**
5. Invite friends now or tap **Do this later**

### Invite someone (shared owner)
1. Open the shared group — tap **Invite** (visible in group header or group details)
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

---

## UX Acceptance Checklist

Four primary journeys that must work end-to-end before merge.

### Journey 1: Create a local group

- [ ] User taps **Create group**, selects **Local**
- [ ] User names the group, adds participants, completes creation
- [ ] Group appears in group list with a **Local** indicator
- [ ] Group details show local-only actions (edit, manage participants, convert/share)
- [ ] No shared-group actions visible (invite, members, manage sharing)

### Journey 2: Create a shared group and invite

- [ ] User taps **Create group**, selects **Shared**
- [ ] User signs in (if not already authenticated)
- [ ] User names the group, completes creation
- [ ] App navigates directly to **Invite people** step
- [ ] User can invite via share sheet or tap **Do this later**
- [ ] Group appears in group list with a **Shared** indicator and owner badge
- [ ] Group details show owner actions (invite, members, manage sharing)
- [ ] **Convert to Shared Group** is NOT visible
- [ ] Owner can reach **Invite** in one tap from group timeline and group details

### Journey 3: Convert a local group to shared

- [ ] User opens an existing local group
- [ ] User taps **Convert to Shared Group** / **Share this group** from group details
- [ ] User signs in (if not already authenticated)
- [ ] Group is converted; existing data is encrypted and uploaded
- [ ] App navigates to **Invite people** step
- [ ] Group now behaves as shared-owner (same as Journey 2 post-creation)
- [ ] **Convert to Shared Group** is no longer visible
- [ ] If user navigates to the convert screen again, they are redirected to invite/member management

### Journey 4: Open an existing shared group as owner / member

- [ ] **Owner** sees: invite, members, manage sharing, edit settings, add/edit/delete expenses
- [ ] **Member** sees: view members, add/edit/delete expenses
- [ ] **Member** does NOT see: invite, revoke, manage sharing, transfer ownership
- [ ] **Read-only** group: no write actions, view-only members, no owner actions
- [ ] Empty shared group (no expenses yet) shows contextual copy about inviting people or waiting for sync
- [ ] Group list and group switcher distinguish shared/local and owner/member visually

---

## Phase 14 Validation Results

**Build**: `dotnet build LuSplit.slnx` — 0 errors
**Tests**: All tests pass (421 App, 145 Application, 52 Infrastructure, 42 Domain, 13 Functions, 8 Contracts)

### Implementation coverage

| Journey | ViewModel flags | XAML bindings | Navigation | Tests |
|---------|----------------|---------------|------------|-------|
| Create local group | `GroupCollaborationMode.Local` default | Local/Shared selector in step 1 | `GroupCreated` → home | `CreateGroupModeSelectionTests`, `SharedGroupPostCreateNavigationTests` |
| Create shared group + invite | `CollaborationMode.Shared` | Same selector, `IsSharedMode` helper text | `SharedGroupCreated` → invite with `postCreate=true` | Same as above |
| Convert local → shared | `CanConvertToShared` on GroupDetailsVM | "Share this group" button | ConvertGroup route → ShareGroupPage (already-shared guard) | `ConvertGroupAlreadySharedTests` |
| Owner actions | `IsOwner`, `CanInviteMembers`, `CanManageSharing` | Invite/Members buttons, toolbar items | GroupPage + GroupDetails → Invite/Members routes | `GroupDetailsActionVisibilityTests`, `InviteEntryPointReachabilityTests` |
| Member actions | `CanManageMembers` (no invite) | Members button only | GroupPage toolbar → Members route | Same as above |
| Owner membership seed | `CreateSharedGroupUseCase`, `ConvertGroupToSharedUseCase` | N/A | N/A | `OwnerMembershipSeedTests` |
