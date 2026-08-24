---
id: FR-018
type: functional-requirement
title: Multi-language support
status: draft
derived-from:
  - UC-001
verification:
  - scenario: A user changes the app language in settings; the UI updates accordingly
provenance:
  source: src/LuSplit.App/Features/Settings/Settings/SettingsViewModel.cs, assets/store-listing/
  confidence: medium
  recovered-from: observation
---

## Requirement

The product MUST support multiple UI languages (at least 14 locales).

## Rationale

The product serves an international audience of friends and families.
