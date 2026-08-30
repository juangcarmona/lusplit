---
id: JRN-004
type: journey
title: Record a payment between members
status: draft
primary-actor: ACT-001
steps:
  - use-case: UC-007
provenance:
  source: src/LuSplit.App/Features/Payments/RecordPayment/RecordPaymentViewModel.cs, src/LuSplit.Application/Payments/Commands/AddManualTransferUseCase.cs
  confidence: high
  recovered-from: observation
---

## Intended Outcome

A payment between two participants is recorded, updating both participants' balances.

## Entry Conditions

A group exists with at least two participants. The group is not archived.

## Journey Narrative

1. The user opens the record payment form (optionally pre-filled from a settlement suggestion).
2. The user selects who is paying and who is receiving.
3. The user enters the amount.
4. The user saves the payment.

## Variants and Branches

- The user may record a payment that does not match any settlement suggestion (partial payment, different amount).

## Completion Conditions

A payment between two participants is recorded and both participants' balances are updated.
