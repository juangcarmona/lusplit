---
id: UC-004
type: use-case
title: Add expense
status: draft
primary-actor: ACT-001
supporting-actors: []
governed-by: [BR-001, BR-002, BR-003, BR-004, BR-005, BR-006]
uses-terms: [TERM-EXPENSE, TERM-SPLIT-DEFINITION, TERM-PARTICIPANT]
provenance:
  source: src/LuSplit.Application/Expenses/Commands/AddExpenseUseCase.cs
  confidence: high
  recovered-from: observation
---

## Goal

Record a new shared expense in a group with a valid split definition.

## Trigger

User selects "Add expense" from the group screen.

## Preconditions

The group exists and is not archived. The payer is a participant in the group.

## Main Flow

1. User enters expense title, amount, and selects the payer.
2. User configures the split definition (equal, weighted, percentage, or fixed).
3. System validates the split consumes the full amount.
4. System records the expense.

## Postconditions

## Alternative Flows

- User excludes certain participants from the split.
- User switches between split modes (equal, weighted, percentage, fixed) before saving.

## Failure Conditions

- The split does not consume the full expense amount.
- The group is archived.

## Postconditions

A new expense exists in the group. Balances are updated to reflect the new expense.
