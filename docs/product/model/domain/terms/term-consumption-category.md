---
id: TERM-CONSUMPTION-CATEGORY
type: domain-term
title: Consumption Category
status: draft
defined-in: BC-001
synonyms: []
provenance:
  source: src/LuSplit.Domain/Groups/ConsumptionCategory.cs, src/LuSplit.Domain/Expenses/SplitEvaluator.cs
  confidence: high
  recovered-from: observation
---

## Definition

How much of a share a participant gets in weighted splits. Three values: Full (weight 1.0), Half (weight 0.5, typically for children), or Custom (user-specified weight, must be > 0).

## Distinguish From

- **Split Definition**: The overall division rules; consumption category is a participant property that influences weighted splits.
- **Fixed Split**: An explicit amount; consumption category determines proportional shares.

## Usage

Set per participant. Used by the weight-mode remainder split to determine each participant's proportional share.
