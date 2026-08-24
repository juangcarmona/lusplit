---
id: BR-007
type: business-rule
title: Transfer sender and receiver must differ
status: draft
applies-to: [UC-007]
provenance:
  source: src/LuSplit.Domain/Payments/Transfer.cs, src/LuSplit.Application/Payments/Commands/AddManualTransferUseCase.cs
  confidence: high
  recovered-from: observation
---

## Rule

A transfer's sender and receiver must be different participants.

## Rationale

A self-payment has no financial meaning and would not change any balance.

## Examples

- Recording a payment from Alice to Bob ✓
- Recording a payment from Alice to Alice → rejected.

## Exceptions

None.
