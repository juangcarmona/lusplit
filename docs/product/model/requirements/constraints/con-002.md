---
id: CON-002
type: constraint
title: No cross-currency conversion
status: draft
provenance:
  source: src/LuSplit.Domain/Groups/Group.cs, docs/product-def/MVP_SCOPE.md
  confidence: high
  recovered-from: observation
---

## Constraint

Each group uses a single currency. The product does not perform currency conversion.

## Rationale

Currency conversion would require network access (for exchange rates) and add complexity inconsistent with the offline-first, simple design.

## Consequences

Expenses in different currencies must be tracked in separate groups. No automatic conversion between currencies.
