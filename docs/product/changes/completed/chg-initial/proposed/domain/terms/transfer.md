---
id: TERM-TRANSFER
type: domain-term
title: Transfer
status: draft
defined-in: BC-001
synonyms: [payment]
provenance:
  source: src/LuSplit.Domain/Payments/Transfer.cs
  confidence: high
  recovered-from: observation
---

## Definition

A recorded payment from one participant to another. Can be manual (user-recorded) or generated (system-suggested as part of a settlement plan). Reduces the sender's debt and increases the receiver's credit in balance calculations.

## Distinguish From

- **Expense**: A shared cost; transfers are about settling debts, not spending.
- **Settlement Plan**: A suggested set of transfers; a transfer is one payment.

## Usage

Transfers are recorded by users or suggested by the settlement planner. They are factored into balance calculations alongside expenses.
