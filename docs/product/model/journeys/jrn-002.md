---
id: JRN-002
type: journey
title: Log a shared expense and split it
status: draft
primary-actor: ACT-001
steps:
  - use-case: UC-004
provenance:
  source: src/LuSplit.App/Features/Expenses/AddExpense/AddExpenseViewModel.cs, src/LuSplit.Application/Expenses/Commands/AddExpenseUseCase.cs
  confidence: high
  recovered-from: observation
---

## Intended Outcome

A new expense is recorded in the group with a valid split definition. All participants' balances are updated.

## Entry Conditions

A group exists with at least two participants. The group is not archived.

## Journey Narrative

1. The user opens the add expense form.
2. The user enters a title, amount, and selects who paid.
3. The user configures the split: equal (default), weighted, percentage, or fixed amounts per participant.
4. The system validates the split consumes the full amount and shows a preview.
5. The user saves the expense.

## Variants and Branches

- The user may exclude certain participants from the split.
- The user may use "Adults Only" quick action to exclude dependents.
- The user may switch between split modes before saving.

## Completion Conditions

A new expense is saved with a valid split definition and all participants' balances reflect the new expense.
