# Repository Structure

LuSplit is a .NET solution with six source projects, an Azure Functions control plane, Bicep infrastructure, and a docs folder.

## Source tree

```
src/
  LuSplit.App/                  MAUI presentation layer
  LuSplit.Application/          Use cases, queries, ports
  LuSplit.Domain/               Business rules and value types
  LuSplit.Infrastructure/       Adapters, repositories, I/O
  LuSplit.Contracts/            Shared wire DTOs (client + server)
  LuSplit.Functions/            Azure Functions control plane

infra/
  main.bicep
  modules/
  parameters/

docs/
  ARCHITECTURE.md
  REPO_STRUCTURE.md
  EXPORT_FORMAT.md
  product/
  brand/

specs/
  001-shared-sync-groups/       SpecKit artifacts for the Shared Sync Groups feature

tests/
  LuSplit.App.Tests/
  LuSplit.Application.Tests/
  LuSplit.Domain.Tests/
  LuSplit.Infrastructure.Tests/
  LuSplit.Functions.Tests/
```

---

## Project roles

### `src/LuSplit.Domain`

Pure business rules and immutable value types. No persistence, no UI, no network.

Contains:

- `Expenses/` — expense entity, split logic, validation
- `Groups/` — group entity, shared group state, membership, key versioning
- `Payments/` — payment entity, transfer entity
- `Sync/` — `Operation`, `OperationType`, `SyncCursor`, `ConflictResolutionPolicy`, `ConflictResolutionResult`, `ConflictOutcome`
- `Activity/` — `ActivityEntry`, `ActivityEntryType`
- `Invitations/` — `Invitation`, `InvitationStatus`
- `Identity/` — `Device`
- `Shared/` — money model, balance logic, settlement logic, domain errors

Rules:

- depends on nothing
- pure business logic only
- deterministic and unit-testable without mocks

### `src/LuSplit.Application`

Use cases, queries, ports, and application models. Defines the contracts that Infrastructure must implement.

Contains:

- `Expenses/` — AddExpense, EditExpense, DeleteExpense use cases and queries
- `Payments/` — RecordPayment, AddTransfer use cases and queries
- `Groups/` — CreateGroup query; `IGroupRepository`, `IGroupRegistrationPort`, `ISharedGroupStateRepository`
- `Sync/` — `SyncGroupUseCase`, `OperationApplicator`, `GetSyncStatusQuery`; ports: `ISyncPort`, `IOperationRepository`, `ISyncCursorRepository`, `IGroupKeyProvider`
- `Identity/` — `RegisterDeviceUseCase`; port: `IDeviceRegistrationPort`
- `Invitations/` — `CreateInvitationUseCase`, `AcceptInvitationUseCase`, `DeclineInvitationUseCase`, `GetPendingInvitationsQuery`; port: `IInvitationPort`
- `KeyManagement/` — `RotateGroupKeyUseCase`; port: `IKeyRotationPort`
- `Revocation/` — `RevokeMemberUseCase`, `TransferOwnershipUseCase`; port: `IRevocationPort`
- `Shared/Ports/` — `IAuthPort`, `IEncryptionPort`, `IKeyWrapPort`, `ISecureKeyStoragePort`, `IActivityEntryPort`, `IIdGenerator`, `IClock`

Rules:

- depends on `LuSplit.Domain` and `LuSplit.Contracts` only
- no MAUI, no network calls, no persistence implementations

### `src/LuSplit.Infrastructure`

Implements Application ports. All side-effecting I/O lives here.

Contains:

- `Crypto/` — `AesGcmEncryptionAdapter` (`IEncryptionPort`), `RsaKeyWrapAdapter` (`IKeyWrapPort`), `SecureKeyStorageAdapter` (`ISecureKeyStoragePort`)
- `Identity/` — `MsalAuthAdapter` (`IAuthPort`)
- `Sync/` — `BlobSyncAdapter` (`ISyncPort`), `GroupKeyProvider` (`IGroupKeyProvider`), `OperationRepositorySqlite` (`IOperationRepository`), `SyncCursorRepositorySqlite` (`ISyncCursorRepository`)
- `ControlPlane/` — `ControlPlaneHttpClient`, `DeviceRegistrationAdapter`, `GroupRegistrationAdapter`, `InvitationAdapter`, `MemberRevocationAdapter`, `KeyRotationAdapter`
- `Groups/` — `SharedGroupStateRepositorySqlite`, `GroupMembershipRepositorySqlite`
- `Activity/` — `ActivityEntryRepository`
- `Expenses/` — expense SQLite repository
- `Payments/` — payment and transfer SQLite repositories
- `Export/` — export adapters
- `Sqlite/` — `InfraLocalSqlite` (SQLite composition root, schema migrations)
- `Snapshot/` — snapshot helpers

Rules:

- depends on Application and Domain
- no page logic, no ViewModel logic

### `src/LuSplit.App`

