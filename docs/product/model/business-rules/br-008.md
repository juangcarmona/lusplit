---
id: BR-008
type: business-rule
title: Zero-sum balance invariant
status: draft
applies-to: [UC-008, UC-009, UC-010]
provenance:
  source: src/LuSplit.Domain/Payments/BalanceCalculator.cs, tests/LuSplit.Domain.Tests/BalanceParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

The sum of all participant balances in a group must be exactly zero. If not, the system raises an error.

## Rationale

Every cent paid into the group must be owed by someone. A non-zero sum indicates a calculation error or data corruption.

## Examples

- Alice paid $30, Bob owes $15, Carol owes $15 → 30 - 15 - 15 = 0 ✓
- If balances summed to $1 → system error.

## Exceptions

None.
