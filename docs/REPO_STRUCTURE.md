# Repository Structure

LuSplit is a .NET solution with four main projects and a docs folder.

## Source tree

```text
src/
  LuSplit.App/
  LuSplit.Application/
  LuSplit.Domain/
  LuSplit.Infrastructure/

docs/
  ARCHITECTURE.md
  REPO_STRUCTURE.md
  product/
  brand/
````

## Project roles

### `src/LuSplit.App`

Presentation layer for the .NET MAUI app.

Contains:

* `Pages/`
* app-side `Services/`
* resources, styles, localization
* shell, routes, startup
* viewmodels and presentation helpers as the refactor progresses

Rules:

* views and viewmodels stay here
* navigation/dialog/media concerns stay here
* no domain rules in code-behind

### `src/LuSplit.Application`

Application layer.

Contains:

* `Commands/`
* `Queries/`
* `Models/`
* `Ports/`
* application errors

Rules:

* orchestrates use cases
* defines contracts for infrastructure
* does not know MAUI or XAML

### `src/LuSplit.Domain`

Domain layer.

Contains:

* entities
* money model
* split logic
* balance logic
* settlement logic
* domain errors

Rules:

* pure business logic only
* deterministic
* framework-independent

### `src/LuSplit.Infrastructure`

Infrastructure layer.

Contains:

* SQLite repositories
* export adapters
* snapshot services
* local client/integration code

Rules:

* implements Application ports
* no UI logic

## Presentation slice direction

Pages are organized as slices, not as giant page/controller files.

Each non-trivial page should evolve toward:

```text
LuSplit.App/
  Pages/
    <Feature>/
      <Page>.xaml
      <Page>.xaml.cs
  ViewModels/
    <Feature>/
      <Page>ViewModel.cs
  Presentation/
    <Feature>/
      <Page>UiMapper.cs
      <Page>Draft.cs
      <Page>Parser.cs
  Services/
    Navigation/
    Dialogs/
    Media/
```

Exact folders may vary.
The rule is structural clarity, not folder dogma.

## Current documentation split

### Core docs

* `ARCHITECTURE.md`
* `REPO_STRUCTURE.md`

### Product docs

* `product/`
* `brand/`

Rule:

* architecture docs define technical source of truth
* product docs define UX, language, and product constraints

## Repo rules

* one responsibility per project
* one page refactor at a time
* small reviewable changes
* no whole-app rewrites
* no duplicate business logic in the presentation layer
