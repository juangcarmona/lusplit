---
id: BR-006
type: business-rule
title: Percentage split must sum to exactly 100
status: draft
applies-to: [UC-004, UC-005]
provenance:
  source: src/LuSplit.Domain/Expenses/SplitEvaluator.cs, tests/LuSplit.Domain.Tests/SplitParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

In percentage-mode remainder splits, the assigned percentages must sum to exactly 100.

## Rationale

Any deviation from 100% would leave an unassigned remainder, violating the full-consumption rule.

## Examples

- Three participants at 40%, 35%, 25% = 100% ✓
- Three participants at 40%, 35%, 20% = 95% → rejected.

## Exceptions

None.
