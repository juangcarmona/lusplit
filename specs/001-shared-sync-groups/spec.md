# Feature Specification: Shared Synchronized Groups

**Feature Branch**: `001-shared-sync-groups`
**Created**: 2026-04-18
**Status**: Draft
**Input**: User description: "Secure shared and synchronized groups using a local-first architecture with a minimal Azure control plane"

**Governance**: This spec, once approved, is the source of truth for feature intent
and scope. Architecture docs in `docs/` provide layer context and terminology.

## Clarifications

### Session 2026-04-18

- Q: Which key distribution mechanism should the system use for shared group encryption keys? → A: Per-user asymmetric key wrapping with member/device keypairs.
- Q: How should shared-group ownership behave if the current owner leaves? → A: Ownership can be transferred to another current member; otherwise the group becomes read-only.
- Q: How strong should device identity be in the baseline architecture? → A: Use an app-generated device identifier plus secure local keypair storage; defer platform attestation to later hardening.
- Q: How visible should conflict resolution be to end users? → A: Auto-resolve by default and surface a lightweight review prompt when the affected expense is next opened.
- Q: What invitation delivery mechanism should the baseline support? → A: Baseline supports only a shareable invitation link or code via the system share sheet.

---

## Problem Statement

LuSplit today is a single-device expense tracker. A user can create a group, add expenses, and see who owes what — but only on one device. If two friends use LuSplit for a shared trip, each must manually enter expenses and trust the other to do the same. There is no shared truth, no synchronization, and no way to verify completeness.

Phase 2 of the product roadmap calls for collaborative, multi-device shared groups. The challenge is to deliver this without abandoning the local-first model, without introducing a heavy backend, and without compromising the calm, minimal experience that defines LuSplit.

The system must allow a user to share a group with others, keep data synchronized across authorized users and devices, protect group content with strong security boundaries, and degrade gracefully when offline — all using a deliberately minimal Azure control plane.

---

## Goals

1. **Enable shared groups**: A user can create a shared group or convert a local group to shared, invite others, and collaborate on expenses.
2. **Local-first synchronization**: All reads and writes happen locally first. Sync is additive and never blocks the core expense flow.
3. **Strong security boundaries**: Group content is encrypted and protected. Storage reachability alone is never sufficient to read group data. Invitations alone are never sufficient to modify group data.
4. **Minimal infrastructure**: The Azure footprint is intentionally small — Entra External ID, Azure Functions, Blob Storage, Key Vault, and basic monitoring. No SQL databases, no Kubernetes, no message buses, no microservice sprawl.
5. **Incremental delivery**: The solution can be delivered in slices, each independently testable and shippable, evolving from local-only to fully shared without breaking existing behavior.
6. **Monorepo-native**: All artifacts — mobile app, control-plane functions, infrastructure-as-code, shared contracts, docs — live in the permanent monorepo with clear boundaries.
7. **Preserve calm UX**: Sharing and sync must feel invisible when working correctly and understandable when not. No enterprise admin patterns, no anxiety-inducing sync indicators.

---

## Non-Goals

- **Real-time co-editing or presence indicators**: No typing dots, no live cursors, no "Alice is viewing this expense" signals.
- **Push notifications for expense updates**: Sync happens when the app is active, not via background push.
- **Web client**: Deferred to a future phase. This spec covers the mobile app only.
- **Social features**: No profiles, avatars, activity feeds, or social graphs.
- **Fintech, budgeting, or banking features**: Permanently excluded per product direction.
- **Heavy backend API**: No traditional REST/GraphQL business API with controllers, DTOs, and service layers. The backend is a thin control plane.
- **SQL database in baseline**: No Azure SQL, Cosmos DB, or PostgreSQL in the initial architecture. Blob Storage is the remote persistence layer.
- **Full offline-to-offline conflict resolution between two users**: If two users both work offline and create conflicting changes, the system must handle it — but optimizing for complex multi-party offline merge scenarios is not a Phase 2 priority.
- **Multi-currency support in sync**: Currency handling remains as-is in the domain; sync does not introduce new currency conversion logic.
- **Granular per-expense permissions**: Authorization is at the group level (read/write), not per-expense.
- **Account requirement for local-only use**: Creating an account must never be required to use LuSplit for local groups.

---

## Actors

### Group Owner
The user who created the shared group or converted a local group to shared. Has full control over the group: can invite members, revoke members, revoke devices, and manage group settings. A group has exactly one owner.

### Group Member (Editor)
A user who has accepted an invitation and has read and write access to the group. Can add, edit, and delete expenses. Cannot invite or revoke other members.

### Invited User (Pending)
A user who has received an invitation link but has not yet accepted. Has no access to group data until they accept and are authorized.

### Authorized Device
A device that has been registered to a user's account and has valid credentials to access shared groups on behalf of that user. A user may have multiple authorized devices.

