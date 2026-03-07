# LuSplit MAUI Replatform (Milestone 1)

This slice contains the .NET foundation scaffold for the MAUI migration.

## Scope in this milestone

- Create `LuSplit.slnx` and project graph under `apps/maui`.
- Establish architecture boundaries:
  - `LuSplit.Domain` -> no dependencies.
  - `LuSplit.Application` -> `LuSplit.Domain`.
  - `LuSplit.Infrastructure` -> `LuSplit.Application`, `LuSplit.Domain`.
  - `LuSplit.App` -> `LuSplit.Application`, `LuSplit.Infrastructure`.
- Add baseline parity tests for:
  - integer minor-unit money constraints,
  - deterministic ordering assumptions,
  - snapshot contract version pinning.
- Add parity documentation in `apps/maui/docs/parity-matrix.md`.

## Non-scope in this milestone

- Full domain and use-case parity port.
- Full SQLite repository implementation.
- Full snapshot import/export implementation.
- Production MAUI screens.
- Any backend/network integration.

## Build and test

```powershell
dotnet build apps/maui/LuSplit.slnx
dotnet test apps/maui/LuSplit.slnx
```

## Notes

- Money semantics are minor-units only.
- This scaffold intentionally keeps domain/application logic minimal until parity tests from TypeScript are fully mapped and ported.
