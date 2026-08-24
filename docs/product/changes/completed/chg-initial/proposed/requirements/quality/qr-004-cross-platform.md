---
id: QR-004
type: quality-requirement
title: Cross-platform support
status: draft
quality-attribute: portability
applies-to: [UC-001, UC-004, UC-007]
verification:
  - scenario: The app runs on Android, iOS, Windows, and macOS
provenance:
  source: src/LuSplit.App/Platforms/, src/LuSplit.App/LuSplit.App.csproj
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST run on Android, iOS, Windows, and macOS platforms.

## Measurement

The app builds and launches on all four platforms with core features functional.
