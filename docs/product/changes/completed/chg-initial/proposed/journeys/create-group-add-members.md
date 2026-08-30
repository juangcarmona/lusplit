---
id: JRN-001
type: journey
title: Create a group and add members
status: draft
primary-actor: ACT-002
steps:
  - use-case: UC-001
  - use-case: UC-003
  - use-case: UC-002
provenance:
  source: src/LuSplit.App/Features/Groups/CreateGroup/CreateGroupViewModel.cs, src/LuSplit.Application/Groups/Commands/
  confidence: high
  recovered-from: observation
---

## Intended Outcome

A new group exists with a name, currency, and at least one participant. Household relationships are configured for any dependents.

## Entry Conditions

The user has the app installed and opens it for the first time or chooses to create a new group.

## Journey Narrative

1. The user creates a group by providing a name and selecting a currency.
2. The user creates an economic unit (household) for themselves.
3. The user adds participants, assigning each to a household. The first participant ("Me") is auto-created.
4. For participants with dependents (e.g., children), the user assigns a dependency relationship.

## Variants and Branches

- The user may skip adding dependents initially and add them later from group details.
- The user may create multiple economic units for different households.

## Completion Conditions

The group exists with a name, currency, and at least one participant assigned to an economic unit.