### Revoked Device
A device that was previously authorized but has had its access removed. Must not be able to read or write group data after revocation, even if cached credentials or encryption keys remain on-device.

### Revoked Member
A user who was previously a group member but has been removed by the owner. Must not be able to read new group data or write to the group after revocation. May retain read access to data they previously downloaded (the system cannot un-deliver already-synced data).

### Anonymous / Unauthenticated User
A person using LuSplit without an account. Can use all local-only features. Cannot participate in shared groups.

---

## User Scenarios & Testing

### User Story 1 — Create a Shared Group (Priority: P1)

A user who has a LuSplit account opens the app, creates a new group, and chooses to make it shared. The group is initialized with encrypted remote storage. The user is the group owner and the only member.

Alternatively, a user with an existing local group decides to share it. The app converts the group to shared, uploads the encrypted group data, and the user becomes the owner.

**Why this priority**: This is the foundational capability. Nothing else (invitations, sync, revocation) works without a shared group existing first.

**Independent Test**: A single authenticated user can create a shared group and verify it appears in their group list with a "shared" indicator. The group data is uploaded to remote storage in encrypted form.

**Acceptance Scenarios**:

1. **Given** an authenticated user with no shared groups, **When** they create a new group and select "shared", **Then** the group is created locally, encrypted group data is uploaded to remote storage, and the group shows a shared indicator.
2. **Given** an authenticated user with an existing local group, **When** they choose to convert it to shared, **Then** the existing expenses and participants are encrypted and uploaded, the group shows a shared indicator, and local behavior is unaffected.
3. **Given** an unauthenticated user, **When** they attempt to create a shared group, **Then** they are prompted to sign in or create an account before proceeding.
4. **Given** an authenticated user on a device not yet registered, **When** they attempt to create a shared group, **Then** the device is registered automatically as part of the flow.

---

### User Story 2 — Invite a User to a Shared Group (Priority: P1)

A group owner opens a shared group, navigates to group settings, and sends an invitation. The invitation is a link (or code) that the invitee can use to join. The invitation is time-limited and single-use. The invitation alone does not grant access to group data — the invitee must also authenticate and be authorized by the system.

**Why this priority**: Without invitations, a shared group has only one user — which makes it just a synced group, not a shared one.

**Independent Test**: An owner generates an invitation link, shares it out-of-band (e.g., messaging app), and the invitee uses it to join. The owner sees the new member in the group member list.

**Acceptance Scenarios**:

1. **Given** an owner of a shared group, **When** they tap "Invite", **Then** an invitation link is generated with a time-limited token and the system share sheet opens.
2. **Given** an invitation link, **When** an authenticated user taps it, **Then** they see the group name and owner, and are prompted to accept or decline.
3. **Given** an invitation link, **When** an unauthenticated user taps it, **Then** they are prompted to sign in or create an account first, and then see the accept/decline prompt.
4. **Given** an invitation link that has expired, **When** a user taps it, **Then** they see a clear message that the invitation is no longer valid.
5. **Given** an invitation link that has already been used, **When** another user taps it, **Then** the link is rejected.
6. **Given** an invitation link, **When** a malicious user without valid authentication tries to use it, **Then** they cannot access any group data, even partial metadata.

---

### User Story 3 — Sync Expenses Across Devices (Priority: P1)

A group member adds an expense on their phone. Later, another member opens the same group on their phone. The new expense appears after a sync cycle. If the second member was offline when the expense was added, it appears the next time they come online and sync completes.

**Why this priority**: Synchronization is the core value proposition of shared groups. Without it, sharing is meaningless.

**Independent Test**: Two devices with the same shared group. Device A adds an expense. Device B syncs and sees the expense with correct amounts, participants, and payer. Balances update correctly.

**Acceptance Scenarios**:

1. **Given** two authorized devices in the same shared group, **When** device A adds an expense and device B syncs, **Then** device B shows the new expense with correct details and updated balances.
2. **Given** a device that has been offline, **When** it comes back online and syncs, **Then** all changes made by other members during the offline period are applied.
3. **Given** a device that has been offline, **When** the user adds expenses while offline, **Then** those expenses are queued and uploaded on the next successful sync.
4. **Given** a sync in progress, **When** the user navigates within the app, **Then** sync continues in the background without blocking the UI.
5. **Given** a sync failure (network error), **When** the failure occurs, **Then** the user sees an ambient status indicator (not a blocking error dialog) and local data remains intact.

---

### User Story 4 — Accept or Reject an Invitation (Priority: P2)

A user receives an invitation link to a shared group. They open LuSplit (or install it if needed), authenticate, and see the group details. They can accept to join or decline to ignore the invitation. On acceptance, the group appears in their group list and begins syncing.

**Why this priority**: Completes the invitation flow started in Story 2. Requires the invitation and auth infrastructure to be in place.

**Independent Test**: A user with the app installed taps an invitation link, sees the group info, accepts, and the group appears in their list with initial data synced.

**Acceptance Scenarios**:

