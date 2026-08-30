---
id: FR-008
type: functional-requirement
title: Settlement plan suggestion
status: draft
derived-from: [UC-010]
verification:
  - scenario: The system suggests the minimum set of transfers to settle all debts
provenance:
  source: src/LuSplit.Domain/Payments/SettlementPlanner.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST suggest a settlement plan with the minimum number of transfers to zero out all balances.

## Rationale

Minimizing the number of payments reduces the effort to settle shared expenses.
