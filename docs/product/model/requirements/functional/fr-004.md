---
id: FR-004
type: functional-requirement
title: Split mode support
status: draft
derived-from: [UC-004]
verification:
  - scenario: A user splits an expense equally, by weight, by percentage, or with fixed amounts per participant
provenance:
  source: src/LuSplit.Domain/Expenses/SplitContracts.cs, src/LuSplit.Domain/Expenses/SplitEvaluator.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST support equal, weighted (by consumption category), percentage, and fixed-amount split modes for dividing expenses among participants.

## Rationale

Different sharing situations require different split methods (e.g., equal for dinner, weighted for families with children).