1. **Given** a valid invitation link, **When** an authenticated user accepts, **Then** the group appears in their group list, initial data is synced, and the owner sees them in the member list.
2. **Given** a valid invitation link, **When** an authenticated user declines, **Then** the invitation is consumed (cannot be reused) and no group data is downloaded.
3. **Given** a valid invitation link, **When** a user who already belongs to the group taps it, **Then** they see a message that they are already a member.

---

### User Story 5 — Register a Device (Priority: P2)

A user signs in on a new device. The device is registered to their account and can access their shared groups. The user can see which devices are registered and remove devices they no longer use.

**Why this priority**: Multi-device support is critical for the use case (people switch phones, use tablets), but a single-device flow works for initial testing.

**Independent Test**: A user signs in on a second device, the device is registered, and shared groups become accessible on the new device.

**Acceptance Scenarios**:

1. **Given** a user signing in on a new device, **When** authentication succeeds, **Then** the device is automatically registered and shared groups begin syncing.
2. **Given** a user with multiple registered devices, **When** they view their account settings, **Then** they see a list of their registered devices with recognizable names.
3. **Given** a user viewing their registered devices, **When** they remove a device, **Then** that device loses access to shared groups on next sync attempt.

---

### User Story 6 — Revoke a Member (Priority: P2)

A group owner removes a member from a shared group. The revoked member can no longer read new data or write to the group. Their previously synced data remains on their device but the group is marked as inaccessible. Remaining members' apps reflect the membership change.

**Why this priority**: Revocation is essential for trust and safety, but groups can function with invitation-only flows initially.

**Independent Test**: An owner revokes a member. The revoked member's next sync attempt is rejected. Other members see the updated member list.

**Acceptance Scenarios**:

1. **Given** a group owner, **When** they revoke a member, **Then** the member's access is terminated, encryption keys are rotated, and the member list updates for all remaining members.
2. **Given** a revoked member, **When** they open the app, **Then** the group is marked as "access removed" and they cannot read new expenses or write to the group.
3. **Given** a revoked member, **When** they attempt to sync, **Then** the sync is rejected with a clear authorization error, not a generic failure.

---

### User Story 7 — View Sync Status (Priority: P2)

A user can see whether their shared group is up to date, syncing, pending sync, or unable to sync. The status is ambient — a small icon or subtle indicator, not a banner or modal.

**Why this priority**: Without sync visibility, users cannot distinguish between "no new changes" and "sync is broken."

**Independent Test**: A user with a shared group sees a subtle sync status icon. When offline, it shows a pending state. When back online and synced, it shows "up to date."

**Acceptance Scenarios**:

1. **Given** a shared group that is fully synced, **When** the user views the group, **Then** a subtle "up to date" indicator is visible.
2. **Given** a shared group with pending local changes, **When** the user views the group, **Then** a subtle "pending" indicator is visible.
3. **Given** a shared group that cannot reach the server, **When** the user views the group, **Then** a subtle "will update when online" indicator is visible.
4. **Given** a sync error that requires user attention (e.g., authorization failure), **When** the user views the group, **Then** a clear but non-alarming message explains the situation and suggests action.

---

### User Story 8 — View Group Membership (Priority: P3)

A member of a shared group can see who has access: the owner and all current members. The display uses friendly names, not email addresses or technical identifiers. The owner can see pending invitations.

**Why this priority**: Membership visibility is important for trust but not blocking for core expense flows.

**Independent Test**: A member opens group settings and sees the owner and all members listed with display names.

**Acceptance Scenarios**:

1. **Given** a shared group with multiple members, **When** any member views the group settings, **Then** they see the owner and all members with display names.
2. **Given** a shared group, **When** the owner views the group settings, **Then** they additionally see pending invitations with expiration status.

---

### User Story 9 — Handle Conflicts (Priority: P3)

Two members edit the same expense while one or both are offline. When both sync, the system detects the conflict and resolves it deterministically. The result is visible through a calm activity entry, and the next time the affected expense is opened the app offers a lightweight review prompt so the member can confirm or correct the final value if needed.

**Why this priority**: Conflicts are an edge case in typical usage (most expenses are added, not edited simultaneously), but the system must handle them gracefully.

**Independent Test**: Two devices edit the same expense offline. Both sync. The conflict is resolved automatically and both devices converge to the same state. An activity entry notes the conflict resolution.

**Acceptance Scenarios**:

1. **Given** two devices that edited the same expense offline, **When** both sync, **Then** the system applies a deterministic resolution rule (last-write-wins with vector clock tiebreaking) and both devices converge.
2. **Given** a conflict that was auto-resolved, **When** a member views the group activity, **Then** they see an entry like "Expense updated by [name]" indicating the change, not a frightening "conflict" message.
3. **Given** a conflict that was auto-resolved, **When** a member later opens the affected expense, **Then** the app shows a lightweight review prompt describing that the expense changed while they were away.
4. **Given** two devices that each added different new expenses offline, **When** both sync, **Then** both expenses appear with no conflict — additions are commutative.

