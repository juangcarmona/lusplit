# Architecture

LuSplit is a local-first .NET MAUI app.

The solution is split into four runtime projects:

- `LuSplit.Domain`
- `LuSplit.Application`
- `LuSplit.Infrastructure`
- `LuSplit.App`

## Project responsibilities

### `LuSplit.Domain`

Owns pure business rules and invariants.

Examples:
- money rules
- split rules
- balance calculation
- settlement planning
- entity invariants

Rules:
- no UI
- no persistence
- no framework concerns
- deterministic and unit-testable

### `LuSplit.Application`

Owns use cases, queries, ports, and application models.

Examples:
- create/edit/delete expense use cases
- group creation and closing
- balance and settlement queries
- repository contracts

Rules:
- depends on Domain only
- no MAUI, XAML, navigation, dialogs, or device APIs
- no infrastructure details

### `LuSplit.Infrastructure`

Owns adapters that implement Application ports.

Examples:
- SQLite repositories
- export services
- snapshot services
- filesystem-backed implementations

Rules:
- depends on Application and Domain
- no page logic
- no viewmodel logic

### `LuSplit.App`

Owns the presentation layer.

Examples:
- pages and views
- viewmodels
- navigation
- dialogs
- media/file picker
- UI formatting and presentation helpers

Rules:
- may depend on Application
- must not move business rules out of Domain/Application
- must keep code-behind thin

## Presentation pattern

LuSplit uses MVVM in the MAUI app with `CommunityToolkit.Mvvm`.

Preferred primitives:
- `ObservableObject`
- `[ObservableProperty]`
- `[RelayCommand]`
- `[NotifyCanExecuteChangedFor]`

ViewModels live in `LuSplit.App`.

They own:
- page state
- derived state
- validation state
- commands
- orchestration of Application use cases and queries

Pages remain thin.

Code-behind is limited to:
- `InitializeComponent`
- `BindingContext`
- tiny lifecycle handoff
- strictly view-only behavior

## UI structure inside `LuSplit.App`

`LuSplit.App` is organized by feature and slice, not by top-level technical buckets such as `Pages/`, `ViewModels/`, and `Presentation/`.

Each non-trivial screen should keep its screen-specific artifacts together:
- page
- page code-behind
- viewmodel
- row viewmodels
- local parsers
- local mappers
- local UI helpers
- local service interfaces when needed

Cross-feature app-side services stay in shared service areas.

## Vertical slice canon

Each non-trivial screen is a presentation slice.

A slice may contain:
- page
- page code-behind
- viewmodel
- optional presentation mapper
- optional UI-only service

Canonical rules:

1. Thin view
2. ViewModel owns state
3. Commands over handlers
4. Use cases below UI
5. UI-only services stay in App
6. No business rules in code-behind

## Dependency direction

- `LuSplit.Domain` depends on nothing
- `LuSplit.Application` depends on `LuSplit.Domain`
- `LuSplit.Infrastructure` depends on `LuSplit.Application` and `LuSplit.Domain`
- `LuSplit.App` depends on `LuSplit.Application` and app-side services/helpers

## Sync and collaboration model

LuSplit is evolving from a single-device coordinator to a collaborative,
multi-device system. Sync is optional. The app remains fully functional
offline.

### What stays local

- All domain logic: split rules, balance calculation, settlement planning
- Domain remains deterministic and unit-testable with no network awareness
- Local SQLite is the primary data store; sync never replaces it

### What syncs

- Group membership and participant lists
- Expenses and transfers
- Group metadata (name, currency, closed state)

Balances and settlements are never synced. They are always computed locally
from synced primitives.

### Architectural placement

Sync is an Infrastructure concern. It implements Application ports, same as
SQLite repositories.

- `LuSplit.Application` defines sync contracts as ports (push/pull change sets)
- `LuSplit.Infrastructure` implements sync adapters
- Domain has no knowledge of sync
- App has no knowledge of sync mechanics (only connectivity state for UX)

Dependency direction is unchanged. Sync adapters depend on Application and
Domain, never the reverse.

### Authority model

Local state is authoritative. Sync is eventually consistent.

- Each device produces changes locally and pushes them upstream
- Conflicts are resolved by Application-level policies, not domain logic
- The domain layer never sees "merge" or "conflict" — it only sees resolved
  state

### Identity

Accounts are optional. A device identity is sufficient for sync participation.
If accounts are introduced, they must not gate core expense-tracking
functionality.

### Risks and boundaries

- Domain logic must never be duplicated in sync adapters
- Sync must not introduce non-determinism into balance or settlement
  computation
- Conflict resolution policies belong in Application, not Infrastructure
- Sync failures must degrade gracefully — the app must remain usable offline
- No real-time presence, typing indicators, or social-network patterns

## Non-goals

LuSplit does not put:
- ViewModels in `Application`
- persistence in pages
- domain rules in code-behind
- MAUI concerns in Domain or Application
- sync logic in Domain
- conflict resolution in Infrastructure
- account gates on core expense tracking

## Refactoring rule

Refactors are done one page at a time.

Goal:
- preserve behavior
- reduce code-behind responsibility
- make slice structure predictable
- keep changes small and reviewable