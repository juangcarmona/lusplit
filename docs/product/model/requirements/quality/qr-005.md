---
id: QR-005
type: quality-requirement
title: Accessibility baseline
status: draft
quality-attribute: usability
applies-to: [UC-004, UC-007]
verification:
  - scenario: Touch targets are at least 44px; text scales responsively; contrast ratio is at least 4.5:1; dark mode is first-class
provenance:
  source: docs/product-def/UX_PRINCIPLES.md, docs/product-def/MVP_SCOPE.md
  confidence: high
  recovered-from: documentation
---

## Requirement

The product MUST meet an accessibility baseline: minimum 44px touch targets, responsive text scaling, at least 4.5:1 contrast ratio, and first-class dark mode support.

## Measurement

Automated accessibility audit confirms touch target sizes, contrast ratios, and text scaling. Dark mode renders all screens correctly.
