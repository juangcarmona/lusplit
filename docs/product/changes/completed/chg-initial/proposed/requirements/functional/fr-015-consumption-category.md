---
id: FR-015
type: functional-requirement
title: Consumption category for weighted splits
status: draft
derived-from: [UC-004]
verification:
  - scenario: A participant with Half consumption category receives half the share of a Full participant in weighted splits
provenance:
  source: src/LuSplit.Domain/Groups/ConsumptionCategory.cs, src/LuSplit.Domain/Expenses/SplitEvaluator.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST support consumption categories (Full, Half, Custom) that determine a participant's proportional share in weighted splits.

## Rationale

Children and other dependents typically consume half portions; custom weights handle special cases.
