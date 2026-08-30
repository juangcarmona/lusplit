---
id: TERM-ARCHIVED
type: domain-term
title: Archived
status: draft
defined-in: BC-001
synonyms: [closed]
provenance:
  source: src/LuSplit.Domain/Groups/Group.cs, src/LuSplit.Application/Groups/Commands/CloseGroupUseCase.cs
  confidence: high
  recovered-from: observation
---

## Definition

A group state where no mutations are allowed (no new expenses, participants, or payments). Data remains readable and exportable. Archiving is idempotent.

## Distinguish From

- **Deleted**: Archived groups are not deleted; their data is preserved.
- **Active**: The default group state where mutations are allowed.

## Usage

Groups are archived by the Group Creator. Archived groups appear in a separate list and are view-only.
