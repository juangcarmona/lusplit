# Tasks: Shared Synchronized Groups

**Input**: Design documents from `/specs/001-shared-sync-groups/`
**Branch**: `001-shared-sync-groups`
**Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: Focused unit tests are included for every new or changed logic slice per constitution §V and copilot-instructions.

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies)
- **[US#]**: User story this task belongs to (Phase 3+ only)
- All paths are workspace-relative from the solution root

---

## Phase 1: Setup

**Purpose**: Create new projects, wire the solution, and scaffold the infrastructure directory. No user story can begin until this phase is complete.

- [x] T001 Add `src/LuSplit.Contracts/LuSplit.Contracts.csproj` (Class Library, `net10.0`) to `LuSplit.slnx`
- [x] T002 [P] Add `src/LuSplit.Functions/LuSplit.Functions.csproj` (Azure Functions isolated worker, `net10.0`) to `LuSplit.slnx`
- [x] T003 [P] Add `tests/LuSplit.Contracts.Tests/LuSplit.Contracts.Tests.csproj` (xUnit, references LuSplit.Contracts) to `LuSplit.slnx`
- [x] T004 [P] Add `tests/LuSplit.Functions.Tests/LuSplit.Functions.Tests.csproj` (xUnit + NSubstitute, references LuSplit.Functions) to `LuSplit.slnx`
- [x] T005 Add project references: `LuSplit.Contracts` referenced by `LuSplit.App.csproj`, `LuSplit.Application.csproj`, `LuSplit.Infrastructure.csproj`, and `LuSplit.Functions.csproj`
- [x] T006 [P] Add NuGet packages `Microsoft.Identity.Client` (MSAL.NET) and `Azure.Storage.Blobs` to `src/LuSplit.App/LuSplit.App.csproj` and `src/LuSplit.Infrastructure/LuSplit.Infrastructure.csproj`
- [x] T007 [P] Add NuGet packages `Microsoft.Azure.Functions.Worker`, `Microsoft.Azure.Functions.Worker.Http`, `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`, `Azure.Data.Tables`, and `Microsoft.Identity.Web` to `src/LuSplit.Functions/LuSplit.Functions.csproj`
- [x] T008 [P] Create `infra/main.bicep` (empty orchestrator skeleton), `infra/modules/` directory with placeholder `.bicep` files, and `infra/parameters/` directory

**Checkpoint**: All projects compile. Solution builds with `dotnet build LuSplit.slnx`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared entities, ports, adapters, SQLite schema, and the Functions host that every user story depends on. No user story work begins until this phase is complete.

**⚠️ CRITICAL**: All user stories block on this phase.

### Tests

- [x] T009 [P] Test: `AesGcmEncryptionAdapterTests` — round-trip encrypt/decrypt, tampered-tag rejection in `tests/LuSplit.Infrastructure.Tests/Crypto/AesGcmEncryptionAdapterTests.cs`
- [x] T010 [P] Test: `RsaKeyWrapAdapterTests` — wrap/unwrap group key, wrong-key rejection in `tests/LuSplit.Infrastructure.Tests/Crypto/RsaKeyWrapAdapterTests.cs`

### Domain models

- [x] T011 Add `OperationType` enum (`AddExpense`, `EditExpense`, `DeleteExpense`, `AddParticipant`, `EditParticipant`, `RecordPayment`, `EditPayment`, `DeletePayment`, `AddTransfer`, `EditTransfer`, `DeleteTransfer`) in `src/LuSplit.Domain/Sync/OperationType.cs`
- [x] T012 [P] Add `SyncStatus` enum (`UpToDate`, `Syncing`, `PendingLocalChanges`, `SyncError`) in `src/LuSplit.Domain/Groups/SyncStatus.cs`
- [x] T013 [P] Add `SharedGroupState` value type (IsShared, RemoteContainerName, OwnerId, CurrentKeyVersion, SyncStatus, IsReadOnly) to extend Group in `src/LuSplit.Domain/Groups/SharedGroupState.cs`
- [x] T014 [P] Add `GroupMembership` entity (GroupId, UserId, Role enum Owner/Member, JoinedAt, IsRevoked, RevokedAt) in `src/LuSplit.Domain/Groups/GroupMembership.cs`
- [x] T015 [P] Add `GroupKey` and `WrappedKeyEntry` value types (KeyVersion, CreatedAt, CreatedByDeviceId, WrappedKeys list) in `src/LuSplit.Domain/Groups/GroupKey.cs`
- [x] T016 [P] Add `Operation` entity (OperationId, GroupId, DeviceId, UserId, HlcTimestamp, OperationType, EntityId, EncryptedPayload, KeyVersion, CreatedAt) in `src/LuSplit.Domain/Sync/Operation.cs`
- [x] T017 [P] Add `SyncCursor` entity (DeviceId, GroupId, LastSyncedHlcTimestamp, LastSyncedAt) in `src/LuSplit.Domain/Sync/SyncCursor.cs`

### Contracts (shared schemas)

- [x] T018 [P] Add `OperationEnvelope` type (KeyVersion int32 header, Nonce, Ciphertext, AuthTag byte regions) in `src/LuSplit.Contracts/Sync/OperationEnvelope.cs`
- [x] T019 [P] Add typed operation payload DTOs (`AddExpensePayload`, `EditExpensePayload`, `AddParticipantPayload`, `RecordPaymentPayload`, etc.) in `src/LuSplit.Contracts/Sync/Payloads/`
- [x] T020 [P] Add control-plane API request/response DTOs (RegisterDeviceRequest, CreateGroupRequest, SyncTokenRequest/Response, CreateInvitationResponse, AcceptInvitationResponse, etc.) in `src/LuSplit.Contracts/ControlPlane/`

### Application ports

- [x] T021 Add `IAuthPort` (GetAccessTokenAsync, SignInAsync, SignOutAsync, GetCurrentUserIdAsync) in `src/LuSplit.Application/Shared/Ports/IAuthPort.cs`
- [x] T022 [P] Add `IEncryptionPort` (Encrypt, Decrypt using group key + nonce) in `src/LuSplit.Application/Shared/Ports/IEncryptionPort.cs`
- [x] T023 [P] Add `ISecureKeyStoragePort` (StoreWrappedKey, RetrieveWrappedKey, StorePrivateKey, RetrievePrivateKey) in `src/LuSplit.Application/Shared/Ports/ISecureKeyStoragePort.cs`

### Infrastructure crypto adapters

- [x] T025 Add `AesGcmEncryptionAdapter` (AES-256-GCM encrypt/decrypt; implements `IEncryptionPort`) in `src/LuSplit.Infrastructure/Crypto/AesGcmEncryptionAdapter.cs`
- [x] T026 [P] Add `RsaKeyWrapAdapter` (RSA-OAEP wrap/unwrap group key with device keypair) in `src/LuSplit.Infrastructure/Crypto/RsaKeyWrapAdapter.cs`
- [x] T027 [P] Add `SecureKeyStorageAdapter` (MAUI `SecureStorage` for device private key and cached group keys; implements `ISecureKeyStoragePort`) in `src/LuSplit.Infrastructure/Crypto/SecureKeyStorageAdapter.cs`
- [x] T028 [P] Add `ControlPlaneHttpClient` (typed `HttpClient` with Entra bearer token delegating handler and base URL configuration) in `src/LuSplit.Infrastructure/ControlPlane/ControlPlaneHttpClient.cs`

### SQLite migrations

- [x] T029 Add SQLite migrations for new tables: `SharedGroupState` columns on Groups, `GroupMembership`, `Operation`, `SyncCursor`, `ActivityEntry` in `src/LuSplit.Infrastructure/Sqlite/Migrations/`

### Functions host

- [x] T030 Add Azure Functions host setup: `Program.cs` (host builder, DI registration, JSON serialization options) and `Middleware/EntraTokenValidationMiddleware.cs` in `src/LuSplit.Functions/`

**Checkpoint**: `dotnet build LuSplit.slnx` passes. Foundational crypto tests in T009–T010 pass.

---

## Phase 3: User Story 1 — Create a Shared Group (Priority: P1) 🎯 MVP Start

**Goal**: An authenticated user creates a new shared group or converts an existing local group. Encrypted remote storage is provisioned. The group shows a shared indicator.

**Independent Test**: Single authenticated user creates a shared group — group appears in list with shared indicator, encrypted blob container is created, group key is generated and wrapped.

### Tests

- [x] T031 [P] [US1] Test: `CreateSharedGroupUseCaseTests` (happy path, unauthenticated rejection, duplicate group handling) in `tests/LuSplit.Application.Tests/Groups/CreateSharedGroupUseCaseTests.cs`
- [x] T032 [P] [US1] Test: `ShareGroupViewModelTests` (create command, loading state, navigation on success) in `tests/LuSplit.App.Tests/ShareGroupViewModelTests.cs`
- [x] T033 [P] [US1] Test: `GroupRegistrationAdapterTests` (maps request correctly, handles 409 conflict) in `tests/LuSplit.Infrastructure.Tests/ControlPlane/GroupRegistrationAdapterTests.cs`

### Application

- [x] T034 [US1] Add `IGroupRegistrationPort` (RegisterGroupAsync, GetGroupInfoAsync) in `src/LuSplit.Application/Groups/Ports/IGroupRegistrationPort.cs`
- [x] T035 [US1] Add `CreateSharedGroupUseCase` (generate group key, wrap to device, call `IGroupRegistrationPort`, persist `SharedGroupState`) in `src/LuSplit.Application/Groups/UseCases/CreateSharedGroupUseCase.cs`
- [x] T036 [P] [US1] Add `ConvertGroupToSharedUseCase` (encrypt existing participants/expenses as initial snapshot, call `IGroupRegistrationPort`, update local group state) in `src/LuSplit.Application/Groups/UseCases/ConvertGroupToSharedUseCase.cs`

### Infrastructure

- [x] T037 [US1] Add `GroupRegistrationAdapter` (POST `/api/groups`, GET `/api/groups/{groupId}`; implements `IGroupRegistrationPort`) in `src/LuSplit.Infrastructure/ControlPlane/GroupRegistrationAdapter.cs`

### Functions

- [x] T038 [US1] Add `GroupFunctions` with `CreateGroup` (`POST /api/groups`) and `GetGroupInfo` (`GET /api/groups/{groupId}`) in `src/LuSplit.Functions/Functions/GroupFunctions.cs`
- [x] T039 [P] [US1] Add `GroupMetadataStore` (persist group metadata rows to Azure Table Storage) in `src/LuSplit.Functions/Services/GroupMetadataStore.cs`

### Infra/Bicep

- [x] T040 [P] [US1] Add `infra/modules/storage.bicep` (Storage account, private containers, lifecycle management policy for old operation blobs)

### App

- [x] T041 [US1] Add `ShareGroupViewModel` (create shared group command, loading/error state, `[ObservableProperty]` bindings) in `src/LuSplit.App/Features/SharedGroups/ShareGroupViewModel.cs`
- [x] T042 [P] [US1] Add `ShareGroupPage.xaml` and thin code-behind in `src/LuSplit.App/Features/SharedGroups/ShareGroupPage.xaml`
- [x] T043 [P] [US1] Add `ConvertGroupViewModel` (convert local group command, progress state) in `src/LuSplit.App/Features/SharedGroups/ConvertGroupViewModel.cs`
- [x] T044 [US1] Update `GroupDetailsViewModel` to expose `IsShared`, shared indicator visibility, and navigate-to-share command in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsViewModel.cs`
- [x] T044a [P] [US1] Update `HomeViewModel` to include shared/local badge in the group list items in `src/LuSplit.App/Features/Home/Home/HomeViewModel.cs`
- [x] T044b [P] [US1] Update `GroupSwitcherViewModel` to include shared/local badge in the group switcher items in `src/LuSplit.App/Features/Groups/GroupSwitcher/GroupSwitcherViewModel.cs`
- [x] T044c [P] [US1] Test: `HomeViewModelSharedBadgeTests` (shared groups show badge, local groups do not) in `tests/LuSplit.App.Tests/HomeViewModelSharedBadgeTests.cs`

**Checkpoint**: A single authenticated user can create a shared group. US1 acceptance scenarios 1–4 pass.

---

## Phase 4: User Story 2 — Invite a User to a Shared Group (Priority: P1)

**Goal**: The group owner generates an invitation link via the system share sheet. The link is time-limited, single-use, and does not expose group data.

**Independent Test**: Owner taps Invite → share sheet opens with a link containing a token. Token is recorded server-side as Pending.

### Tests

- [x] T045 [P] [US2] Test: `CreateInvitationUseCaseTests` (link generated, expiry enforced, non-owner rejected) in `tests/LuSplit.Application.Tests/Invitations/CreateInvitationUseCaseTests.cs`
- [x] T046 [P] [US2] Test: `InviteViewModelTests` (invite command, share sheet call, error state) in `tests/LuSplit.App.Tests/InviteViewModelTests.cs`
- [x] T047 [P] [US2] Test: `InvitationFunctionsCreateTests` (CreateInvitation and GetInvitationInfo endpoints) in `tests/LuSplit.Functions.Tests/InvitationFunctionsCreateTests.cs`

### Domain

- [x] T048 [P] [US2] Add `Invitation` entity and `InvitationStatus` enum (`Pending`, `Accepted`, `Declined`, `Cancelled`, `Expired`) with state-transition validation in `src/LuSplit.Domain/Invitations/Invitation.cs`

### Application

- [x] T049 [US2] Add `IInvitationPort` (CreateInvitationAsync, CancelInvitationAsync, GetInvitationInfoAsync, AcceptInvitationAsync, DeclineInvitationAsync) in `src/LuSplit.Application/Invitations/Ports/IInvitationPort.cs`
- [x] T050 [US2] Add `CreateInvitationUseCase` (verify owner role, call `IInvitationPort.CreateInvitationAsync`, build deep link URI) in `src/LuSplit.Application/Invitations/UseCases/CreateInvitationUseCase.cs`

### Infrastructure

- [x] T051 [US2] Add `InvitationAdapter` (POST `/api/groups/{groupId}/invitations`, DELETE `/api/groups/{groupId}/invitations/{id}`, GET `/api/invitations/{token}/info`; implements `IInvitationPort` create/cancel/info methods) in `src/LuSplit.Infrastructure/ControlPlane/InvitationAdapter.cs`

### Functions

- [x] T052 [US2] Add `CreateInvitation` (`POST /api/groups/{groupId}/invitations`), `CancelInvitation` (`DELETE /api/groups/{groupId}/invitations/{invitationId}`), and `GetInvitationInfo` (`GET /api/invitations/{token}/info`, no auth required) to `src/LuSplit.Functions/Functions/InvitationFunctions.cs`
- [x] T053 [P] [US2] Add `InvitationStore` (persist invitation rows — token hash, status, expiry — to Azure Table Storage) in `src/LuSplit.Functions/Services/InvitationStore.cs`

### Infra/Bicep

- [x] T054 [P] [US2] Add `infra/modules/identity.bicep` (Entra External ID external tenant config, app registration, redirect URIs for MSAL)

### App

- [x] T055 [US2] Add `InviteViewModel` (generate invitation command, open `Share.RequestAsync` with deep link URI) in `src/LuSplit.App/Features/Invitations/InviteViewModel.cs`
- [x] T056 [P] [US2] Add `InvitePage.xaml` and thin code-behind in `src/LuSplit.App/Features/Invitations/InvitePage.xaml`

**Checkpoint**: Owner can generate and share an invitation link. US2 acceptance scenarios 1–6 pass.

---

## Phase 5: User Story 3 — Sync Expenses Across Devices (Priority: P1)

**Goal**: Expenses added on one device appear on other authorized devices after sync. Offline changes queue and upload when connectivity returns.

**Independent Test**: Device A adds expense → Device B syncs → expense visible with correct amounts and balances.

### Tests

- [x] T057 [P] [US3] Test: `SyncGroupUseCaseTests` (pull-apply-push cycle, cursor advances, idempotent re-apply) in `tests/LuSplit.Application.Tests/Sync/SyncGroupUseCaseTests.cs`
- [x] T058 [P] [US3] Test: `BlobSyncAdapterTests` (upload blob, list blobs after cursor, download and decrypt) in `tests/LuSplit.Infrastructure.Tests/Sync/BlobSyncAdapterTests.cs`
- [x] T059 [P] [US3] Test: `OperationApplicatorTests` (each `OperationType` applied correctly, idempotency on duplicate `OperationId`) in `tests/LuSplit.Application.Tests/Sync/OperationApplicatorTests.cs`

### Application

- [x] T060 [US3] Add `ISyncPort` (ListRemoteOperationsAsync, DownloadOperationAsync, UploadOperationAsync, RequestSyncTokenAsync, WriteSnapshotAsync, ReadLatestSnapshotAsync) in `src/LuSplit.Application/Sync/Ports/ISyncPort.cs`
- [x] T061 [US3] Add `SyncGroupUseCase` (pull remote ops after cursor → decrypt → apply → push pending local ops → advance cursor → snapshot if threshold reached) in `src/LuSplit.Application/Sync/UseCases/SyncGroupUseCase.cs`
- [x] T062 [P] [US3] Add `OperationApplicator` (dispatches each `OperationType` to correct local domain write — expense, participant, payment) in `src/LuSplit.Application/Sync/OperationApplicator.cs`

### Infrastructure

- [x] T063 [US3] Add `BlobSyncAdapter` (list/download/upload encrypted operation blobs via `Azure.Storage.Blobs`; implements `ISyncPort` blob methods) in `src/LuSplit.Infrastructure/Sync/BlobSyncAdapter.cs`
- [x] T064 [P] [US3] Add `SasTokenProvider` (calls POST `/api/groups/{groupId}/sync-token`, caches SAS URI until near-expiry) in `src/LuSplit.Infrastructure/Sync/SasTokenProvider.cs`

### Functions

- [x] T065 [US3] Add `SyncFunctions` with `RequestSyncToken` (`POST /api/groups/{groupId}/sync-token` — verify membership, issue 15-min User Delegation SAS, return scoped URI) in `src/LuSplit.Functions/Functions/SyncFunctions.cs`

### Infra/Bicep

- [x] T066 [P] [US3] Add `infra/modules/keyvault.bicep` (Key Vault for Functions app secrets, managed identity RBAC assignment for User Delegation SAS)

### App

- [x] T067 [US3] Add `SyncOrchestrationService` (per-group sync trigger on app foreground, queues pending ops, exposes `SyncStatus` observable) in `src/LuSplit.App/Services/SyncOrchestrationService.cs`
- [x] T068 [P] [US3] Add `ActivityEntry` domain entity and `ActivityEntryType` enum (`ExpenseAdded`, `ExpenseEdited`, `ExpenseDeleted`, `PaymentRecorded`, `MemberJoined`, `MemberRevoked`, `OwnershipTransferred`, `ConflictResolved`, `KeyRotated`) in `src/LuSplit.Domain/Activity/ActivityEntry.cs`
- [x] T069 [P] [US3] Add `ActivityEntryRepository` (local SQLite insert/query for activity log; no remote sync) in `src/LuSplit.Infrastructure/Activity/ActivityEntryRepository.cs`
- [x] T070 [US3] Register `SyncOrchestrationService` in `src/LuSplit.App/MauiProgram.cs` and wire start/stop to `App.xaml.cs` lifecycle methods

**Checkpoint**: Two devices in the same shared group converge after sync. US3 acceptance scenarios 1–5 pass. P1 user stories (US1, US2, US3) are all independently testable — **minimal shippable increment**.

---

## Phase 6: User Story 4 — Accept or Reject an Invitation (Priority: P2)

**Goal**: A user taps an invitation link, sees the group name and owner, and accepts or declines. On acceptance the group appears in their list and initial sync begins.

**Independent Test**: User taps invitation deep link → InvitationLandingPage shows group info → user accepts → group in list, initial sync completes.

### Tests

- [x] T071 [P] [US4] Test: `AcceptInvitationUseCaseTests` (accept returns group + key, duplicate join rejected, expired token rejected) in `tests/LuSplit.Application.Tests/Invitations/AcceptInvitationUseCaseTests.cs`
- [x] T072 [P] [US4] Test: `InvitationLandingViewModelTests` (accept/decline commands, loading state, already-member message) in `tests/LuSplit.App.Tests/InvitationLandingViewModelTests.cs`
- [x] T073 [P] [US4] Test: `InvitationFunctionsAcceptTests` (AcceptInvitation and DeclineInvitation endpoints, consumed token rejection) in `tests/LuSplit.Functions.Tests/InvitationFunctionsAcceptTests.cs`

### Application

- [x] T074 [US4] Add `AcceptInvitationUseCase` (validate token via `IInvitationPort`, unwrap group key with device private key, persist `GroupMembership`, trigger initial sync) in `src/LuSplit.Application/Invitations/UseCases/AcceptInvitationUseCase.cs`
- [x] T075 [P] [US4] Add `DeclineInvitationUseCase` (call `IInvitationPort.DeclineInvitationAsync`) in `src/LuSplit.Application/Invitations/UseCases/DeclineInvitationUseCase.cs`

### Infrastructure

- [x] T076 [US4] Add `AcceptInvitationAdapter` and `DeclineInvitationAdapter` methods to `src/LuSplit.Infrastructure/ControlPlane/InvitationAdapter.cs` (POST `/api/invitations/{token}/accept`, POST `/api/invitations/{token}/decline`)

### Functions

- [x] T077 [US4] Add `AcceptInvitation` (`POST /api/invitations/{token}/accept` — atomically mark token consumed, create membership, return wrapped group key) and `DeclineInvitation` (`POST /api/invitations/{token}/decline`) to `src/LuSplit.Functions/Functions/InvitationFunctions.cs`

### App

- [x] T078 [US4] Add `InvitationLandingViewModel` (preview group name/owner from token, `AcceptCommand`, `DeclineCommand`, already-member guard) in `src/LuSplit.App/Features/Invitations/InvitationLandingViewModel.cs`
- [x] T079 [P] [US4] Add `InvitationLandingPage.xaml` and thin code-behind in `src/LuSplit.App/Features/Invitations/InvitationLandingPage.xaml`
- [x] T080 [US4] Add `AuthenticationViewModel` (MSAL interactive sign-in, silent token refresh, `IAuthPort` orchestration) in `src/LuSplit.App/Features/Auth/AuthenticationViewModel.cs`
- [x] T081 [P] [US4] Add `AuthenticationPage.xaml` in `src/LuSplit.App/Features/Auth/AuthenticationPage.xaml`
- [x] T082 [US4] Register deep link route for invitation token in `src/LuSplit.App/AppShell.xaml.cs` and `src/LuSplit.App/AppRoutes.cs`; add intent filter in `src/LuSplit.App/Platforms/Android/AndroidManifest.xml` and URL scheme in `src/LuSplit.App/Platforms/iOS/Info.plist`

**Checkpoint**: End-to-end invite → accept flow works between two devices. US4 acceptance scenarios 1–3 pass.

---

## Phase 7: User Story 5 — Register a Device (Priority: P2)

**Goal**: A user signing in on a new device sees their shared groups after automatic device registration. They can list and revoke devices from account settings.

**Independent Test**: User signs in on second device → device auto-registered → shared groups sync → device visible in device list.

### Tests

- [x] T083 [P] [US5] Test: `RegisterDeviceUseCaseTests` (generates deviceId, creates keypair, posts to control plane, second registration same device is idempotent) in `tests/LuSplit.Application.Tests/Identity/RegisterDeviceUseCaseTests.cs`
- [x] T084 [P] [US5] Test: `DeviceManagementViewModelTests` (list devices loaded, revoke command calls use case, loading state) in `tests/LuSplit.App.Tests/DeviceManagementViewModelTests.cs`
- [x] T085 [P] [US5] Test: `DeviceFunctionsTests` (RegisterDevice, ListDevices, RevokeDevice endpoints) in `tests/LuSplit.Functions.Tests/DeviceFunctionsTests.cs`

### Domain

- [x] T086 [P] [US5] Add `Device` entity (DeviceId, UserId, DeviceName, PublicKey, RegisteredAt, IsRevoked) and `UserProfile` (UserId, DisplayName) in `src/LuSplit.Domain/Identity/Device.cs` and `src/LuSplit.Domain/Identity/UserProfile.cs`

### Application

- [x] T087 [US5] Add `IDeviceRegistrationPort` (RegisterDeviceAsync, ListDevicesAsync, RevokeDeviceAsync) in `src/LuSplit.Application/Identity/Ports/IDeviceRegistrationPort.cs`
- [x] T088 [US5] Add `RegisterDeviceUseCase` (generate UUID deviceId, generate RSA keypair via `RsaKeyWrapAdapter`, store private key via `ISecureKeyStoragePort`, call `IDeviceRegistrationPort.RegisterDeviceAsync`) in `src/LuSplit.Application/Identity/UseCases/RegisterDeviceUseCase.cs`

### Infrastructure

- [x] T089 [US5] Add `MsalAuthAdapter` (MSAL.NET `PublicClientApplication`, `AcquireTokenInteractive`, `AcquireTokenSilent`, token cache; implements `IAuthPort`) in `src/LuSplit.Infrastructure/Identity/MsalAuthAdapter.cs`
- [x] T090 [US5] Add `DeviceRegistrationAdapter` (POST `/api/devices/register`, GET `/api/devices`, POST `/api/devices/{deviceId}/revoke`; implements `IDeviceRegistrationPort`) in `src/LuSplit.Infrastructure/ControlPlane/DeviceRegistrationAdapter.cs`

### Functions

- [x] T091 [US5] Add `DeviceFunctions` with `RegisterDevice` (`POST /api/devices/register`), `ListDevices` (`GET /api/devices`), and `RevokeDevice` (`POST /api/devices/{deviceId}/revoke`) in `src/LuSplit.Functions/Functions/DeviceFunctions.cs`
- [x] T092 [P] [US5] Add `DeviceStore` (persist device registration rows — deviceId, userId, publicKey, isRevoked — to Azure Table Storage) in `src/LuSplit.Functions/Services/DeviceStore.cs`

### App

- [x] T093 [US5] Add `DeviceManagementViewModel` (load devices list, `RevokeDeviceCommand` with confirmation guard) in `src/LuSplit.App/Features/Devices/DeviceManagementViewModel.cs`
- [x] T094 [P] [US5] Add `DeviceManagementPage.xaml` and thin code-behind in `src/LuSplit.App/Features/Devices/DeviceManagementPage.xaml`

**Checkpoint**: Multi-device sign-in and group access works. US5 acceptance scenarios 1–3 pass.

---

## Phase 8: User Story 6 — Revoke a Member (Priority: P2)

**Goal**: A group owner removes a member. The member's access terminates immediately. Remaining members see the updated member list.

**Independent Test**: Owner revokes member → member's next sync is rejected with 403 → remaining members see updated list → `KeyRotated` activity entry generated.

### Tests

- [x] T095 [P] [US6] Test: `RevokeMemberUseCaseTests` (revoke marks membership, triggers key rotation flag, cannot revoke owner) in `tests/LuSplit.Application.Tests/Revocation/RevokeMemberUseCaseTests.cs`
- [x] T096 [P] [US6] Test: `MemberFunctionsTests` (RevokeMember, TransferOwnership endpoints, authorization guards) in `tests/LuSplit.Functions.Tests/MemberFunctionsTests.cs`

### Application

- [x] T097 [US6] Add `IRevocationPort` (RevokeMemberAsync, TransferOwnershipAsync) in `src/LuSplit.Application/Revocation/Ports/IRevocationPort.cs`
- [x] T098 [US6] Add `RevokeMemberUseCase` (verify caller is owner, call `IRevocationPort.RevokeMemberAsync`, schedule `RotateGroupKeyUseCase` execution, generate `MemberRevoked` activity entry) in `src/LuSplit.Application/Revocation/UseCases/RevokeMemberUseCase.cs`

### Infrastructure

- [x] T099 [US6] Add `MemberRevocationAdapter` (POST `/api/groups/{groupId}/members/{userId}/revoke`, POST `/api/groups/{groupId}/transfer-ownership`; implements `IRevocationPort`) in `src/LuSplit.Infrastructure/ControlPlane/MemberRevocationAdapter.cs`

### Functions

- [x] T100 [US6] Add `MemberFunctions` with `RevokeMember` (`POST /api/groups/{groupId}/members/{userId}/revoke`) and `TransferOwnership` (`POST /api/groups/{groupId}/transfer-ownership`) in `src/LuSplit.Functions/Functions/MemberFunctions.cs`

### App

- [x] T101 [US6] Add `MemberListViewModel` (list members with display names, `RevokeMemberCommand` for owner with confirmation, `TransferOwnershipCommand`) in `src/LuSplit.App/Features/Members/MemberListViewModel.cs`
- [x] T102 [P] [US6] Add `MemberListPage.xaml` and thin code-behind in `src/LuSplit.App/Features/Members/MemberListPage.xaml`
- [x] T103 [P] [US6] Add `KeyStore` (persist wrapped group key rows — groupId, keyVersion, deviceId, wrappedKeyBlob — to Azure Table Storage) in `src/LuSplit.Functions/Services/KeyStore.cs`
- [x] T104 [US6] Update `GroupDetailsViewModel` to show member management entry point and owner-only revoke action in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsViewModel.cs`
- [x] T104a [US6] Add owner-loss detection: when `SyncGroupUseCase` receives a 403/404 for the owner's membership, set `SharedGroupState.IsReadOnly = true` and persist in `src/LuSplit.Application/Sync/UseCases/SyncGroupUseCase.cs`
- [x] T104b [P] [US6] Add read-only guard in `GroupViewModel` and `ExpenseDetailsViewModel` to disable write commands when `SharedGroupState.IsReadOnly` is true in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs` and `src/LuSplit.App/Features/Expenses/ExpenseDetailsViewModel.cs`
- [x] T104c [P] [US6] Add user-facing banner in `GroupPage.xaml` when group is read-only: "The group owner left — this group is now view-only" in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupPage.xaml`
- [x] T104d [P] [US6] Test: `OwnerLossReadOnlyTests` (sync receives 403 → group becomes read-only → write commands disabled) in `tests/LuSplit.Application.Tests/Revocation/OwnerLossReadOnlyTests.cs`
- [x] T104e [P] [US6] Add explicit access-removed messaging: when a revoked member's sync returns 403, show "You no longer have access to this group" in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs`

**Checkpoint**: Owner can revoke a member and key rotation is triggered. Owner loss makes the group read-only. US6 acceptance scenarios 1–3 pass.

---

## Phase 9: User Story 7 — View Sync Status (Priority: P2)

**Goal**: Shared groups display an ambient sync indicator (up to date, pending, syncing, error). No banners or modals for normal state transitions.

**Independent Test**: User views shared group → small indicator shows correct status → goes offline → indicator shows "will update when online" → back online and synced → shows "up to date."

### Tests

- [x] T105 [P] [US7] Test: `GetSyncStatusQueryTests` (returns correct status for each SyncStatus value, local-only groups return null) in `tests/LuSplit.Application.Tests/Sync/GetSyncStatusQueryTests.cs`
- [x] T106 [P] [US7] Test: `SyncStatusViewModelTests` (status changes propagate to observable, correct icon/text per state) in `tests/LuSplit.App.Tests/SyncStatusViewModelTests.cs`

### Application

- [x] T107 [US7] Add `GetSyncStatusQuery` (reads `SyncStatus` from local `SharedGroupState` for a given groupId) in `src/LuSplit.Application/Sync/Queries/GetSyncStatusQuery.cs`

### App

- [x] T108 [US7] Add `SyncStatusViewModel` (observable `SyncStatus`, `StatusText`, `StatusIconGlyph` derived properties bound to indicator) in `src/LuSplit.App/Features/Sync/SyncStatusViewModel.cs`
- [x] T109 [US7] Update `GroupViewModel` to expose `SyncStatus` observable property kept in sync with `SyncOrchestrationService` notifications in `src/LuSplit.App/Features/Groups/GroupViewModel.cs`
- [x] T110 [P] [US7] Add `SyncStatusIndicator.xaml` (small MAUI `ContentView` with icon + optional label; binds to `SyncStatus`) in `src/LuSplit.App/Features/Sync/SyncStatusIndicator.xaml`
- [x] T111 [US7] Add `SyncStatusIndicator` to the group header area in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupPage.xaml`
- [x] T111a [P] [US7] Add `SyncStatusIndicator` to the group detail header in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml`

### Infra/Bicep

- [x] T112 [P] [US7] Add `infra/modules/monitoring.bicep` (Application Insights workspace, alert rule for sync error rate)

**Checkpoint**: Sync status visible in group views. US7 acceptance scenarios 1–4 pass.

---

## Phase 10: User Story 8 — View Group Membership (Priority: P3)

**Goal**: Any member sees the full member list with display names. The owner additionally sees pending invitations with expiry status.

**Independent Test**: Member opens group settings → member list shows owner + all members with display names. Owner additionally sees pending invitation count.

### Tests

- [x] T113 [P] [US8] Test: `GetGroupMembersQueryTests` (returns all non-revoked members, owner flagged) in `tests/LuSplit.Application.Tests/Groups/GetGroupMembersQueryTests.cs`
- [x] T114 [P] [US8] Test: `GetPendingInvitationsQueryTests` (owner gets pending invitations, non-owner gets empty list) in `tests/LuSplit.Application.Tests/Invitations/GetPendingInvitationsQueryTests.cs`

### Application

- [x] T115 [US8] Add `GetGroupMembersQuery` (read `GroupMembership` rows from local SQLite for a groupId, join display names from cached UserProfile) in `src/LuSplit.Application/Groups/Queries/GetGroupMembersQuery.cs`
- [x] T116 [P] [US8] Add `GetPendingInvitationsQuery` (owner-only — read Pending invitations with expiry from local cache or control plane) in `src/LuSplit.Application/Invitations/Queries/GetPendingInvitationsQuery.cs`

### Infrastructure

- [x] T117 [P] [US8] Add `GroupMembershipRepository` (local SQLite read/write for `GroupMembership` rows) in `src/LuSplit.Infrastructure/Groups/GroupMembershipRepository.cs`

### App

- [x] T118 [US8] Update `MemberListViewModel` to use `GetGroupMembersQuery`, show owner badge, show pending invitations section (owner only) in `src/LuSplit.App/Features/Members/MemberListViewModel.cs`
- [x] T119 [P] [US8] Update `MemberListPage.xaml` to include owner role badge and a `CollectionView` for pending invitations (owner view) in `src/LuSplit.App/Features/Members/MemberListPage.xaml`
- [x] T120 [US8] Update `GroupDetailsViewModel` to include navigation command to `MemberListPage` in `src/LuSplit.App/Features/Groups/GroupDetailsViewModel.cs`

**Checkpoint**: All members can view who has access. US8 acceptance scenarios 1–2 pass.

---

## Phase 11: User Story 9 — Handle Conflicts (Priority: P3)

**Goal**: Concurrent offline edits are auto-resolved deterministically (LWW by HLC, delete wins over edit). Both devices converge. A calm activity entry records the resolution.

**Independent Test**: Two devices edit same expense offline → both sync → both converge to same state → activity entry shows "updated by [name]" → affected expense shows lightweight review prompt on next open.

### Tests

- [x] T121 [P] [US9] Test: `ConflictResolutionPolicyTests` (LWW by HLC wins, delete beats edit, concurrent additions are commutative, same-field merge) in `tests/LuSplit.Domain.Tests/Sync/ConflictResolutionPolicyTests.cs`
- [x] T122 [P] [US9] Test: `SyncConflictIntegrationTests` (two operation sets with conflicting HLC, apply in any order, assert same final state and activity entries) in `tests/LuSplit.Application.Tests/Sync/SyncConflictIntegrationTests.cs`
- [x] T123 [P] [US9] Test: `ConflictReviewPromptViewModelTests` (prompt visible when conflict flag set, dismissed after user acknowledges) in `tests/LuSplit.App.Tests/ConflictReviewPromptViewModelTests.cs`

### Domain

- [x] T124 [US9] Add `ConflictResolutionPolicy` (static rules: LWW by HLC for field edits, delete-wins for delete vs. edit, commutative for additions) in `src/LuSplit.Domain/Sync/ConflictResolutionPolicy.cs`

### Application

- [x] T125 [US9] Update `SyncGroupUseCase` to invoke `ConflictResolutionPolicy` during operation application and write `ConflictResolved` activity entries to `ActivityEntryRepository` in `src/LuSplit.Application/Sync/UseCases/SyncGroupUseCase.cs`
- [x] T126 [P] [US9] Add `ConflictResolutionResult` value type (WinningOperationId, LosingOperationId, AffectedEntityId, Resolution description) in `src/LuSplit.Application/Sync/ConflictResolutionResult.cs`

### App

- [x] T127 [US9] Add `ConflictReviewPromptViewModel` (show "expense changed while away" message, dismiss command, clears conflict flag) in `src/LuSplit.App/Features/Expenses/ConflictReviewPromptViewModel.cs`
- [x] T128 [US9] Update `ExpenseDetailsViewModel` to check conflict flag on load and trigger `ConflictReviewPromptViewModel` when set in `src/LuSplit.App/Features/Expenses/ExpenseDetailsViewModel.cs`
- [x] T129 [P] [US9] Add `ActivityFeedViewModel` (paged load of `ActivityEntry` records, LuSplit-voice descriptions) in `src/LuSplit.App/Features/Activity/ActivityFeedViewModel.cs`
- [x] T130 [P] [US9] Add `ActivityFeedPage.xaml` and thin code-behind in `src/LuSplit.App/Features/Activity/ActivityFeedPage.xaml`

**Checkpoint**: Conflicting offline edits converge. US9 acceptance scenarios 1–4 pass.

---

## Phase 12: User Story 10 — Rotate Access After Revocation (Priority: P3)

**Goal**: After revocation the owner's device generates a new group key, distributes it to remaining members, and future data is encrypted with the new key. The revoked member cannot decrypt post-rotation data.

**Independent Test**: Member revoked → key rotation triggered → remaining members receive new wrapped key → new expense encrypted with new key → revoked member decryption fails.

### Tests

- [x] T131 [P] [US10] Test: `RotateGroupKeyUseCaseTests` (new key version > previous, wrapped for all non-revoked devices, revoked device absent) in `tests/LuSplit.Application.Tests/KeyManagement/RotateGroupKeyUseCaseTests.cs`
- [x] T132 [P] [US10] Test: `KeyRotationAdapterTests` (POST /keys maps correctly, GET /keys returns key chain per deviceId) in `tests/LuSplit.Infrastructure.Tests/ControlPlane/KeyRotationAdapterTests.cs`
- [x] T133 [P] [US10] Test: `KeyFunctionsTests` (UploadRotatedKey validates version monotonicity, GetWrappedKeysForDevice returns correct versions) in `tests/LuSplit.Functions.Tests/KeyFunctionsTests.cs`

### Domain

- [x] T134 [P] [US10] Add `KeyRotationPolicy` (rules: rotation required on member revocation, key version strictly monotonic, all non-revoked devices must receive wrapped key) in `src/LuSplit.Domain/Groups/KeyRotationPolicy.cs`

### Application

- [x] T135 [US10] Add `IKeyRotationPort` (UploadRotatedKeyAsync, GetWrappedKeysForDeviceAsync) in `src/LuSplit.Application/KeyManagement/Ports/IKeyRotationPort.cs`
- [x] T136 [US10] Add `RotateGroupKeyUseCase` (generate new AES-256 key, wrap to each non-revoked device's public key, call `IKeyRotationPort.UploadRotatedKeyAsync`, update local `CurrentKeyVersion`) in `src/LuSplit.Application/KeyManagement/UseCases/RotateGroupKeyUseCase.cs`
- [x] T137 [US10] Update `RevokeMemberUseCase` to call `RotateGroupKeyUseCase` after successful member revocation in `src/LuSplit.Application/Revocation/UseCases/RevokeMemberUseCase.cs`

### Infrastructure

- [x] T138 [US10] Add `KeyRotationAdapter` (POST `/api/groups/{groupId}/keys`, GET `/api/groups/{groupId}/keys`; implements `IKeyRotationPort`) in `src/LuSplit.Infrastructure/ControlPlane/KeyRotationAdapter.cs`

### Functions

- [x] T139 [US10] Add `KeyFunctions` with `UploadRotatedKey` (`POST /api/groups/{groupId}/keys` — validate version monotonicity, store wrapped keys) and `GetWrappedKeysForDevice` (`GET /api/groups/{groupId}/keys?deviceId=` — return full key chain for device) in `src/LuSplit.Functions/Functions/KeyFunctions.cs`
- [x] T140 [US10] Update `BlobSyncAdapter` to write `KeyVersion` header when encrypting and select correct unwrapped key by version when decrypting in `src/LuSplit.Infrastructure/Sync/BlobSyncAdapter.cs`

**Checkpoint**: Key rotation on revocation works end-to-end. US10 acceptance scenarios 1–3 pass.

---

## Phase 13: Feature Closure — Wiring, Reachability & Runtime Gaps

**Purpose**: Connect implemented infrastructure to the UI. No new architectural layers or abstractions. Every task wires existing code or adds minimal XAML/code-behind to make features user-reachable.

### CRITICAL — Sync Operation Enqueuing

- [x] T153 [US3] Update `AddExpenseUseCase` to enqueue an `Operation` after `SaveAsync` when the group is shared using `IOperationRepository`, `ISharedGroupStateRepository`, `IIdGenerator`, `IClock`, `IEncryptionPort`, and `IGroupKeyProvider` in `src/LuSplit.Application/Expenses/Commands/AddExpenseUseCase.cs`
- [x] T154 Add unit tests for operation enqueuing in `AddExpenseUseCase` (shared group creates operation, non-shared group skips) in `tests/LuSplit.Application.Tests/AddExpenseUseCaseTests.cs`

- [x] T155 [P] [US3] Update `EditExpenseUseCase` to enqueue an `Operation` after `SaveAsync` when the group is shared in `src/LuSplit.Application/Expenses/Commands/EditExpenseUseCase.cs`
- [x] T156 [P] Add unit tests for operation enqueuing in `EditExpenseUseCase` in `tests/LuSplit.Application.Tests/EditExpenseUseCaseTests.cs`

- [x] T157 [P] [US3] Update `DeleteExpenseUseCase` to enqueue an `Operation` after deletion when the group is shared in `src/LuSplit.Application/Expenses/Commands/DeleteExpenseUseCase.cs`
- [x] T158 [P] Add unit tests for operation enqueuing in `DeleteExpenseUseCase` in `tests/LuSplit.Application.Tests/DeleteExpenseUseCaseTests.cs`

- [x] T159 [US3] Update `AddManualTransferUseCase` to enqueue an `Operation` after `SaveAsync` when the group is shared in `src/LuSplit.Application/Payments/Commands/AddManualTransferUseCase.cs`
- [x] T160 Add unit tests for operation enqueuing in `AddManualTransferUseCase` in `tests/LuSplit.Application.Tests/AddManualTransferUseCaseTests.cs`

### CRITICAL — Auth & Bearer Token

- [x] T161 Register `IPublicClientApplication` and `MsalAuthAdapter` as `IAuthPort` in DI in `src/LuSplit.App/MauiProgram.cs`
- [x] T162 Update `ControlPlaneHttpClient` registration to use `IAuthPort.GetAccessTokenAsync` as the bearer token provider in `src/LuSplit.App/MauiProgram.cs`

### CRITICAL — Navigation Entry Points

- [x] T163 Add shared indicator badge and "Members" / "Share" buttons bound to `IsShared` in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml`
- [x] T164 Subscribe to `NavigateToMembersRequested` and `NavigateToShareRequested` and navigate via `Shell.Current.GoToAsync` in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml.cs`

- [x] T165 Add "Convert to Shared" button bound to `NavigateToConvertRequested` in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml`
- [x] T166 Add `NavigateToConvertRequested` event and `NavigateToConvertCommand` in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsViewModel.cs`
- [x] T167 Subscribe to `NavigateToConvertRequested` and navigate to ConvertGroup route in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml.cs`

### HIGH — Routes & DI Registration

- [x] T168 Add `Invite` route constant in `src/LuSplit.App/AppRoutes.cs`
- [x] T169 Register `InvitePage` shell route in `src/LuSplit.App/AppShell.xaml.cs`
- [x] T170 Register `InviteViewModel` and `InvitePage` in DI in `src/LuSplit.App/MauiProgram.cs`

- [x] T171 Add `ConvertGroup` route constant in `src/LuSplit.App/AppRoutes.cs`
- [x] T172 Register `ConvertGroupViewModel` and `ConvertGroupPage` in DI in `src/LuSplit.App/MauiProgram.cs`
- [x] T172a Register `ConvertGroup` shell route in `src/LuSplit.App/AppShell.xaml.cs`

### HIGH — Activity Infrastructure Wiring

- [x] T173 Make `ActivityEntryRepository` implement `IActivityEntryPort` in `src/LuSplit.Infrastructure/Activity/ActivityEntryRepository.cs`
- [x] T174 Register `IActivityEntryPort` in DI using `ActivityEntryRepository` in `src/LuSplit.App/MauiProgram.cs`
- [x] T175 Create `ActivityFeedDataService` delegating to `ActivityEntryRepository.ListByGroupAsync` in `src/LuSplit.App/Features/Activity/ActivityFeedDataService.cs`
- [x] T176 Register `IActivityFeedDataService` in DI in `src/LuSplit.App/MauiProgram.cs`
- [x] T177 Update `SyncGroupUseCase` to require non-null `IActivityEntryPort` in constructor in `src/LuSplit.Application/Sync/UseCases/SyncGroupUseCase.cs`

### HIGH — RevokeMemberUseCase DI

- [x] T178 Register `RevokeMemberUseCase` in DI and verify all constructor dependencies resolve in `src/LuSplit.App/MauiProgram.cs`
- [x] T178a Add DI resolution test for `RevokeMemberUseCase` and `MemberListViewModel` in `tests/LuSplit.App.Tests/RevokeMemberDiResolutionTests.cs`

### HIGH — SyncOrchestration → UI Wiring

- [x] T179 Pass `SyncOrchestrationService` from DI into `GroupViewModel` construction in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupPage.xaml.cs`
- [x] T179a Update `GroupViewModel` to subscribe to `SyncOrchestrationService.SyncStateChanged` and update `SyncStatus` in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs`
- [x] T179b Add `GroupViewModelSyncStatusUpdateTests` validating `SyncStatus` updates on event in `tests/LuSplit.App.Tests/GroupViewModelSyncStatusUpdateTests.cs`

### MEDIUM — Sync Retry Suppression

- [x] T180 Add early return in `SyncGroupUseCase.ExecuteAsync` when `SharedGroupState.IsReadOnly` is true in `src/LuSplit.Application/Sync/UseCases/SyncGroupUseCase.cs`
- [x] T181 Add unit test validating no sync execution when group is read-only in `tests/LuSplit.Application.Tests/SyncGroupUseCaseTests.cs`

### MEDIUM — Conflict Review Prompt UI

- [x] T182 Add conflict review prompt banner bound to `ConflictPrompt.HasConflict` in `src/LuSplit.App/Features/Expenses/ExpenseDetails/ExpenseDetailsPage.xaml`

### MEDIUM — XAML Resource Fixes

- [x] T183 [P] Replace invalid resource keys in `src/LuSplit.App/Features/Sync/SyncStatusIndicator.xaml`
- [x] T184 [P] Replace invalid resource keys in `src/LuSplit.App/Features/Members/MemberListPage.xaml`
- [x] T185 [P] Replace invalid resource keys in `src/LuSplit.App/Features/Devices/DeviceManagementPage.xaml`

### MEDIUM — Settings Reachability

- [x] T188 Add `NavigateToAuthCommand` in `src/LuSplit.App/Features/Settings/Settings/SettingsViewModel.cs`
- [x] T189 Add `NavigateToDevicesCommand` in `src/LuSplit.App/Features/Settings/Settings/SettingsViewModel.cs`
- [x] T190 Add Auth and Devices navigation rows in `src/LuSplit.App/Features/Settings/Settings/SettingsPage.xaml`
- [x] T191 Wire navigation via `Shell.Current.GoToAsync` in `src/LuSplit.App/Features/Settings/Settings/SettingsPage.xaml.cs`

### VALIDATION

- [x] T186 Run `dotnet build LuSplit.slnx` — fix all compilation errors
- [x] T187 Run `dotnet test LuSplit.slnx` — ensure all tests pass

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Complete the Bicep orchestrator, wire remaining cross-cutting concerns, update docs, and perform required build/test validation.

- [x] T141 Complete `infra/main.bicep` orchestrator with all module references, parameter passing, managed identity role assignments, and output values
- [x] T142 [P] Add `infra/modules/functions.bicep` (Azure Functions Consumption plan, app settings, managed identity assignment to Storage and Key Vault)
- [x] T143 [P] Add `infra/parameters/dev.bicepparam` and `infra/parameters/prod.bicepparam` with environment-specific values
- [x] T144 [P] Add exponential backoff retry policy (Polly or manual) to `BlobSyncAdapter` transient failures and `ControlPlaneHttpClient` in `src/LuSplit.Infrastructure/Sync/BlobSyncAdapter.cs` and `src/LuSplit.Infrastructure/ControlPlane/ControlPlaneHttpClient.cs`
- [x] T145 [US3] Complete `OperationApplicator` branches so all `ActivityEntry` types are generated for every operation type in `src/LuSplit.Application/Sync/OperationApplicator.cs`
- [x] T146 [P] Register all new page routes (ShareGroup, InvitationLanding, Authentication, MemberList, DeviceManagement, ActivityFeed) in `src/LuSplit.App/AppRoutes.cs` and `src/LuSplit.App/AppShell.xaml`
- [x] T147 [P] Register all new Application ports and Infrastructure adapters in `src/LuSplit.App/MauiProgram.cs` DI container
- [x] T148 [P] Update `docs/ARCHITECTURE.md` with sync architecture diagram, control plane responsibilities, new project descriptions
- [x] T149 [P] Update `docs/REPO_STRUCTURE.md` with `LuSplit.Contracts`, `LuSplit.Functions`, `infra/` entries and their purposes
- [x] T150 Run `dotnet build LuSplit.slnx` — fix all compilation errors before proceeding
- [x] T151 Run `dotnet test LuSplit.slnx` — fix all failing tests before marking work done
- [x] T152 [P] Run `az bicep build --file infra/main.bicep` — fix all Bicep validation errors
- [x] T192 Update `docs/ARCHITECTURE.md` with Phase 13 wiring changes if any new data flow paths were added
- [x] T193 Run `dotnet build LuSplit.slnx` — confirm clean
- [x] T194 Run `dotnet test LuSplit.slnx` — confirm all green

**Final Checkpoint**: `dotnet build` clean, `dotnet test` all green, `az bicep build` validates. All user stories independently testable per their acceptance criteria. All shared-group pages reachable from existing UI. Sync pipeline end-to-end functional for shared groups.

---

## Phase 14: UX Coherence — Shared Group Journey

**Purpose**: Close the gap between implemented shared-group capabilities and the actual user journey. No new sync architecture. This phase is state-driven UI adaptation, flow repair, reachability, copy cleanup, and acceptance validation.

### Acceptance criteria

* A shared group never shows **Convert to Shared Group**
* A newly created shared group lands on an **Invite people** step
* A shared owner can reach **Invite** in one tap from the group
* A shared member never sees owner-only actions
* Invalid screens short-circuit to the correct destination instead of showing broken actions

### Tests

* [ ] T195 [P] Test: `CreateGroupModeSelectionTests` — local/shared choice changes create flow behavior and post-create navigation in `tests/LuSplit.App.Tests/Groups/CreateGroupModeSelectionTests.cs`
* [ ] T196 [P] Test: `GroupDetailsActionVisibilityTests` — local/shared/owner/member/read-only combinations expose correct actions in `tests/LuSplit.App.Tests/Groups/GroupDetailsActionVisibilityTests.cs`
* [ ] T197 [P] Test: `InviteEntryPointReachabilityTests` — shared owner can reach invite from group details and group timeline; local/member cannot in `tests/LuSplit.App.Tests/Invitations/InviteEntryPointReachabilityTests.cs`
* [ ] T198 [P] Test: `ConvertGroupAlreadySharedTests` — already shared groups do not attempt conversion and redirect to sharing management in `tests/LuSplit.App.Tests/Groups/ConvertGroupAlreadySharedTests.cs`
* [ ] T199 [P] Test: `SharedGroupPostCreateNavigationTests` — creating a shared group navigates to invite flow instead of stopping at group details in `tests/LuSplit.App.Tests/Groups/SharedGroupPostCreateNavigationTests.cs`
* [ ] T200 [P] Test: `OwnerMembershipSeedTests` — shared-group creation and conversion seed local owner membership immediately so role-aware UI works before next sync in `tests/LuSplit.Application.Tests/Groups/OwnerMembershipSeedTests.cs`

### Application / state shaping

* [ ] T201 Add `GroupCollaborationMode` enum (`Local`, `Shared`) for create-flow orchestration in `src/LuSplit.Application/Groups/GroupCollaborationMode.cs`
* [ ] T202 Update shared-group creation and conversion flows to persist local owner membership immediately after success in `src/LuSplit.Application/Groups/UseCases/CreateSharedGroupUseCase.cs` and `src/LuSplit.Application/Groups/UseCases/ConvertGroupToSharedUseCase.cs`
* [ ] T203 Add state-derived UI policy flags (`CanConvertToShared`, `CanInviteMembers`, `CanManageMembers`, `CanManageSharing`, `CanEditGroupSettings`) in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsViewModel.cs`
* [ ] T204 Add state-derived group-surface flags (`ShowInviteAction`, `ShowMembersAction`, `ShowOwnerActions`) in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs`

### Create group flow

* [ ] T205 Update `CreateGroupViewModel` to capture collaboration mode and branch between local completion and shared post-create invite flow in `src/LuSplit.App/Features/Groups/CreateGroup/CreateGroupViewModel.cs`
* [ ] T206 Update `CreateGroupPage.xaml` first step to include explicit **Local** vs **Shared** mode selection with contextual helper copy in `src/LuSplit.App/Features/Groups/CreateGroup/CreateGroupPage.xaml`
* [ ] T207 Update the create-group completion path so **Shared** mode navigates directly to `InvitePage` with the newly created group context in `src/LuSplit.App/Features/Groups/CreateGroup/CreateGroupPage.xaml.cs`
* [ ] T208 Update `InviteViewModel` to support a post-create onboarding state (`IsFirstInviteStep`, helper copy, done/skip behavior) in `src/LuSplit.App/Features/Invitations/InviteViewModel.cs`
* [ ] T209 Update `InvitePage.xaml` to behave as a real second step after shared-group creation, with primary **Invite people** CTA and secondary **Do this later** action in `src/LuSplit.App/Features/Invitations/InvitePage.xaml`

### Existing-group adaptation

* [ ] T210 Update `GroupDetailsPage.xaml` to hide **Convert to Shared Group** for shared groups and show **Invite people** / **Members** actions only when appropriate in `src/LuSplit.App/Features/Groups/GroupDetails/GroupDetailsPage.xaml`
* [ ] T211 Update `GroupPage.xaml` header or action area to expose a visible **Invite** entry point for shared owners and a visible **Members** entry point for all shared groups in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupPage.xaml`
* [ ] T212 Update `ConvertGroupViewModel` so already-shared groups short-circuit to sharing management instead of showing an actionable conversion screen in `src/LuSplit.App/Features/SharedGroups/ConvertGroupViewModel.cs`
* [ ] T213 Update `ConvertGroupPage.xaml` to replace the broken "already shared" error + active convert button with an informational state and a navigation action to invite/member management in `src/LuSplit.App/Features/SharedGroups/ConvertGroupPage.xaml`
* [ ] T214 Add owner-only **Invite people** entry point to `MemberListPage` so group access management is reachable from the members surface too in `src/LuSplit.App/Features/Members/MemberListPage.xaml` and `src/LuSplit.App/Features/Members/MemberListViewModel.cs`

### Navigation / route guards

* [ ] T215 Register direct post-create navigation to invite flow and ensure route parameters carry `groupId` and onboarding context in `src/LuSplit.App/AppShell.xaml.cs` and `src/LuSplit.App/AppRoutes.cs`
* [ ] T216 Add route guards so `ShareGroupPage` / `ConvertGroupPage` cannot remain reachable in invalid state combinations; redirect to invite or group details instead in `src/LuSplit.App/Features/SharedGroups/ShareGroupPage.xaml.cs` and `src/LuSplit.App/Features/SharedGroups/ConvertGroupPage.xaml.cs`
* [ ] T217 De-emphasize or retire standalone `ShareGroupPage` from primary navigation; existing local groups use **Convert**, newly created shared groups use **Invite**, existing shared groups use **Invite/Members** in `src/LuSplit.App/AppShell.xaml.cs` and related navigation handlers

### Copy / discoverability / polish

* [ ] T218 Update group list and group switcher templates so shared/local and owner/member are visibly distinguishable beyond a subtle badge in `src/LuSplit.App/Features/Home/Home/HomePage.xaml`, `src/LuSplit.App/Features/Home/Home/HomeViewModel.cs`, `src/LuSplit.App/Features/Groups/GroupSwitcher/GroupSwitcherPage.xaml`, and `src/LuSplit.App/Features/Groups/GroupSwitcher/GroupSwitcherViewModel.cs`
* [ ] T219 Update empty states in Overview / Expenses / Balances for shared groups so they mention inviting people or waiting for sync where relevant in `src/LuSplit.App/Features/Groups/GroupTimeline/GroupPage.xaml` and `src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs`
* [ ] T220 Normalize terminology across the app so the user sees one consistent concept: **Local group**, **Shared group**, **Share this group**, **Invite people** in `src/LuSplit.App/`
* [ ] T221 Review the **Independent** participant label and replace it if it collides semantically with local/shared collaboration language in `src/LuSplit.App/Features/Groups/CreateGroup/` and `src/LuSplit.App/Features/Groups/GroupDetails/`

### Validation / docs

* [ ] T222 Add a UX acceptance checklist covering four primary journeys in `specs/001-shared-sync-groups/quickstart.md`
* [ ] T223 Update `specs/001-shared-sync-groups/spec.md` with explicit state-driven UX rules for local/shared/owner/member/read-only group surfaces
* [x] T224 Update `specs/001-shared-sync-groups/tasks.md` to include Phase 14 and mark it as required before merge
* [ ] T225 Run end-to-end validation for these journeys and document results: create local group, create shared group and invite, convert local group to shared, open existing shared group as owner/member in `specs/001-shared-sync-groups/quickstart.md`

### Execution priority

Do these first:

1. **T205–T209** — Make local/shared explicit in the create flow and land shared creation on invite.
2. **T210–T213** — Fix invalid shared/local state handling on existing-group screens.
3. **T215–T217** — Repair route reachability so the app cannot land on nonsense states.
4. **T218–T221** — Make the state legible and the copy coherent.
5. **T222–T225** — Lock the behavior into the spec and quickstart.

**Phase 14 Checkpoint**: `dotnet build` clean, `dotnet test` all green. All four group states (local, shared-owner, shared-member, shared-read-only) produce correct action surfaces. Create-shared lands on invite. No invalid screens reachable.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
  └─► Phase 2 (Foundational) ──── BLOCKS ALL USER STORIES
        ├─► Phase 3 (US1 P1) ─────┐
        ├─► Phase 4 (US2 P1) ─────┤ Can run in parallel after Phase 2
        ├─► Phase 5 (US3 P1) ─────┤
        ├─► Phase 6 (US4 P2) ──── depends on Phase 4 (needs Invitation domain)
        ├─► Phase 7 (US5 P2) ─────┤ Independent after Phase 2
        ├─► Phase 8 (US6 P2) ─────┤ Independent after Phase 2
        ├─► Phase 9 (US7 P2) ──── depends on Phase 5 (SyncStatus driven by sync engine)
        ├─► Phase 10 (US8 P3) ─── soft dependency on Phase 8 (membership after revocation)
        ├─► Phase 11 (US9 P3) ─── depends on Phase 5 (conflicts are sync-layer behavior)
        └─► Phase 12 (US10 P3) ── depends on Phase 8 (rotation triggered by revocation)
              └─► Phase 13 (Feature Closure) ── depends on Phases 3–12 where wiring/reachability is required
                    └─► Final Phase (Docs + Validation)
                          └─► Phase 14 (UX Coherence) ── depends on Final Phase; required before merge
```

### User Story Dependencies

| Story | Depends On | Independent? |
|-------|-----------|-------------|
| US1 Create Shared Group (P1) | Phase 2 only | ✅ Yes |
| US2 Invite User (P1) | Phase 2 only | ✅ Yes |
| US3 Sync Expenses (P1) | Phase 2 only | ✅ Yes |
| US4 Accept/Reject Invitation (P2) | US2 (Invitation domain) | Mostly independent |
| US5 Register Device (P2) | Phase 2 only | ✅ Yes |
| US6 Revoke Member (P2) | Phase 2 only | ✅ Yes |
| US7 Sync Status (P2) | US3 (SyncOrchestrationService) | After US3 |
| US8 View Membership (P3) | US6 (MemberListPage scaffolded) | After US6 |
| US9 Handle Conflicts (P3) | US3 (SyncGroupUseCase) | After US3 |
| US10 Key Rotation (P3) | US6 (RevokeMemberUseCase) | After US6 |

**Phase 13 note**: Phase 13 is a cross-cutting closure phase. It does not introduce new user stories; it completes runtime wiring, DI composition, navigation reachability, shared-group write enqueuing, and final UI exposure across US1–US10.

### Within Each User Story

- Tests → Domain models → Ports → Use cases → Adapters/Functions → App/ViewModels
- Models before use cases; use cases before adapters; adapters before ViewModels

---

## Parallel Execution Examples

### Phase 2 (Foundational) — max parallelism

```
Parallel group A (tests):        T009, T010
Parallel group B (domain):       T011, T012, T013, T014, T015, T016, T017
Parallel group C (contracts):    T018, T019, T020
Parallel group D (ports):        T021, T022, T023
Parallel group E (adapters):     T025, T026, T027, T028
Sequential (migrations):         T029 (after domain models)
Sequential (Functions host):     T030 (after T025-T028)
```

### Phase 3 (US1) — after T030

```
Parallel: T031, T032, T033 (tests)
Sequential: T034 → T035, T036 (port before use cases)
Sequential: T037 (adapter after T035)
Parallel: T038, T039 (Functions), T040 (Bicep)
Sequential: T041 → T042, T043, T044 (ViewModels before pages)
```

### P1 Stories in parallel (after Phase 2)

```
Track A: Phase 3 (US1) — T031..T044
Track B: Phase 4 (US2) — T045..T056
Track C: Phase 5 (US3) — T057..T070
```

### Phase 13 (Feature Closure) — grouped execution

```
Sequential group A (sync write pipeline): T153 → T154, T155, T156, T157, T158, T159, T160
Sequential group B (auth/token wiring): T161 → T162
Sequential group C (group details reachability): 
T163 → T164
T165 → T166 → T167
Parallel group D (route / DI wiring): T168, T169, T170, T171, T172
Sequential group E (activity wiring): T173 → T174 → T175 → T176 → T177
Sequential group F (remaining runtime closure): T178 → T179 → T180 → T181 → T182
Parallel group G (XAML resource fixes): T183, T184, T185
Sequential group H (settings reachability): T188 → T189 → T190 → T191
Validation: T186 → T187
```

---

## Implementation Strategy

### MVP First — P1 Stories Only (Phases 1–5 + Final T150–T152)

Complete the three P1 stories for a shippable increment:

1. **Phase 1** — Create projects and solution structure
2. **Phase 2** — Foundational crypto, ports, SQLite, Functions host
3. **Phase 3 (US1)** — Create/convert a shared group
4. **Phase 4 (US2)** — Generate and send invitation links
5. **Phase 5 (US3)** — Sync expenses across devices
6. **T150–T152** — Build, test, Bicep validation

**MVP delivers**: Full end-to-end shared group creation, invitation, and sync for two users.

### Increment 2 — P2 Stories (Phases 6–9)

Add US4 (accept invite), US5 (device management), US6 (revoke member), US7 (sync status UI).

### Increment 3 — P3 Stories (Phases 10–12 + Final Polish)

Add US8 (membership view), US9 (conflict handling), US10 (key rotation) and complete infrastructure and documentation.

### Increment 4 — Feature Closure (Phase 13 + Final Validation)

Complete Phase 13 to close runtime wiring, navigation reachability, auth/token flow, shared-group operation enqueuing, and remaining UI exposure gaps. Then run the Final Phase validation and documentation tasks.

**Increment 4 delivers**: A user-complete shared-group feature where invite, join, sync, revoke, membership, devices, and activity are reachable and executable from the app UI.

### Increment 5 — UX Coherence (Phase 14)

Complete Phase 14 to close the gap between implemented capabilities and the user-facing journey. State-driven UI adaptation, create-flow branching, invite reachability, route guards, copy normalization, and acceptance validation.

**Increment 5 delivers**: A coherent product flow where group state (local, shared-owner, shared-member, shared-read-only) drives every surface, create-shared lands on invite, and no invalid screens are reachable. Required before merge.

---

## Summary

| Phase | Tasks | Story | Priority |
|-------|-------|-------|----------|
| Phase 1: Setup | T001–T008 | — | Blocking |
| Phase 2: Foundational | T009–T030 | — | Blocking |
| Phase 3: US1 Create Shared Group | T031–T044c | US1 | P1 🎯 |
| Phase 4: US2 Invite User | T045–T056 | US2 | P1 🎯 |
| Phase 5: US3 Sync Expenses | T057–T070 | US3 | P1 🎯 |
| Phase 6: US4 Accept/Reject Invitation | T071–T082 | US4 | P2 |
| Phase 7: US5 Register Device | T083–T094 | US5 | P2 |
| Phase 8: US6 Revoke Member | T095–T104e | US6 | P2 |
| Phase 9: US7 View Sync Status | T105–T112 | US7 | P2 |
| Phase 10: US8 View Membership | T113–T120 | US8 | P3 |
| Phase 11: US9 Handle Conflicts | T121–T130 | US9 | P3 |
| Phase 12: US10 Key Rotation | T131–T140 | US10 | P3 |
| Phase 13: Wiring & Navigation Gaps | T153–T191 | — | Required before merge |
| Final: Polish & Validation | T141–T152, T192–T194 | — | Required |
| Phase 14: UX Coherence | T195–T225 | — | Required before merge |
| **Total** | **225 tasks** | | |

**Parallel opportunities**: Significant parallelism remains in Phases 2–12 and selected Phase 13 XAML / DI / validation tasks.
**Test tasks per story**: US1: 4, US2: 3, US3: 3, US4: 3, US5: 3, US6: 4, US7: 2, US8: 2, US9: 3, US10: 3 — 30 focused test tasks total.
**Suggested current completion scope**: Finish Phase 14 (UX Coherence) and its validation tasks before merge.