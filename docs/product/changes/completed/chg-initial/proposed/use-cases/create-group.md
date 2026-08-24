---
id: UC-001
type: use-case
title: Create group
status: draft
primary-actor: ACT-002
supporting-actors: []
governed-by: [BR-001]
uses-terms: [TERM-GROUP]
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateGroupUseCase.cs
  confidence: high
  recovered-from: observation
---

## Goal

Create a new shared-expense group with a name and currency.

## Trigger

User selects "Create group" from the home screen or group switcher.

## Preconditions

No group with the same ID exists.

## Main Flow

1. User provides a group name and selects a currency.
2. System creates the group in open (active) state.

## Postconditions

## Alternative Flows

- User selects a different currency after initial entry.

## Failure Conditions

- A group with the same ID already exists (collision).

## Postconditions

A new group exists with the given name and currency. The group is active (not archived).