---

### User Story 10 — Rotate Access After Revocation (Priority: P3)

After a member is revoked, the group owner's device triggers a key rotation. New encryption keys are distributed to remaining authorized members and devices. Future data is encrypted with the new key. Previously synced data remains accessible to remaining members because they re-encrypt with the new key.

**Why this priority**: Key rotation is critical for security completeness but can be deferred behind basic revocation in initial delivery.

**Independent Test**: A member is revoked, keys are rotated, and remaining members can still read all historical and new data. The revoked member cannot decrypt new data.

**Acceptance Scenarios**:

1. **Given** a member revocation, **When** the owner's device processes the revocation, **Then** new encryption keys are generated and distributed to remaining members.
2. **Given** rotated keys, **When** a remaining member syncs, **Then** they receive the new key and can decrypt all data (old data re-encrypted, new data encrypted with new key).
3. **Given** rotated keys, **When** the revoked member attempts to decrypt new data with their old key, **Then** decryption fails.

---

### Edge Cases

- What happens when a user tries to share a group but has no internet connection? → The share action is queued and completes when connectivity returns. The user sees a "will share when online" message.
- What happens when the group owner's account is deleted? → Ownership must be transferred before account deletion, or the group becomes read-only for existing members until an explicit recovery or administrative action is taken.
- What happens when a device's local database is corrupted? → The device can re-sync from remote storage, rebuilding local state from the encrypted operation log / snapshots.
- What happens when the invitation link is shared publicly (leaked)? → The link alone is insufficient — the recipient must also authenticate and be authorized. The link is single-use and time-limited.
- What happens when two users accept the same invitation link simultaneously? → Only the first acceptance succeeds; the link is consumed atomically.
- What happens when a user uninstalls and reinstalls the app? → They sign in again, re-register the device, and re-sync shared groups. Local-only groups are lost unless backed up.
- What happens when remote storage is temporarily unavailable? → Local operations continue unaffected. Sync retries with exponential backoff. The user sees a "will update when online" indicator.
- What happens when a user is a member of many shared groups? → Sync is per-group and prioritized. Active/visible groups sync first. Background groups sync opportunistically.
- What happens when an expense is deleted by one member while another is editing it offline? → The delete takes precedence on sync. The editing member sees the expense disappear with an activity entry noting the deletion.
- What happens when a device clock is significantly wrong? → Operations use server-issued timestamps for ordering, not device clocks. Device clocks are used only for local display.

---

## Requirements

### Functional Requirements

#### Identity & Authentication

- **FR-001**: The system MUST support optional user accounts via Microsoft Entra External ID. Users without accounts can use all local-only features.
- **FR-002**: The system MUST support device registration, associating a physical device with a user account. A user may have multiple registered devices.
- **FR-003**: The system MUST issue short-lived tokens for all authenticated operations. No long-lived secrets, connection strings, or account keys are stored on devices.
- **FR-004**: The system MUST support token refresh without requiring the user to re-authenticate interactively, within the bounds of the identity provider's refresh policy.
- **FR-004a**: The baseline architecture MUST identify each registered device using an app-generated device identifier and a device-bound asymmetric keypair stored in platform secure storage; platform attestation is deferred to a future hardening phase.

#### Group Lifecycle

- **FR-005**: The system MUST allow an authenticated user to create a new shared group.
- **FR-006**: The system MUST allow an authenticated user to convert an existing local group to a shared group without data loss.
- **FR-007**: The system MUST preserve all existing local group behavior for groups that are not shared. Sharing is purely additive.
- **FR-008**: A shared group MUST have exactly one owner and zero or more members.
- **FR-009**: The system MUST display a clear visual distinction between local and shared groups in the group list and group detail screens.
- **FR-009a**: The system MUST allow the current owner to transfer ownership of a shared group to another current member.
- **FR-009b**: If the owner account leaves or is deleted before transferring ownership, the shared group MUST become read-only for remaining members until an explicit recovery or administrative action is taken.

#### Invitations

- **FR-010**: The group owner MUST be able to generate an invitation link for their shared group.
- **FR-011**: Invitation links MUST be time-limited (configurable, default 72 hours) and single-use.
- **FR-012**: An invitation link MUST NOT by itself grant access to group data. The recipient must also authenticate and be authorized by the control plane.
- **FR-013**: The system MUST allow an invited user to accept or decline the invitation.
- **FR-014**: The system MUST prevent a user from joining a group they are already a member of (idempotent join).
- **FR-015**: The system MUST allow the owner to cancel a pending invitation before it is accepted.
- **FR-015a**: The baseline experience MUST deliver invitations only as a shareable link or code through the platform system share sheet; direct in-app invitation by email or phone is out of scope for the baseline.

#### Authorization & Access Control

