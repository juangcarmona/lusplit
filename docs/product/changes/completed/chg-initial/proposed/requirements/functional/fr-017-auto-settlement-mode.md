---
id: FR-017
type: functional-requirement
title: Automatic settlement mode selection
status: draft
derived-from: [UC-010]
verification:
  - scenario: A group with dependents shows household-aggregated settlement; a group without dependents shows individual settlement
provenance:
  source: src/LuSplit.Application/Groups/Models/GroupOverviewExtensions.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST automatically select individual or household-aggregated settlement mode based on whether the group has dependents.

## Rationale

Users should not need to understand the distinction; the system chooses the appropriate mode.
