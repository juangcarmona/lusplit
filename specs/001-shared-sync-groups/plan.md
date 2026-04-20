# Implementation Plan: Shared Synchronized Groups

**Branch**: `001-shared-sync-groups` | **Date**: 2026-04-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-shared-sync-groups/spec.md`

## Summary

Enable LuSplit users to share groups and synchronize expense data across authorized users and devices using a local-first architecture backed by a minimal Azure control plane (Entra External ID, Azure Functions, Blob Storage, Key Vault). Group content is client-encrypted with per-user asymmetric key wrapping, and all authorization decisions are enforced server-side. The existing domain calculation layer remains the single source of truth — sync is transport, not truth.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: .NET MAUI, CommunityToolkit.Mvvm 8.4.0, Microsoft.Data.Sqlite 9.0.10, MSAL.NET (to add), Azure.Storage.Blobs (to add), System.Security.Cryptography
**Storage**: Local: SQLite via Microsoft.Data.Sqlite. Remote: Azure Blob Storage (encrypted blobs). Control-plane state: Azure Blob Storage or Table Storage (metadata only, no group content).
**Testing**: xUnit, NSubstitute, `dotnet test`
**Target Platform**: .NET MAUI — Android (net10.0-android), iOS (net10.0-ios), macOS Catalyst (net10.0-maccatalyst), Windows (net10.0-windows)
**Project Type**: Mobile app (MAUI) + serverless control plane (Azure Functions) + infrastructure-as-code (Bicep)
**Performance Goals**: Sync latency < 30s between online devices. App launch overhead < 500ms. Offline read/write with zero network dependency.
**Constraints**: Local-first/offline-capable. No long-lived secrets on device. Group data encrypted at rest in remote storage. < $50/month Azure cost for 10,000 active shared groups.
**Scale/Scope**: 2–8 members per group typical. Up to 10,000 active shared groups in baseline. 10 user stories across identity, sharing, sync, revocation, and UX.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|------|--------|-------|
| Approved spec exists and governs this plan | PASS | `specs/001-shared-sync-groups/spec.md` with 5 clarifications |
| Architecture docs reviewed | PASS | `docs/ARCHITECTURE.md`, `docs/REPO_STRUCTURE.md`, `docs/product/MVP_SCOPE.md` reviewed |
| Owning projects identified | PASS | Domain, Application, Infrastructure, App (existing) + Functions, Bicep, Contracts (new) |
| Dependency direction preserved | PASS | No UI in Domain/Application; no business rules in App code-behind; sync adapters implement Application ports |
| Offline remains first-class | PASS | All reads/writes local-first; sync additive; connectivity loss is supported runtime state per Constitution §II |
| Domain calculations canonical | PASS | Balances, settlements, splits reuse existing Domain logic; sync carries operations, not computed results per Constitution §III |
| Existing pattern identified | PASS | Sync adapters follow existing port/adapter pattern in Infrastructure; new projects justified in Complexity Tracking |
| Test plan covers new logic | PASS | Each new layer slice requires focused tests; validation scope: `dotnet build` + `dotnet test` all projects + `az bicep build` |
| Product/UX constraints aligned | PASS | Calm UX, ambient sync indicators, offline-is-normal per `docs/product/UX_PRINCIPLES.md` and `docs/brand/VOICE_AND_TONE.md` |

### Post-Design Re-Evaluation (after Phase 1)

| Gate | Status | Post-Design Notes |
|------|--------|-------------------|
| Dependency direction preserved | PASS | data-model.md places entities in correct layers; contracts/ defines DTOs separate from domain; control-plane API does not expose domain internals |
| Offline remains first-class | PASS | sync-operations.md defines pull/push flows that degrade gracefully; initial sync bootstraps from snapshots; no online-only operations for core expense tracking |
| Domain calculations canonical | PASS | Operations carry raw data (amounts, splits), not computed balances; conflict resolution merges fields without recalculating outside domain; snapshots store entity state, not derived values |
| Collaboration is transport, not truth | PASS | sync-operations.md treats sync as blob transport; conflict rules live above Infrastructure; identity limited to authorization and key distribution; no parallel calculation paths |
| No business rules in adapters | PASS | control-plane-api.md handles auth/keys/invitations only; sync adapter will implement Application-defined ports; encryption is a cross-cutting concern, not business logic |
| Product constraints aligned | PASS | quickstart.md confirms calm UX, no social features, no fintech behavior; invitation via share sheet only; ambient sync indicators |

## Project Structure

### Documentation (this feature)

```text
specs/001-shared-sync-groups/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── control-plane-api.md
│   └── sync-operations.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (planned)