- **FR-016**: The system MUST enforce group-level authorization: owners can read, write, invite, and revoke; members can read and write; non-members have no access.
- **FR-017**: Authorization decisions MUST be made by the control plane (Azure Functions), never by the client alone.
- **FR-018**: The system MUST support revoking a member's access to a shared group. Revocation is immediate for write access and effective on next sync for read access.
- **FR-019**: The system MUST support revoking a specific device without affecting the user's other devices or their group memberships.
- **FR-020**: After member revocation, the system MUST rotate encryption keys and distribute new keys to remaining authorized members.

#### Encryption & Data Protection

- **FR-021**: All shared group content stored in remote storage MUST be encrypted with a group-specific key. The storage provider (Azure Blob Storage) MUST NOT be able to read group content.
- **FR-022**: Group encryption keys MUST be managed and distributed through the control plane using per-user or per-device asymmetric key wrapping, not embedded in invitation links or stored in client configuration.
- **FR-023**: The system MUST support key bootstrapping: when a new member joins, they receive the current group key wrapped to their authorized public key through a secure control-plane-mediated exchange.
- **FR-024**: The system MUST support key rotation after member revocation, ensuring revoked members cannot decrypt data written after revocation.
- **FR-025**: Device-local storage of encryption keys MUST use platform-appropriate secure storage (e.g., iOS Keychain, Android Keystore).

#### Synchronization

- **FR-026**: The system MUST use a local-first synchronization model. All reads and writes happen against the local database first.
- **FR-027**: The system MUST sync group data by uploading encrypted change sets (operations or snapshots) to Azure Blob Storage and downloading change sets from other members.
- **FR-028**: Sync operations MUST be idempotent. Replaying the same operation must produce the same result.
- **FR-029**: The system MUST support offline reads and writes. Offline changes are queued and uploaded on the next successful sync.
- **FR-030**: The system MUST handle concurrent edits with a deterministic conflict resolution strategy (last-write-wins with logical clock tiebreaking).
- **FR-031**: The system MUST support rebuilding local state from the remote operation log and/or snapshots in case of local data loss.
- **FR-032**: The system MUST expose sync status per group: up to date, syncing, pending local changes, or sync error.
- **FR-033**: The system MUST record version metadata on every operation for ordering and conflict detection.
- **FR-034**: The system MUST support safe retry on transient sync failures with exponential backoff.

#### Activity & Auditability

- **FR-035**: The system MUST maintain an activity history for each shared group, recording who added, edited, or deleted expenses and when.
- **FR-036**: The system MUST surface membership changes (joins, revocations) in the group activity history.
- **FR-037**: Activity entries MUST use friendly language consistent with the LuSplit voice (e.g., "Alex added an expense" not "WRITE operation by user_id 42").

#### UX & Presentation

- **FR-038**: Sync status MUST be presented as an ambient indicator (icon or subtle badge), not a banner or modal.
- **FR-039**: The system MUST clearly communicate who has access to a shared group (member list visible to all members).
- **FR-040**: Security-sensitive actions (revocation, access loss, key rotation) MUST be communicated explicitly, not silently.
- **FR-041**: The sharing and invitation flow MUST be understandable to non-technical users. No jargon, no technical identifiers exposed.
- **FR-042**: Offline state MUST be presented as normal ("will update when online"), not as an error.

#### Monorepo & Solution Structure

- **FR-043**: The solution MUST live in the existing monorepo structure with clear project boundaries.
- **FR-044**: The solution MUST include infrastructure-as-code (Bicep) as a first-class concern in the monorepo.
- **FR-045**: The solution MUST define shared contracts (e.g., operation schemas, API contracts) in a location accessible to both the mobile app and the control-plane functions.

### Key Entities

- **User**: A person with an account in Entra External ID. Has a display name, a unique identifier, zero or more registered devices, and an asymmetric identity used for wrapped-key access to shared groups. May be an owner or member of multiple shared groups.
- **Device**: A physical device registered to a user. Has an app-generated device identifier, secure key storage, and a device-bound asymmetric keypair used to receive wrapped group keys. Multiple devices may belong to one user.
- **Shared Group**: An extension of the existing Group entity. Has a remote storage location, an encryption key (or key chain), an owner, a member list, and a sync state.
- **Invitation**: A time-limited, single-use token that allows a specific action (joining a group). Not a credential and not sufficient for access alone.
- **Group Key**: A symmetric encryption key used to encrypt and decrypt group content. Rotated on member revocation. Distributed via the control plane, never directly between devices.
- **Operation**: An atomic, versioned, idempotent change to group state (add expense, edit expense, delete expense, add participant, record payment, etc.). The unit of synchronization.
- **Sync Cursor**: A per-device, per-group marker indicating the last successfully synced operation version. Used to request only new operations on each sync cycle.
- **Activity Entry**: A human-readable record of a change to the group, used for auditability and group history display.

---

## Impacted Areas

