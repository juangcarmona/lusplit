---
id: FR-013
type: functional-requirement
title: Dependent and household support
status: draft
derived-from: [UC-002, UC-003, UC-009]
verification:
  - scenario: A user adds a child as a dependent participant; the child's balance rolls up to the parent in settlement
provenance:
  source: src/LuSplit.Domain/Groups/EconomicUnit.cs, src/LuSplit.Domain/Payments/BalanceCalculator.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST support dependents (e.g., children) whose balances are aggregated under their household's responsible adult for settlement purposes.

## Rationale

Families with children need the children's expenses accounted for without requiring children to settle independently.
