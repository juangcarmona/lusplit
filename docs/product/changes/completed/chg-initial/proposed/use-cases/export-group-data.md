---
id: UC-013
type: use-case
title: Export group data
status: draft
primary-actor: ACT-001
supporting-actors: []
governed-by: []
uses-terms: [TERM-GROUP]
provenance:
  source: src/LuSplit.Infrastructure/Export/GroupExporterService.cs
  confidence: high
  recovered-from: observation
---

## Goal

Export a group's data as a file for record-keeping or sharing.

## Trigger

User selects "Export" from the group timeline or archived group view.

## Preconditions

A group exists with data to export.

## Main Flow

1. User selects the export format: PDF (summary report), CSV (spreadsheet data), or JSON (full snapshot).
2. System generates the file.
3. System offers the file for sharing/saving.

## Postconditions

## Alternative Flows

- User exports an archived group's data.
- User selects JSON format for a full importable snapshot.

## Failure Conditions

- The group has no data to export.

## Postconditions

None (export does not modify group data).