- **Owning Project(s)**:
  - `LuSplit.Domain` — New concepts: shared group state, operation model, sync versioning, group membership, conflict resolution rules
  - `LuSplit.Application` — New use cases: create shared group, convert to shared, invite, accept invitation, sync, revoke. New ports: identity, sync, encryption, remote storage
  - `LuSplit.Infrastructure` — New adapters: Azure Blob Storage sync adapter, Entra ID authentication adapter, secure key storage adapter, encryption adapter
  - `LuSplit.App` — New UI: shared group creation, invitation flow, member management, sync status indicators, device management, account sign-in flow
  - **New: Control-plane project** (Azure Functions) — Invitation management, authorization enforcement, key distribution, device registration
  - **New: Infrastructure-as-code** (Bicep) — Azure resource definitions, environment configuration
  - **New: Shared contracts** — Operation schemas, control-plane API contracts shared between app and functions

- **Impacted Docs**:
  - `docs/ARCHITECTURE.md` — Must be updated with sync architecture, control-plane description, new project responsibilities
  - `docs/REPO_STRUCTURE.md` — Must be updated with new projects and directories
  - `docs/product/MVP_SCOPE.md` — Phase 2 section should reference this spec
  - `docs/brand/VOICE_AND_TONE.md` — Already defines collaboration vocabulary; verify alignment

- **Validation Scope**: `dotnet build` and `dotnet test` across all existing projects plus new projects once created. Bicep validation via `az bicep build`.

---

## Monorepo & Architecture Boundaries

This section identifies the major solution areas that will exist in the monorepo and their responsibilities. Concrete directory structure, deployment shape, CI/CD pipelines, and environment promotion strategy are **deferred to `/speckit.plan`**.

### Solution Areas

| Area | Purpose | Boundary |
| ---- | ------- | -------- |
| Mobile app (`src/LuSplit.*`) | Local-first expense tracking and UI | Owns all on-device behavior. Calls control plane via short-lived tokens. Never makes direct storage calls with account keys. |
| Control-plane functions | Thin authorization and coordination layer | Owns invitation lifecycle, authorization decisions, key distribution, device registration. Does NOT own business logic, balance calculations, or expense validation. |
| Infrastructure-as-code | Azure resource definitions | Owns all Azure resource provisioning. Bicep modules for Functions, Blob Storage, Key Vault, Entra External ID configuration, monitoring. |
| Shared contracts | Operation schemas and API models | Defines the shape of sync operations, control-plane requests/responses. Referenced by both app and functions. Must not contain behavior. |
| Docs and specs | Architecture and product documentation | Source of truth for structure, terminology, and feature intent. |

### Responsibility Boundaries

- **The client is responsible for**: local reads/writes, encrypting/decrypting group data, building operation change sets, presenting sync status, managing local key storage, initiating sync.
- **The client must NOT be trusted to**: make authorization decisions, validate its own group membership, self-issue tokens, or determine key rotation timing.
- **The control plane is responsible for**: authenticating users, authorizing group access, managing invitations, distributing encryption keys, registering devices, issuing short-lived storage access tokens.
- **The control plane must NOT**: read or decrypt group content, own business logic, validate expense amounts, or compute balances.
- **Blob Storage is responsible for**: durably storing encrypted blobs. It is a dumb storage layer with no knowledge of group semantics.

### Deferred to `/speckit.plan`

The following decisions require planning-phase work and are intentionally NOT resolved in this specification:

- Exact directory and project structure for control-plane functions within the monorepo
- Exact directory and project structure for Bicep modules
- Exact location and packaging of shared contracts
- CI/CD pipeline design (build, test, deploy stages)
- Environment strategy (dev, staging, production)
- Deployment strategy for Azure Functions (consumption vs. flex consumption, slots, etc.)
- Blob Storage container and path conventions
- Exact sync protocol wire format
- Monitoring and alerting configuration details

---

## Security & Trust Constraints

### Trust Boundaries

1. **Device boundary**: The device is partially trusted. It holds encrypted data and short-lived tokens. It is NOT trusted to make authorization decisions or self-grant access.
2. **Control-plane boundary**: The control plane is trusted to enforce authorization and manage keys. It is NOT trusted with unencrypted group content.
3. **Storage boundary**: Blob Storage is untrusted for confidentiality. All stored content is encrypted before upload. Storage is trusted only for durability and availability.
4. **Network boundary**: All communication between client and control plane, and between client and storage, MUST use TLS. The client must validate server certificates.

### Authentication Model

- Users authenticate via Microsoft Entra External ID using standard OAuth 2.0 / OIDC flows.
- The mobile app uses the MSAL (Microsoft Authentication Library) for token acquisition.
- Tokens are short-lived (access tokens) with refresh tokens for session continuity.
- No passwords are stored or managed by LuSplit — Entra External ID handles credential management.

### Device Identity Model

