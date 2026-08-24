---
id: TERM-GROUP
type: domain-term
title: Group
status: draft
defined-in: BC-001
synonyms: [trip, workspace]
provenance:
  source: src/LuSplit.Domain/Groups/Group.cs
  confidence: high
  recovered-from: observation
---

## Definition

A shared-expense workspace with its own currency. The top-level container for all participants, expenses, payments, and balances within a specific context (trip, household, event).

## Distinguish From

- **Economic Unit**: A household within a group, not the group itself.
- **Archived Group**: A group in closed state; same entity, different lifecycle stage.

## Usage

Groups are created by the Group Creator actor. All expenses, participants, and settlements are scoped to a single group. A user may belong to multiple groups.
