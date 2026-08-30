---
id: FR-007
type: functional-requirement
title: Balance calculation
status: draft
derived-from: [UC-008, UC-009]
verification:
  - scenario: After recording expenses and payments, each participant's net balance is displayed
provenance:
  source: src/LuSplit.Domain/Payments/BalanceCalculator.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST calculate and display each participant's net balance, and optionally aggregate balances by household for groups with dependents.

## Rationale

Knowing who owes what is the primary value proposition of the product.
