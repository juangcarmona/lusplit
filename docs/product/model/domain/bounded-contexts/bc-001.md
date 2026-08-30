---
id: BC-001
type: bounded-context
title: Expense Splitting
status: draft
provenance:
  source: src/LuSplit.Domain/, src/LuSplit.Application/, docs/ARCHITECTURE.md
  confidence: high
  recovered-from: observation
---

## Responsibility

Managing shared expenses within a group: who paid, how the cost is divided, who owes what, and how debts are settled.

## Language

Everyday financial language oriented toward friends and families: groups, expenses, splits, payments, balances, settlements. Avoids banking and fintech jargon.

## Boundaries

No budgeting workflows. No banking features. No cross-currency conversion. No accounts or authentication.

## External Relationships

The Export & Backup context consumes group data for rendering. User Preferences provides the user's preferred name and currency defaults.
