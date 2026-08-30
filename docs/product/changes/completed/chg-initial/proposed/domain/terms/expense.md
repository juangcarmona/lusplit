---
id: TERM-EXPENSE
type: domain-term
title: Expense
status: draft
defined-in: BC-001
synonyms: []
provenance:
  source: src/LuSplit.Domain/Expenses/Expense.cs
  confidence: high
  recovered-from: observation
---

## Definition

A shared cost recorded in a group. Has a title, amount (in minor currency units), a payer, a date, and a split definition that determines how the cost is divided among participants.

## Distinguish From

- **Transfer**: A payment between participants; expenses are about spending, transfers are about settling.
- **Split Definition**: The rules for dividing an expense; the expense is the cost itself.

## Usage

Expenses are the primary input to balance calculations. Each expense increases the payer's credit and increases each split participant's debt.