- Each device is registered to a user account upon first sign-in.
- Device identity in the baseline architecture is established through user authentication plus an app-generated unique device identifier stored in secure storage.
- Device registration is recorded in the control plane.
- Each authorized device maintains a device-bound asymmetric keypair in secure storage so the control plane can deliver wrapped group keys without exposing raw keys in transit or at rest outside the device boundary.
- Platform attestation such as Play Integrity or App Attest is out of scope for the baseline and may be added later as a defense-in-depth enhancement.
- A device can be revoked independently of the user account.

### Group Authorization Model

- Group membership is managed by the control plane, not by the client.
- The control plane maintains a membership list per group with roles (owner, member).
- When a client requests a sync token (short-lived SAS token for Blob Storage), the control plane verifies the user's membership and role before issuing the token.
- Tokens are scoped to specific group containers and have short expiration times.

### Encryption Model

- Each shared group has a symmetric encryption key (AES-256 or equivalent).
- The group key is generated by the owner's device when the group is created or converted to shared.
- The group key is uploaded to the control plane only in wrapped form, encrypted to each authorized member or device public key.
- The control plane stores wrapped keys but cannot unwrap them because it does not hold member or device private keys.
- On member revocation, the owner's device generates a new group key, re-encrypts the key for remaining members, and uploads the new wrapped keys.
- All data written to Blob Storage after key rotation uses the new key. Historical data access requires the key chain (all previous keys, available to members who were authorized at the time).

### Invitation Safety

- Invitation links contain a one-time token, NOT group keys or access credentials.
- The invitation token is validated by the control plane, which verifies it is unused, unexpired, and associated with the correct group.
- After validation, the control plane initiates the key distribution handshake — the new member's device receives the group key through the secure channel, not through the invitation link.
- A leaked invitation link cannot be used without valid Entra External ID authentication.
- A leaked invitation link can be cancelled by the owner before it is accepted.

### Revocation Behavior

- **Member revocation**: The owner initiates revocation through the control plane. The control plane removes the member from the membership list immediately. The owner's device triggers key rotation. The revoked member's next sync attempt is rejected with an authorization error.
- **Device revocation**: The user (or owner for a group context) revokes a device through the control plane. The device's registration is removed. The device's next token refresh is rejected. This does NOT revoke the user from groups — only the specific device.
- **Data already synced**: The system cannot un-deliver data already on a revoked member's device. This is an accepted limitation. Key rotation ensures future data is inaccessible.

### What the Client Must Never Do

- Store long-lived infrastructure secrets (connection strings, account keys, SAS tokens with long expiry).
- Make its own authorization decisions (e.g., "I think I'm still a member, so I'll keep syncing").
- Distribute encryption keys directly to other devices.
- Trust its own clock for operation ordering (use server-issued or logical timestamps).
- Bypass the control plane to access storage directly with cached tokens after revocation.

---

## Synchronization & Consistency Requirements

### Local-First Guarantees

- All reads are served from the local database. The app never blocks on network access for read operations.
- All writes are committed to the local database first. Network failure does not prevent writes.
- Sync is an asynchronous, background operation that merges remote changes into local state and uploads local changes to remote storage.

### Sync Model

- The unit of sync is an **Operation**: an atomic, versioned, immutable record of a change (e.g., "add expense X", "edit expense Y field Z", "delete expense W").
- Each operation has a logical version (vector clock or hybrid logical clock) for ordering.
- Operations are uploaded as encrypted blobs to the group's container in Blob Storage.
- On sync, a device downloads all operations newer than its sync cursor, decrypts them, and applies them to local state.
- After applying remote operations, the device uploads its own pending local operations.

### Conflict Resolution

- **Additions**: Adding new expenses or participants is commutative. No conflicts arise from concurrent additions.
- **Edits to the same field of the same entity**: Resolved by last-write-wins using logical clock ordering. The operation with the higher logical timestamp wins.
- **Deletes**: A delete operation takes precedence over concurrent edits to the same entity (delete wins).
- **Conflict visibility**: Auto-resolved conflicts are logged in the activity history with friendly language (e.g., "Expense updated by Alex — your edit was replaced"). No "CONFLICT" warnings are shown, but the next time a user opens the affected expense the app surfaces a lightweight review prompt.

### Consistency Guarantees

- **Eventual consistency**: All authorized devices will eventually converge to the same state if they sync periodically.
- **Causal ordering**: Operations from the same device are applied in order. Operations from different devices are ordered by logical clocks.
- **Idempotency**: Every operation can be applied multiple times without changing the result beyond the first application. Sync retries are safe.
- **Convergence**: Given the same set of operations, any device applying them in any valid causal order will arrive at the same final state.

### Versioning & Snapshots

- Each operation carries a version identifier.
- Periodically (or on demand), a device may write a **snapshot**: a full encrypted dump of the current group state. Snapshots serve as checkpoints for faster re-sync and recovery.
- A new device joining a group can bootstrap from the latest snapshot plus subsequent operations, rather than replaying the entire operation history.

