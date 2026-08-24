---
id: UC-003
type: use-case
title: Create economic unit
status: draft
primary-actor: ACT-002
supporting-actors: []
governed-by: [BR-001]
uses-terms: [TERM-ECONOMIC-UNIT, TERM-PARTICIPANT]
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateEconomicUnitUseCase.cs
  confidence: high
  recovered-from: observation
---

## Goal

Create a household (economic unit) within a group with a designated owner participant.

## Trigger

User creates a household during group creation or from group details.

## Preconditions

The group exists and is not archived. The owner participant exists in the group.

## Main Flow

1. User selects an owner participant for the household.
2. System creates the economic unit with the owner.

## Postconditions

## Alternative Flows

- User reassigns the owner participant to a different economic unit after creation.

## Failure Conditions

- The group is archived.
- The owner participant does not exist in the group.

## Postconditions

A new economic unit exists in the group. The owner participant belongs to this unit.