```text
src/
├── LuSplit.App/                    # Existing — add auth, sync UI, sharing flows
├── LuSplit.Application/            # Existing — add sync use cases, new ports
├── LuSplit.Domain/                 # Existing — add operation model, membership, conflict rules
├── LuSplit.Infrastructure/         # Existing — add sync adapter, auth adapter, crypto adapter
├── LuSplit.Contracts/              # NEW — shared operation schemas, API contracts
└── LuSplit.Functions/              # NEW — Azure Functions control plane

infra/
├── main.bicep                      # NEW — orchestrator
├── modules/
│   ├── functions.bicep
│   ├── storage.bicep
│   ├── keyvault.bicep
│   ├── identity.bicep
│   └── monitoring.bicep
└── parameters/
    ├── dev.bicepparam
    └── prod.bicepparam

tests/
├── LuSplit.App.Tests/              # Existing — add sync VM tests, auth flow tests
├── LuSplit.Application.Tests/      # Existing — add sync use case tests
├── LuSplit.Domain.Tests/           # Existing — add operation model tests, conflict tests
├── LuSplit.Infrastructure.Tests/   # Existing — add sync adapter tests, crypto tests
├── LuSplit.Contracts.Tests/        # NEW — schema validation tests
└── LuSplit.Functions.Tests/        # NEW — control plane endpoint tests
```

**Structure Decision**: This feature touches all four existing projects and introduces three new solution areas (Functions, Contracts, Bicep infra). Each new area is justified in Complexity Tracking below. The existing feature-slice organization inside `LuSplit.App` continues — new screens (sharing, membership, device management, sync status) each get their own slice.

## Complexity Tracking

| New Area | Why Needed | Simpler Alternative Rejected Because |
|----------|------------|-------------------------------------|
| `LuSplit.Functions` (Azure Functions project) | Hosts the control plane that enforces authorization, manages invitations, distributes wrapped keys, and issues scoped SAS tokens. The client must NOT be trusted to make these decisions (spec §Security). Constitution §II (v2.2.0) permits a server-side control plane as a separate deployment boundary that MUST NOT own business rules or domain calculations. | Putting authorization logic in the client violates the trust boundary model. A shared library without a hosted endpoint cannot issue tokens or enforce server-side access control. |
| `LuSplit.Contracts` (shared library) | Defines operation schemas, API request/response shapes, and sync protocol types shared between the mobile app and the Functions project. Constitution §II (v2.2.0) permits a contracts library that contains no behavior. | Duplicating types in both projects creates drift risk. Embedding contracts in Application would couple the domain layer to control-plane concerns. |
| `infra/` (Bicep modules) | Infrastructure-as-code is a first-class monorepo concern per FR-044. Bicep defines Functions, Blob Storage, Key Vault, Entra External ID config, and monitoring. | Manual Azure portal provisioning is not reproducible, auditable, or suitable for environment promotion. |

## Open Questions Resolved During Planning

The spec deferred 5 open questions. The following are resolved by planning research:

1. **Snapshot frequency and lifecycle** → Automatic snapshots every 100 operations per group. Retain the 3 most recent snapshots; older snapshots are deleted when a newer one is confirmed durable. Snapshots are created by any device that crosses the threshold during sync.

2. **Operation log compaction** → Operations older than the oldest retained snapshot MAY be deleted during a compaction pass. Compaction is performed by the device that creates a snapshot, using a control-plane-mediated lock to prevent concurrent compaction. Compaction is best-effort; failure leaves stale operations in place (harmless due to idempotency).

3. **Group size limits** → Soft limit of 20 members per group (enforced by the control plane; owner warned at 15). No hard operation count limit; snapshot + compaction keeps sync time bounded. These limits can be raised later.

4. **Data residency** → No data residency constraint for the baseline. Deploy to a single Azure region chosen for cost and latency. Add multi-region support as a future enhancement if required by regulation or user demand.

5. **Cost model validation** → Estimated cost breakdown for 10,000 active groups at ~5 ops/group/day:
   - Blob Storage (50K blobs/day, ~500 bytes avg): < $5/month
   - Azure Functions (Consumption, ~150K invocations/day): < $10/month
   - Key Vault (key operations): < $2/month
   - Entra External ID (up to 50K MAU free tier): $0 baseline
   - Monitoring (Application Insights): < $5/month
   - **Total estimate: ~$22/month**, well within the $50 target.