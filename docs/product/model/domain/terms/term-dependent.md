---
id: TERM-DEPENDENT
type: domain-term
title: Dependent
status: draft
defined-in: BC-001
synonyms: [child]
provenance:
  source: src/LuSplit.Domain/Groups/EconomicUnit.cs, src/LuSplit.Domain/Payments/BalanceCalculator.cs
  confidence: medium
  recovered-from: inference
---

## Definition

A participant whose economic unit is owned by a different participant (the responsible adult). Dependents' balances are aggregated under their owner for settlement purposes. In weighted splits, dependents default to half consumption category.

## Distinguish From

- **Economic Unit Owner**: The responsible adult; the dependent's balances roll up to this person.
- **Participant**: A dependent is a participant with a specific household relationship.

## Usage

Dependents are configured during group creation. Their presence triggers household-aggregated settlement mode.
