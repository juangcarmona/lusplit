---
id: FR-009
type: functional-requirement
title: Group archiving
status: draft
derived-from: [UC-011, UC-012]
verification:
  - scenario: A user archives a group; the group becomes read-only but remains viewable and exportable
provenance:
  source: src/LuSplit.Application/Groups/Commands/CloseGroupUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to archive a group, preventing further mutations while keeping data readable and exportable.

## Rationale

Completed trips or events should be preserved for reference without risk of accidental modification.
