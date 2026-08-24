---
id: UC-006
type: use-case
title: Delete expense
status: draft
primary-actor: ACT-001
supporting-actors: []
governed-by: [BR-001]
uses-terms: [TERM-EXPENSE]
provenance:
  source: src/LuSplit.Application/Expenses/Commands/DeleteExpenseUseCase.cs
  confidence: high
  recovered-from: observation
---

## Goal

Remove an expense from a group.

## Trigger

User deletes an expense from the expense details screen.

## Preconditions

The group is not archived. The expense exists in the group.

## Main Flow

1. User confirms deletion.
2. System removes the expense.

## Postconditions

## Alternative Flows

- User cancels the deletion confirmation.

## Failure Conditions

- The group is archived.
- The expense does not exist in the group.

## Postconditions

The expense no longer exists. Balances are recalculated.
