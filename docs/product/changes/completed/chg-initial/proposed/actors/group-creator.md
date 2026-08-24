---
id: ACT-002
type: actor
title: Group Creator
status: draft
actor-kind: human
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateGroupUseCase.cs, src/LuSplit.App/Features/Groups/CreateGroup/CreateGroupViewModel.cs
  confidence: high
  recovered-from: observation
---

## Purpose

A person who creates a new group and configures its initial membership and currency.

## Goals

- Set up a shared-expense workspace for a trip, household, or event
- Add participants and define household relationships
- Choose the group's currency

## Responsibilities

- Creates the group with a name and currency
- Adds participants and assigns them to households
- Can archive the group when no longer active

## Boundaries

Does not have privileged access to other members' data. Cannot modify other groups.
