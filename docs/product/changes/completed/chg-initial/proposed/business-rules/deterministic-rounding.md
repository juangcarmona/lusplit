---
id: BR-004
type: business-rule
title: Deterministic rounding for penny-precision splits
status: draft
applies-to: [UC-004, UC-005]
provenance:
  source: src/LuSplit.Domain/Expenses/SplitEvaluator.cs (AllocateByWeights), tests/LuSplit.Domain.Tests/SplitParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

When equal, weighted, or percentage splits produce a remainder of minor units (e.g., 10 cents ÷ 3), leftover units are distributed one-by-one to participants sorted by descending remainder, then by ascending participant ID (lexical tiebreak). This ensures reproducible results across platforms.

## Rationale

Non-deterministic rounding would produce different balances on different devices, undermining trust in the calculations.

## Examples

- $0.10 split equally among 3 people: 3¢, 3¢, 4¢ (the extra cent goes to the participant with the lowest ID).

## Exceptions

None.
