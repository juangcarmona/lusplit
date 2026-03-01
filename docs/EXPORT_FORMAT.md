# Export / Import Format (Canonical)

## Canonical snapshot (JSON) — required

Exports MUST be lossless and re-importable. JSON is the only canonical format.

### Envelope

* `schemaVersion: 1`
* `exportedAt: ISO-8601`
* `appVersion: string` (optional but recommended)

### Payload (authoritative persisted entities only)

* `group: { id, currency, closed }`
* `economicUnits: [{ id, groupId, ownerParticipantId, name? }]`
* `participants: [{ id, groupId, economicUnitId, name, consumptionCategory, customConsumptionWeight? }]`
* `expenses: [{ id, groupId, title, paidByParticipantId, amountMinor, date, splitDefinition, notes? }]`
* `transfers: [{ id, groupId, fromParticipantId, toParticipantId, amountMinor, date, type, note? }]`

### Rules

* Amounts are **minor units** (`amountMinor: int`)
* Dates are **ISO-8601 strings**
* Snapshot contains **no projections** (no balances, no settlement plans)
* **Deterministic ordering** on export:

  * All arrays sorted by `id` ascending (string compare)
* Import must be **idempotent** when importing the same snapshot into an empty DB
* After import:

  * Application queries must produce identical balances/settlement to pre-export (deterministic algorithms)

## Non-canonical exports (optional, later)

These are read-only projections and must NOT be used for import.

* CSV: `expenses.csv`, `participants.csv`, `transfers.csv`
* PDF: balances + settlement summary
