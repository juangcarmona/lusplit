---
id: TERM-SETTLEMENT-PLAN
type: domain-term
title: Settlement Plan
status: draft
defined-in: BC-001
synonyms: [settle up]
provenance:
  source: src/LuSplit.Domain/Payments/SettlementPlanner.cs
  confidence: high
  recovered-from: observation
---

## Definition

The minimum set of transfers needed to zero out all participant balances. Uses a greedy creditor-debtor matching algorithm. Can operate in individual mode (per participant) or household-aggregated mode (dependents rolled up to economic unit owner).

## Distinguish From

- **Transfer**: An individual payment; a settlement plan is a collection of suggested transfers.
- **Balance**: The net position; the settlement plan resolves balances to zero.

## Usage

Settlement plans are shown to users as "who owes whom" suggestions. Users can record individual transfers from the plan.
