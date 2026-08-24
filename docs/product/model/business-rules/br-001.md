---
id: BR-001
type: business-rule
title: Archived group immutability
status: draft
applies-to: [UC-001, UC-002, UC-003, UC-004, UC-005, UC-006, UC-007, UC-011]
provenance:
  source: src/LuSplit.Application/Shared/Commands/UseCaseGuards.cs, tests/LuSplit.Application.Tests/ArchiveTripFlowTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

No mutations are allowed on an archived (closed) group. This includes adding expenses, participants, economic units, and payments.

## Rationale

Archiving signals that the group's financial activity is complete. Allowing mutations after archiving would undermine the finality of the settlement.

## Examples

- Adding an expense to an archived group is rejected.
- Adding a participant to an archived group is rejected.
- Archiving an already-archived group succeeds (idempotent).

## Exceptions

None. Read operations (viewing, exporting) remain available on archived groups.
