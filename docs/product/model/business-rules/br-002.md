---
id: BR-002
type: business-rule
title: Expense amount must be positive
status: draft
applies-to: [UC-004, UC-005]
provenance:
  source: src/LuSplit.Application/Expenses/Commands/AddExpenseUseCase.cs, tests/LuSplit.Application.Tests/AddExpenseUseCaseTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

An expense amount must be greater than zero.

## Rationale

A zero or negative expense has no meaningful financial interpretation in a shared-expense tracker.

## Examples

- Recording an expense of $0 is rejected.
- Recording an expense of -$10 is rejected.

## Exceptions

None.
