---
id: QR-002
type: quality-requirement
title: Deterministic calculations
status: draft
quality-attribute: determinism
applies-to: [UC-004, UC-008, UC-010]
verification:
  - scenario: Given identical inputs, split evaluation, balance calculation, and settlement planning produce identical results on any platform
provenance:
  source: src/LuSplit.Domain/Expenses/SplitEvaluator.cs, src/LuSplit.Domain/Payments/SettlementPlanner.cs
  confidence: high
  recovered-from: observation
---

## Requirement

All financial calculations MUST be deterministic. Given identical inputs, the results MUST be identical across platforms and runs.

## Measurement

Unit tests verify that split evaluation, balance calculation, and settlement planning produce byte-identical results given the same inputs, using lexical ordering tiebreaks.
