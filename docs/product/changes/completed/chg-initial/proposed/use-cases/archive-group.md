---
id: UC-011
type: use-case
title: Archive group
status: draft
primary-actor: ACT-002
supporting-actors: []
governed-by: [BR-001]
uses-terms: [TERM-ARCHIVED, TERM-GROUP]
provenance:
  source: src/LuSplit.Application/Groups/Commands/CloseGroupUseCase.cs
  confidence: high
  recovered-from: observation
---

## Goal

Mark a group as archived, preventing further mutations.

## Trigger

User selects "Archive" from group details.

## Preconditions

The group exists and is not already archived.

## Main Flow

1. User confirms archiving.
2. System sets the group's closed flag.

## Postconditions

## Alternative Flows

- User cancels the archive confirmation.

## Failure Conditions

- The group is already archived.

## Postconditions

The group is archived. No new expenses, participants, or payments can be added. Data remains readable and exportable. Archiving is idempotent.
