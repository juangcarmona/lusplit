---
id: FR-011
type: functional-requirement
title: Group snapshot backup and restore
status: draft
derived-from: [UC-013]
verification:
  - scenario: A user exports a group snapshot as JSON and imports it on another device
provenance:
  source: src/LuSplit.Infrastructure/Snapshot/SnapshotService.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to back up and restore an entire group via a versioned JSON snapshot.

## Rationale

Offline-first apps need a data portability mechanism for device transfers and backups.
