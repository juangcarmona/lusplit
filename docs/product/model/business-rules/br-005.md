---
id: BR-005
type: business-rule
title: Group scope integrity
status: draft
applies-to: [UC-002, UC-004, UC-007]
provenance:
  source: src/LuSplit.Domain/Groups/GroupScopeAssertions.cs, tests/LuSplit.Domain.Tests/SplitParityTests.cs, tests/LuSplit.Domain.Tests/BalanceParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

All participants, economic units, expenses, and transfers referenced in an operation must belong to the same group. Cross-group references are rejected.

## Rationale

Groups are isolated financial contexts. Mixing data across groups would produce meaningless balances.

## Examples

- Adding an expense with a payer from a different group is rejected.
- A split referencing a participant from another group is rejected.

## Exceptions

None.
