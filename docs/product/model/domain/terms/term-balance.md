---
id: TERM-BALANCE
type: domain-term
title: Balance
status: draft
defined-in: BC-001
synonyms: [net amount, owe]
provenance:
  source: src/LuSplit.Domain/Payments/BalanceCalculator.cs
  confidence: high
  recovered-from: observation
---

## Definition

A participant's net financial position within a group. Positive means the participant is owed money; negative means the participant owes money. Calculated as: total paid across expenses minus total owed across splits plus total sent in transfers minus total received in transfers.

## Distinguish From

- **Settlement Plan**: The suggested transfers to zero out all balances.
- **Transfer**: An individual payment; balances are the net result of all expenses and transfers.

## Usage

Balances are the primary output shown to users. They drive the settlement plan calculation.
