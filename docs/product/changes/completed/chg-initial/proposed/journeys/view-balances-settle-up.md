---
id: JRN-003
type: journey
title: View balances and settle up
status: draft
primary-actor: ACT-001
steps:
  - use-case: UC-008
  - use-case: UC-009
  - use-case: UC-010
provenance:
  source: src/LuSplit.App/Features/Home/Home/HomeViewModel.cs, src/LuSplit.Domain/Payments/SettlementPlanner.cs
  confidence: high
  recovered-from: observation
---

## Intended Outcome

The user understands who owes what and can settle all debts with the minimum number of payments.

## Entry Conditions

A group has at least one expense recorded.

## Journey Narrative

1. The user views the group's balances tab.
2. The system shows each participant's net balance and "who owes whom" lines.
3. The system suggests a settlement plan with the minimum number of transfers.
4. The user records payments as they are made.

## Variants and Branches

- If dependents exist, balances and settlements are shown in household-aggregated mode.
- If all balances are zero, the system shows "all settled" state.

## Completion Conditions

The user has viewed the group's balances and, if debts exist, recorded one or more payments to settle them.
