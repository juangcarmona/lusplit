---
id: FR-003
type: functional-requirement
title: Expense recording with split
status: draft
derived-from: [UC-004]
verification:
  - scenario: A user records an expense with a title, amount, payer, and split definition; the expense appears in the group
provenance:
  source: src/LuSplit.Application/Expenses/Commands/AddExpenseUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to record an expense with a title, amount, payer, date, and split definition that divides the cost among participants.

## Rationale

Recording who paid and how the cost is split is the core function of the product.
