---
id: FR-006
type: functional-requirement
title: Manual payment recording
status: draft
derived-from: [UC-007]
verification:
  - scenario: A user records a payment from one participant to another; balances update accordingly
provenance:
  source: src/LuSplit.Application/Payments/Commands/AddManualTransferUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to record a manual payment between two participants in a non-archived group.

## Rationale

Users settle debts outside the app and need to record those payments to keep balances accurate.
