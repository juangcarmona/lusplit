---
id: FR-010
type: functional-requirement
title: Group data export
status: draft
derived-from: [UC-013]
verification:
  - scenario: A user exports a group as PDF, CSV, or JSON
provenance:
  source: src/LuSplit.Infrastructure/Export/GroupExporterService.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to export group data as PDF (summary report), CSV (spreadsheet data), or JSON (full snapshot).

## Rationale

Users need to share expense summaries or keep records outside the app.
