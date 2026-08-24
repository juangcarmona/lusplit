---
id: CHG-INITIAL
type: product-change
title: Initial product definition recovered from existing codebase
status: applied
base-revision: '52deb88'
operations:
  add:
    - BC-001
    - ACT-001
    - ACT-002
    - TERM-GROUP
    - TERM-PARTICIPANT
    - TERM-ECONOMIC-UNIT
    - TERM-EXPENSE
    - TERM-SPLIT-DEFINITION
    - TERM-TRANSFER
    - TERM-BALANCE
    - TERM-SETTLEMENT-PLAN
    - TERM-MINOR-UNITS
    - TERM-ARCHIVED
    - TERM-DEPENDENT
    - TERM-CONSUMPTION-CATEGORY
    - JRN-001
    - JRN-002
    - JRN-003
    - JRN-004
    - JRN-005
    - UC-001
    - UC-002
    - UC-003
    - UC-004
    - UC-005
    - UC-006
    - UC-007
    - UC-008
    - UC-009
    - UC-010
    - UC-011
    - UC-012
    - UC-013
    - BR-001
    - BR-002
    - BR-003
    - BR-004
    - BR-005
    - BR-006
    - BR-007
    - BR-008
    - BR-009
    - BR-010
    - FR-001
    - FR-002
    - FR-003
    - FR-004
    - FR-005
    - FR-006
    - FR-007
    - FR-008
    - FR-009
    - FR-010
    - FR-011
    - FR-012
    - FR-013
    - FR-014
    - FR-015
    - FR-016
    - FR-017
    - FR-018
    - QR-001
    - QR-002
    - QR-003
    - QR-004
    - QR-005
    - CON-001
    - CON-002
    - CON-003
  modify: []
  remove: []
---

## Problem

LuSplit has shipped behaviour, users, and accumulated product decisions, but no canonical product definition. Product knowledge is distributed across source code, tests, and informal documentation.

## Intended Product Outcome

A validated product definition that captures what LuSplit does today: actors, journeys, use cases, business rules, domain terms, bounded contexts, functional requirements, quality requirements, and constraints — each with provenance tracing back to the evidence that produced it.

## Rationale

Recovery establishes a baseline from which future product changes can be proposed, validated, and tracked through the PDaC lifecycle. Partial-but-validated beats complete-but-unreviewed.

## Affected Product Areas

All product areas: group management, expense tracking, payment and settlement, export and backup, user preferences.

## Open Questions

- Should the "User Preferences" bounded context be elevated from its current UI-only status, or remain implicit?
- Are there additional actors beyond Group Member and Group Creator (e.g., a "Dependent" actor)?

## Product Acceptance

A human who understands LuSplit reviews each candidate artifact, confirms or corrects it, and accepts the change into the baseline.

## Out of Scope

Implementation design, code architecture, technology choices, and any future features not yet present in the codebase.
