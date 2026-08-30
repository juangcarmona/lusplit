---
id: JRN-005
type: journey
title: Export group expenses
status: draft
primary-actor: ACT-001
steps:
  - use-case: UC-013
provenance:
  source: src/LuSplit.Infrastructure/Export/, src/LuSplit.App/Features/Groups/GroupTimeline/GroupViewModel.cs
  confidence: high
  recovered-from: observation
---

## Intended Outcome

The user has a file (PDF, CSV, or JSON) containing the group's expense data for record-keeping or sharing.

## Entry Conditions

A group exists with at least one expense or participant.

## Journey Narrative

1. The user chooses to export from the group timeline or details screen.
2. The user selects the export format (PDF, CSV, or JSON).
3. The system generates the file and offers it for sharing/saving.

## Variants and Branches

- Archived groups can also be exported.
- JSON export includes the full snapshot (importable).

## Completion Conditions

The user has received a file in the chosen format containing the group's expense data.
