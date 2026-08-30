---
id: BR-010
type: business-rule
title: Minimum-transfer settlement
status: draft
applies-to: [UC-010]
provenance:
  source: src/LuSplit.Domain/Payments/SettlementPlanner.cs, tests/LuSplit.Domain.Tests/SettlementParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

The settlement planner produces the minimum number of transfers to zero out all balances using a greedy creditor-debtor matching approach. Creditors and debtors are sorted by participant ID for deterministic output.

## Rationale

Minimizing the number of payments reduces friction in settling shared expenses.

## Examples

- Alice is owed $20, Bob owes $15, Carol owes $5 → one transfer from Bob to Alice ($15) and one from Carol to Alice ($5) = 2 transfers.

## Exceptions

If all balances are zero, the result is an empty list (everyone is settled).
