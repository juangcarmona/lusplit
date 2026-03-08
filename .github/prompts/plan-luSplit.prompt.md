## 1. Executive decision

**Selective port + rewrite** is the right path.

Why:

* `apps/mobile` and `apps/web` are currently README-only placeholders (`apps/mobile/README.md`, `apps/web/README.md`), so there is no substantial UI surface to “incrementally” migrate screen-by-screen.
* The real product value is already implemented and tested in the current domain/application layers (`packages/core`, `packages/application`).
* Infra is Node-specific (`node:sqlite`) and must be rewritten for .NET anyway (`packages/infra-local/src/client.ts`).
* A full rewrite would throw away validated logic and test behavior with no upside.

## 2. What is reusable

* Business rules from `packages/core/src/split/index.ts`, `packages/core/src/balance/index.ts`, `packages/core/src/settlement/index.ts`.
* Domain model semantics from `packages/core/src/entities/index.ts` and `packages/core/DOMAIN.md`.
* Money invariant: integer minor units only (`docs/ARCHITECTURE.md`, `docs/EXPORT_FORMAT.md`, `packages/core/README.md`).
* Application use-case boundaries and port contracts from `packages/application/src/usecases/**` and `packages/application/src/ports/**`.
* Persistence concepts and schema shape from `packages/infra-local/src/migrations.ts` (group-rooted data, FK constraints, transaction boundaries).
* Export/import contract (`version: 1`, complete snapshot) from `packages/infra-local/src/snapshot.ts` and `docs/EXPORT_FORMAT.md`.
* Test scenarios as specification from `packages/core/test/**`, `packages/application/test/unit/**`, `packages/application/test/flows/application-flow.test.ts`, `packages/infra-local/test/infra-local.test.ts`.
* Product/UX/brand docs from `docs/product/MVP_SCOPE.md`, `docs/brand/DESIGN_TOKENS.md`, `docs/brand/BRAND.md`, `docs/ARCHITECTURE.md`.

## 3. What should be discarded

