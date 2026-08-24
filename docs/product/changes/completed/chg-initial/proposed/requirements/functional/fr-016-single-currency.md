---
id: FR-016
type: functional-requirement
title: Single currency per group
status: draft
derived-from: [UC-001]
verification:
  - scenario: A group is created with a currency; all expenses in the group use that currency
provenance:
  source: src/LuSplit.Domain/Groups/Group.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST assign a single currency to each group at creation time. All expenses and payments within the group use that currency.

## Rationale

Cross-currency conversion would add complexity inconsistent with the product's simple, offline-first design.