MAUI presentation layer organized as vertical feature slices.

```
Features/
  Activity/          ActivityFeedPage, ActivityFeedViewModel
  Auth/              AuthenticationPage, AuthenticationViewModel
  Devices/           DeviceManagementPage, DeviceManagementViewModel
  Expenses/
    AddExpense/      AddExpensePage, AddExpenseViewModel
    ExpenseDetails/  ExpenseDetailsPage, ExpenseDetailsViewModel, ConflictReviewPromptViewModel
    Shared/          ParticipantSplitRowViewModel, ExpenseParticipantRowViewModel
  Groups/
    ArchivedGroups/
    ArchivedGroupView/
    CreateGroup/
    GroupDetails/
    GroupSwitcher/
    GroupTimeline/
    Shared/          GroupViewModel
  Home/              HomePage, HomeViewModel
  Invitations/       InvitationLandingPage, InvitationLandingViewModel
  Members/           MemberListPage, MemberListViewModel
  Payments/
    RecordPayment/
    Settlement/
  Settings/          SettingsPage, SettingsViewModel
  SharedGroups/      ShareGroupPage, ShareGroupViewModel
  Sync/              SyncStatusViewModel

Services/
  Persistence/       AppDataService
  SyncOrchestrationService
  ConflictFlagStore
```

Rules:

- depends on Application (Infrastructure only for DI wiring in `MauiProgram.cs`)
- code-behind limited to `InitializeComponent`, `BindingContext`, and minimal lifecycle wiring
- no business rules, no persistence orchestration in pages or code-behind

### `src/LuSplit.Contracts`

Shared wire types. Referenced by both `LuSplit.Infrastructure` (client) and `LuSplit.Functions` (server).

Contains:

- `Sync/` — `OperationEnvelope`, operation payload types per `OperationType`
- `ControlPlane/` — all request/response DTOs for the control-plane HTTP API

Depends on nothing.

### `src/LuSplit.Functions`

Azure Functions isolated worker (`net10.0`, v4). Implements the control plane.

Contains:

- `DeviceFunctions` — device registration, listing, revocation
- `GroupFunctions` — group registration, metadata
- `SyncFunctions` — SAS token issuance
- `InvitationFunctions` — invitation create, cancel, accept, decline
- `MemberFunctions` — member revocation, ownership transfer
- `KeyFunctions` — wrapped group key upload and retrieval
- `Middleware/` — `EntraTokenValidationMiddleware` (target; currently auth is anonymous)

Depends on `LuSplit.Contracts` only.

---

## Infrastructure (`infra/`)

Bicep Infrastructure as Code for all Azure resources.

```
infra/
  main.bicep                   Orchestrator; composes modules; emits outputs
  modules/
    storage.bicep              StorageV2 account (encrypted operation log)
    keyvault.bicep             Key Vault (RBAC mode, control-plane secrets)
    identity.bicep             Pass-through; outputs Entra authority values (manual setup)
    monitoring.bicep           Log Analytics workspace, Application Insights, sync-error alert
    functions.bicep            Consumption plan, Function App, RBAC assignments
  parameters/
    dev.bicepparam             Dev environment: westeurope, dev, lusplit
    prod.bicepparam            Prod environment: westeurope, prod, lusplit
```

Outputs from `main.bicep`: `storageAccountName`, `keyVaultName`, `functionAppName`, `functionAppPrincipalId`.

---

## Specs (`specs/`)

SpecKit design artifacts per feature branch.

```
specs/
  001-shared-sync-groups/
    spec.md        Feature specification
    plan.md        Implementation plan
    tasks.md       Dependency-ordered task list (T001–T152, all complete)
```

Specs are the source of truth for feature intent. When implementation diverges from spec, the divergence is documented in `docs/ARCHITECTURE.md`, not silently corrected in specs.

---

## Presentation slice direction

Each non-trivial screen is a feature slice:

```
Features/<Feature>/
  <Feature>Page.xaml
  <Feature>Page.xaml.cs
  <Feature>ViewModel.cs
  [<Row>ViewModel.cs]         optional row/item viewmodels
  [I<Feature>DataService.cs]  optional UI-only data service interface
```

---

## Documentation map

| File | Purpose |
|------|---------|
| `docs/ARCHITECTURE.md` | System context, layer rules, sync architecture, security model, deployment |
| `docs/REPO_STRUCTURE.md` | This file — project roles and folder conventions |
| `docs/EXPORT_FORMAT.md` | CSV/PDF export format specification |
| `docs/product/` | UX principles, MVP scope |
| `docs/brand/` | Brand tokens, voice and tone, logo usage |

Architecture docs are the source of truth for implemented behavior. Product docs define UX and product constraints.

---

## Repo rules

- one responsibility per project
- one slice refactor at a time
- small and reviewable changes
- no whole-app rewrites
- no duplicate business logic in the presentation layer
- no domain rules in code-behind
