# @lusplit/infra-local

Local infrastructure adapters for LuSplit:

- SQLite repository implementations for `@lusplit/application` ports
- Schema migration runner (v1)
- Transaction helper for command boundaries
- Deterministic JSON snapshot export/import

Runtime requirements:

- Node `24.13.1` (uses `node:sqlite`)
- Repository entity ids are globally unique per table; group scoping is enforced by composite keys and foreign keys
