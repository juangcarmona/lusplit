<!--
Sync Impact Report
Version change: 2.0.0 -> 2.1.0
Modified principles:
- I. Spec-Kit Governance Hierarchy -> I. Governance Chain of Truth
- II. Layer Ownership and Dependency Direction -> II. Local-First Runtime Boundaries
- III. Thin Views, ViewModel-Owned State -> III. Domain Calculations Are Canonical
- IV. Small Slice Changes with Behavior Preservation -> IV. Collaboration Is Transport, Not Truth
- V. Test and Validation Gates -> V. Thin Views and Verified Slices
Added sections:
- Product and Runtime Constraints
- Delivery Gates
Removed sections:
- Product Guardrails
- Workflow and Review Gates
Templates requiring updates:
- ✅ .specify/templates/plan-template.md
- ✅ .specify/templates/spec-template.md
Follow-up TODOs:
- None
-->
# LuSplit Constitution

## Core Principles

### I. Governance Chain of Truth
This constitution is LuSplit's highest authority. For non-trivial work, an approved
spec-kit `spec.md` is the source of truth for feature intent, scope, and acceptance.
Plans and tasks MUST derive from the approved spec. Code and tests MUST implement and
verify that approved intent. Docs in `docs/` are reference material for architecture,
product direction, UX, and terminology, but MUST NOT override the constitution or an
approved spec. When constitution, spec, docs, and implementation diverge, the mismatch
MUST be resolved explicitly before work continues.

### II. Local-First Runtime Boundaries
LuSplit MUST remain local-first even as it becomes collaboration-capable. The four
runtime projects remain fixed: `LuSplit.Domain` owns rules and invariants,
`LuSplit.Application` owns use cases, queries, ports, and conflict policies,
`LuSplit.Infrastructure` owns SQLite, export, snapshot, and sync adapters, and
`LuSplit.App` owns MAUI presentation. Core expense tracking, balance reading,
settlement planning, and export generation MUST continue to work without connectivity.
Network features MUST degrade gracefully; connectivity loss is a supported runtime
state, not a broken mode.

### III. Domain Calculations Are Canonical
Balances, settlements, split evaluation, and money invariants MUST be deterministic and
computed from the domain's canonical rules. `LuSplit.Domain` is the single source of
truth for calculation behavior. `LuSplit.Application` MAY orchestrate those rules, but
MUST NOT redefine them. Sync, export, snapshot, UI formatting, and any future backend
feature MUST reuse the same domain calculation path or application use case backed by
it. LuSplit MUST NOT introduce alternative calculation paths for local versus synced
groups. Exports that include balances or settlement outcomes MUST derive them from the
same canonical logic used in-app.

### IV. Collaboration Is Transport, Not Truth
Sync is a transport mechanism for group state, not a source of truth. Synced payloads
may carry groups, participants, expenses, transfers, and metadata, but balances and
settlement plans remain derived locally from canonical primitives. Conflict handling
MUST preserve domain invariants and belong above Infrastructure; sync adapters MUST NOT
contain business rules. Identity may be used for sharing and authorization, but MUST
NOT leak into domain rules, money rules, or calculation paths. LuSplit MUST NOT create
parallel architectures for local and collaborative behavior.

### V. Thin Views and Verified Slices
MAUI pages and code-behind MUST stay thin. ViewModels in `LuSplit.App` own UI state,
derived state, validation state, and commands through `CommunityToolkit.Mvvm`.
Business rules, persistence orchestration, and calculation logic MUST NOT live in
ViewModels or code-behind. Changes MUST stay slice-sized, follow an existing pattern
where one exists, and preserve behavior unless the approved spec changes it. New or
changed logic MUST include focused tests in the owning layer, and completed work MUST
run `dotnet build` plus relevant `dotnet test` validation before it is reported done.

## Product and Runtime Constraints

LuSplit is a calm shared-expense tool for families and friends. Collaboration MUST NOT
push the product toward fintech, social-network behavior, or noisy multi-user UI.

- Offline use MUST remain first-class even when sync and optional identity exist.
- Accounts MAY exist, but MUST NOT gate core expense tracking or local group usage.
- Sync failures MUST leave local data usable and must recover without corrupting group
  invariants.
- Accessibility, tone, and calm UX constraints from `docs/product/` and `docs/brand/`
  remain release constraints for relevant work.

## Delivery Gates

Before implementation begins, plans and tasks MUST reflect the current governance chain.

- Plans MUST identify the owning LuSplit project(s), preserve dependency direction, and
  state how local-first behavior is preserved if network features are involved.
- Specs MUST define independently testable stories, acceptance criteria, and any sync,
  identity, offline, or export implications.
- Tasks MUST include file paths, tests for changed logic, and explicit validation work.
- Reviews MUST reject duplicated calculations, business rules in UI or Infrastructure,
  sync-specific calculation paths, or domain coupling to connectivity or identity.

## Governance

This constitution supersedes ad hoc local practice.

- Authority order is fixed: Constitution -> approved specs -> plans/tasks -> code/tests
  -> docs.
- Amendments MUST update this file and any dependent templates or guidance that become
  inconsistent with it.
- Versioning follows semantic versioning for governance: MAJOR for incompatible
  principle redefinition or removal, MINOR for new constraints or materially expanded
  guidance, PATCH for clarifications.
- Compliance review MUST check layer ownership, local-first behavior, canonical domain
  calculations, MVVM boundaries, and required build/test validation.

**Version**: 2.1.0 | **Ratified**: 2026-04-18 | **Last Amended**: 2026-04-18