* Any React Native / Next.js runtime assumptions as target architecture (`docs/REPO_STRUCTURE.md`).
* Any React-specific state management patterns in future MAUI code (do not mirror hook/store patterns in C#).
* Node-only infra implementation details (`packages/infra-local/src/client.ts`, `packages/infra-local/src/transaction.ts`) beyond conceptual behavior.
* JS app delivery toolchain as MAUI runtime dependency (`pnpm`, `turbo`, Node engine constraints in `package.json`).
* Node test runner conventions for migrated code (`node --test` in package scripts); replace with .NET-native test tooling for the MAUI slice.

## 4. Proposed MAUI target structure

Place a parallel .NET slice in-repo under `apps/maui` while keeping current packages intact during migration.

Projects:

* `apps/maui/LuSplit.slnx`
* `apps/maui/src/LuSplit.Domain`
  Responsibility: entities, invariants, split/balance/settlement algorithms.
* `apps/maui/src/LuSplit.Application`
  Responsibility: use cases, port interfaces, app models.
* `apps/maui/src/LuSplit.Infrastructure`
  Responsibility: SQLite migrations/repositories/transactions and snapshot import/export v1.
* `apps/maui/src/LuSplit.App`
  Responsibility: MAUI UI, view models, composition root.
* `apps/maui/tests/LuSplit.Domain.Tests`
* `apps/maui/tests/LuSplit.Application.Tests`
* `apps/maui/tests/LuSplit.Infrastructure.Tests`

Dependency direction:

* `LuSplit.Domain` -> none.
* `LuSplit.Application` -> `LuSplit.Domain`.
* `LuSplit.Infrastructure` -> `LuSplit.Application`, `LuSplit.Domain`.
* `LuSplit.App` -> `LuSplit.Application` + `LuSplit.Infrastructure`.
* Tests -> project under test.

DI boundary:

* Single composition root in MAUI startup (`MauiProgram`) where ports are bound to concrete infra.

Platform integration boundary:

* File picker/share/print/storage-path/device APIs stay in `LuSplit.App` (or a thin platform adapter layer), never in Domain/Application.

## 5. Migration phases

### Phase 1: Foundation contract baseline

* Objective: establish the .NET solution and architectural boundaries with zero semantic drift.
* Scope: create projects/references; define only the minimum core contracts needed for parity; scaffold tests.
* Deliverables: `LuSplit.slnx`, project graph, parity checklist doc, baseline invariant tests.
* Risks: translation drift (ordering, IDs, minor-unit constraints).
* Exit criteria: solution builds; dependency direction enforced; baseline tests pass.

### Phase 2: Domain + application parity port

* Objective: port business logic and use-case behavior to C# with parity-first discipline.
* Scope: port core algorithms + use cases; recreate current test scenarios in .NET.
* Deliverables: passing Domain/Application test suites with a parity matrix.
* Risks: rounding/tie-breaker differences, collection-order nondeterminism.
* Exit criteria: deterministic split/balance/settlement tests all green; closed-group and validation behavior matched.

### Phase 3: Local persistence + export/import parity

* Objective: restore offline local operation and data portability in .NET.
* Scope: implement SQLite schema/migrations/repositories/transactions; implement snapshot v1 import/export; contract tests with fixtures.
* Deliverables: `LuSplit.Infrastructure`, round-trip tests.
* Risks: schema or JSON compatibility drift, optional-field serialization mismatch.
* Exit criteria: cross-fixture snapshot compatibility proven; end-to-end offline flow works locally.

### Phase 4: MAUI MVP shell

* Objective: deliver usable MAUI MVP UI on top of validated core.
* Scope: group summary, expense list, add expense flow, balances/settlement view; accessibility and UX constraints from MVP docs.
* Deliverables: MAUI app with command/query wiring and no business math in UI.
* Risks: UI pressure causing logic leakage into view models.
* Exit criteria: MVP checklist passes (`docs/product/MVP_SCOPE.md`), local-first behavior confirmed, no backend dependencies.

## 6. First milestone

**Milestone: Foundation Parity Scaffold**

Scope:

* Create `apps/maui` solution and initial project graph.
* Define only the minimum C# contracts needed for parity.
* Add a parity test harness for foundational invariants (minor units, deterministic ordering assumptions).
* Add canonical fixtures derived from the current implementation.

Non-scope:

* No complete feature screens.
* No full SQLite repository implementation.
* No full import/export engine.
* No backend/network.
* No premature typed IDs or elaborate value objects.

Definition of done:

* `apps/maui/LuSplit.slnx` builds on Windows.
* Project references match target dependency direction.
* Baseline tests pass in Domain/Application test projects.
* A parity matrix document exists mapping current behavior to planned .NET tests.

Likely files/projects to create:

* `apps/maui/LuSplit.slnx`
* `apps/maui/src/LuSplit.Domain/LuSplit.Domain.csproj`
* `apps/maui/src/LuSplit.Application/LuSplit.Application.csproj`
* `apps/maui/src/LuSplit.Infrastructure/LuSplit.Infrastructure.csproj`
* `apps/maui/src/LuSplit.App/LuSplit.App.csproj`
* `apps/maui/tests/LuSplit.Domain.Tests/LuSplit.Domain.Tests.csproj`
* `apps/maui/tests/LuSplit.Application.Tests/LuSplit.Application.Tests.csproj`
* `apps/maui/tests/LuSplit.Infrastructure.Tests/LuSplit.Infrastructure.Tests.csproj`
* `apps/maui/README.md`
* `apps/maui/docs/parity-matrix.md`

## 7. Branch strategy

* Keep a long-lived integration branch: `replatform/maui` (your dedicated branch).
* Do not put all commits directly there; create short-lived milestone branches off it.
* Suggested milestone branches: `replatform/maui-m1-foundation`, `replatform/maui-m2-domain-app`, `replatform/maui-m3-infra`, `replatform/maui-m4-mvp-ui`.
* Merge milestone branches into `replatform/maui` only when phase exit criteria and parity evidence are met.
* Keep `main` clean: no half-migration code merges to `main`.
* Only merge to `main` when the MAUI slice reaches a coherent cutover state, or for stack-agnostic docs-only changes.

## 8. Testing/tooling constraints

* Use .NET-native test tooling.
* Do not use Moq.
* Use NSubstitute where mocking is required.
* Prefer real tests over excessive mocking.
* Avoid unnecessary test abstractions.
