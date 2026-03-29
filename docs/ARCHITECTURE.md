# Architecture

LuSplit is an offline-first .NET MAUI app.

The solution is split into four runtime projects:

- `LuSplit.Domain`
- `LuSplit.Application`
- `LuSplit.Infrastructure`
- `LuSplit.App`

## Layer responsibilities

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

## Non-goals

LuSplit does not put:
- ViewModels in `Application`
- persistence in pages
- domain rules in code-behind
- MAUI concerns in Domain or Application

## Refactoring rule

Refactors are done one page at a time.

Goal:
- preserve behavior
- reduce code-behind responsibility
- make slice structure predictable
- keep changes small and reviewable