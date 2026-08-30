---
id: UC-010
type: use-case
title: Get settlement plan
status: draft
primary-actor: ACT-001
supporting-actors: []
governed-by: [BR-008, BR-010]
uses-terms: [TERM-SETTLEMENT-PLAN, TERM-TRANSFER, TERM-BALANCE]
provenance:
  source: src/LuSplit.Application/Payments/Queries/GetSettlementPlanUseCase.cs, src/LuSplit.Domain/Payments/SettlementPlanner.cs
  confidence: high
  recovered-from: observation
---

## Goal

Get the minimum set of transfers needed to settle all debts in a group.

## Trigger

User views the "who owes whom" section or selects "Settle up".

## Preconditions

A group exists with at least one expense. Balances sum to zero.

## Main Flow

1. System calculates the settlement plan using greedy creditor-debtor matching.
2. System displays the suggested transfers.

## Postconditions

## Alternative Flows

- User records a partial payment instead of the full suggested transfer.

## Failure Conditions

- Balances do not sum to zero (data inconsistency).

## Postconditions

None (read-only query). The user may record individual transfers from the suggestions.
