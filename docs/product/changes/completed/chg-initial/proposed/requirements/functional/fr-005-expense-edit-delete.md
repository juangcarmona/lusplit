---
id: FR-005
type: functional-requirement
title: Expense editing and deletion
status: draft
derived-from: [UC-005, UC-006]
verification:
  - scenario: A user edits an expense's title, amount, or split; a user deletes an expense
provenance:
  source: src/LuSplit.Application/Expenses/Commands/EditExpenseUseCase.cs, src/LuSplit.Application/Expenses/Commands/DeleteExpenseUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to edit or delete an existing expense in a non-archived group.

## Rationale

Mistakes happen; users need to correct or remove incorrectly recorded expenses.
