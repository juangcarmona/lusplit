---
id: TERM-SPLIT-DEFINITION
type: domain-term
title: Split Definition
status: draft
defined-in: BC-001
synonyms: [split]
provenance:
  source: src/LuSplit.Domain/Expenses/SplitContracts.cs, src/LuSplit.Domain/Expenses/SplitEvaluator.cs
  confidence: high
  recovered-from: observation
---

## Definition

The rules for dividing an expense amount among participants. Composed of ordered components: fixed components (explicit amounts per participant) are applied first, then remainder components distribute what is left. Remainder modes: equal, weighted (by consumption category), or percentage.

## Distinguish From

- **Expense**: The cost being divided; the split definition is how it is divided.
- **Consumption Category**: A participant property that influences weighted splits.

## Usage

Every expense has exactly one split definition. The split evaluator enforces that the definition consumes the full expense amount.
