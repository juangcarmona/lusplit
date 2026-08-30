---
id: ACT-001
type: actor
title: Group Member
status: draft
actor-kind: human
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateParticipantUseCase.cs, src/LuSplit.App/Features/Expenses/AddExpense/AddExpenseViewModel.cs
  confidence: high
  recovered-from: observation
---

## Purpose

A person who participates in a group and can pay expenses, owe shares, and make or receive payments.

## Goals

- Understand how much they owe or are owed
- Settle their debts with the fewest possible payments
- Track shared expenses within a group

## Responsibilities

- Pays for shared expenses
- Records manual payments to other members
- Views balances and settlement suggestions

## Boundaries

Does not manage group configuration or membership. Does not access other groups' data.
