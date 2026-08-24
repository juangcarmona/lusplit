---
id: QR-003
type: quality-requirement
title: Integer money arithmetic
status: draft
quality-attribute: determinism
applies-to: [UC-004, UC-007, UC-008]
verification:
  - scenario: All monetary values are stored and calculated as integer minor units; no floating-point arithmetic is used for money
provenance:
  source: src/LuSplit.Domain/Shared/MoneyAmount.cs
  confidence: high
  recovered-from: observation
---

## Requirement

All monetary values MUST be stored and calculated as integer minor currency units. No floating-point arithmetic MUST be used for money.

## Measurement

Code review confirms all money fields use `long` (int64) minor units. No `float`, `double`, or `decimal` types are used for monetary calculations.
