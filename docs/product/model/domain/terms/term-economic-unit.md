---
id: TERM-ECONOMIC-UNIT
type: domain-term
title: Economic Unit
status: draft
defined-in: BC-001
synonyms: [household, family unit]
provenance:
  source: src/LuSplit.Domain/Groups/EconomicUnit.cs, src/LuSplit.Domain/Payments/BalanceCalculator.cs
  confidence: high
  recovered-from: observation
---

## Definition

A household or family unit within a group. Has one owner participant. Dependents (e.g., children) belong to their guardian's economic unit. When settling, dependent balances are aggregated under the owner.

## Distinguish From

- **Group**: The overall workspace; a group contains multiple economic units.
- **Participant**: An individual person; an economic unit groups participants.

## Usage

Economic units determine how balances are aggregated for settlement. When dependents exist, settlement uses household-aggregated mode instead of individual mode.