### Failure Recovery

- **Transient network failures**: Retry with exponential backoff. Local state is unaffected.
- **Corrupt local database**: Re-sync from remote (latest snapshot + operations). User is warned that local-only data (drafts, UI state) may be lost.
- **Corrupt remote blob**: Operations are immutable; corruption of a single blob affects only that operation. The control plane or a periodic integrity check can detect missing or corrupt blobs.
- **Partial sync (upload succeeded, cursor update failed)**: Idempotency ensures re-uploading the same operations is safe. The cursor is updated only after confirmed upload.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: A user can create a shared group and invite another user in under 3 minutes, end-to-end.
- **SC-002**: A new expense added on one device is visible on another authorized device within 30 seconds of both devices being online.
- **SC-003**: The app remains fully functional (read and write) with zero network connectivity for at least 30 days of typical use.
- **SC-004**: 90% of users who receive an invitation link successfully join the shared group on first attempt.
- **SC-005**: Sync conflicts (concurrent edits to the same expense) occur in less than 5% of sync cycles across typical usage patterns.
- **SC-006**: A revoked member is unable to read or write new group data within 60 seconds of revocation, assuming their device is online.
- **SC-007**: Group data stored remotely is unreadable without the correct encryption key, even if storage access controls are bypassed.
- **SC-008**: The Azure infrastructure cost for the control plane is under $50/month for up to 10,000 active shared groups.
- **SC-009**: Users can understand whether a group is local or shared, who has access, and whether sync is healthy without reading documentation or tooltips.
- **SC-010**: The app launch time does not increase by more than 500ms with shared group support enabled compared to local-only mode.

---

## Assumptions

- Users have intermittent internet connectivity. The app must work fully offline and sync when connectivity is available.
- The majority of groups will have 2–8 members (friends or family splitting expenses for trips, meals, or shared living).
- Most expenses are added by one person at a time. Concurrent edits to the same expense are rare.
- Users are non-technical. They understand "sharing a link" but not "encryption key distribution" or "SAS tokens."
- Microsoft Entra External ID supports the required OAuth 2.0 / OIDC flows for mobile apps, including token refresh and device registration patterns.
- Azure Blob Storage SAS tokens can be scoped to individual containers with short expiration times, which is sufficient for delegated access.
- The existing domain model (Group, Expense, Participant, Transfer, etc.) does not need structural changes — sync and sharing are additive concepts layered on top.
- The existing local SQLite database remains the source of truth on-device. Sync writes to and reads from the same local database.
- Bicep is sufficient for all infrastructure-as-code needs in the baseline architecture. If a hard limitation is discovered during planning, alternatives (e.g., Terraform) can be evaluated.
- The monorepo structure can accommodate additional projects (Azure Functions, Bicep modules, shared contracts) without build tooling conflicts.
- App store review processes will not reject the app for requiring an optional account for sharing features.

---

## Open Questions & Risks

### Open Questions (to resolve in `/speckit.clarify` or `/speckit.plan`)

1. **Snapshot frequency and lifecycle**: How often should snapshots be created? Should they be automatic (every N operations) or manual? How long should old snapshots be retained?
2. **Operation log compaction**: Should the operation log be compacted after a snapshot to reduce storage costs and sync time? If so, what is the compaction policy?
3. **Group size limits**: Should there be a maximum number of members per group? A maximum number of operations before requiring compaction?
4. **Data residency**: Are there data residency requirements that constrain which Azure regions can host group data?
5. **Cost model validation**: Is the assumption of <$50/month for 10,000 groups achievable with the proposed architecture? This needs a planning-phase cost estimate.

### Risks

| Risk | Impact | Likelihood | Mitigation |
| ---- | ------ | ---------- | ---------- |
| Entra External ID pricing or feature limitations for consumer-grade apps | May force identity provider change | Medium | Validate pricing and feature set during planning phase before committing |
| Encryption key management complexity exceeds team capacity | Delays delivery, increases bug surface | Medium | Start with simplest viable key management; defer advanced rotation to later slices |
| Blob Storage access patterns create unexpected costs | Cost overruns | Low | Model expected usage during planning; set up billing alerts; consider lifecycle policies |
| Sync protocol design proves harder than expected (ordering, conflicts, recovery) | Delivery delays | Medium | Prototype sync early; use established CRDT/event-sourcing patterns as reference |
| App store rejection due to mandatory account for sharing features | Blocks release | Low | Account is optional; local-only mode is fully functional without sign-in |
| User confusion about local vs. shared groups | Poor adoption of sharing | Medium | Invest in UX design and user testing for the sharing flow; follow established voice and tone |
| Key rotation performance for groups with large history | Slow revocation experience | Low | Defer full history re-encryption; only encrypt future data with new key; maintain key chain |
| Monorepo build times increase significantly with new projects | Developer productivity impact | Low | Evaluate incremental build strategies during planning; consider build caching |
