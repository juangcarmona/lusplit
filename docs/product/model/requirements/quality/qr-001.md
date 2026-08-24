---
id: QR-001
type: quality-requirement
title: Offline-first operation
status: draft
quality-attribute: portability
applies-to: [UC-001, UC-004, UC-007]
verification:
  - scenario: All core features work without any network connection; data is stored locally in SQLite
provenance:
  source: src/LuSplit.Infrastructure/Sqlite/, docs/product-def/MVP_SCOPE.md
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST function fully without a network connection. All data is stored locally.

## Measurement

All core use cases (create group, add expense, record payment, view balances) complete successfully with network disabled.
