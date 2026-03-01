# Architecture

LuSplit is offline-first. v1 has no backend.

## Packages

- core: domain model + deterministic algorithms (split/balance/settlement)
- application: CQRS handlers + ports
- infra-local: SQLite + filesystem/export
- apps/mobile: RN UI
- apps/web: Next.js UI

## Key rules

- Money = integer minor units (cents)
- Domain is pure and fully unit-tested
- UI never recalculates balances; it requests DTOs via queries
