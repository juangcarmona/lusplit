---
id: FR-001
type: functional-requirement
title: Group creation
status: draft
derived-from: [UC-001]
verification:
  - scenario: A user creates a group with a name and currency; the group appears in the active groups list
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateGroupUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to create a group with a name and a currency.

## Rationale

Groups are the fundamental container for shared expenses.
