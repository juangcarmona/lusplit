---
id: UC-012
type: use-case
title: View archived group
status: draft
primary-actor: ACT-001
supporting-actors: []
governed-by: [BR-001]
uses-terms: [TERM-ARCHIVED, TERM-GROUP]
provenance:
  source: src/LuSplit.App/Features/Groups/ArchivedGroupView/ArchivedGroupViewModel.cs
  confidence: high
  recovered-from: observation
---

## Goal

View an archived group's data (balances, expenses, events) in read-only mode.

## Trigger

User selects an archived group from the archived groups list.

## Preconditions

The group exists and is archived.

## Main Flow

1. System loads the group's data in read-only mode.
2. User can view balances, expenses, and events.
3. User can export the group data.

## Postconditions

## Alternative Flows

- User exports data from the archived group view.

## Failure Conditions

- The group is not archived (should use the active group view instead).

## Postconditions

None (read-only).
