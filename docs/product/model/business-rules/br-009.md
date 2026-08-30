---
id: BR-009
type: business-rule
title: Dependent balances aggregate to owner
status: draft
applies-to: [UC-009, UC-010]
provenance:
  source: src/LuSplit.Domain/Payments/BalanceCalculator.cs (AggregateBalancesByEconomicUnitOwner), tests/LuSplit.Domain.Tests/BalanceParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

Dependent participants' balances are aggregated under their economic unit owner for household-level settlement. The owner must belong to their own unit.

## Rationale

Children and other dependents do not independently settle debts; their responsible adult handles payments on their behalf.

## Examples

- Child owes $10, Parent is owed $30 → Household net: $20 owed to Parent.

## Exceptions

None.
