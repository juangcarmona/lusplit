---
id: TERM-MINOR-UNITS
type: domain-term
title: Minor Units
status: draft
defined-in: BC-001
synonyms: []
provenance:
  source: src/LuSplit.Domain/Shared/MoneyAmount.cs
  confidence: high
  recovered-from: observation
---

## Definition

The smallest currency unit (e.g., cents for USD, pence for GBP). All monetary values in the system are stored and calculated as integer minor units to avoid floating-point precision errors.

## Distinguish From

- **Amount**: The user-facing value (e.g., $10.50); minor units are the internal representation (1050).

## Usage

All expense amounts, transfer amounts, and balance calculations use minor units. The split evaluator distributes leftover minor units deterministically.
