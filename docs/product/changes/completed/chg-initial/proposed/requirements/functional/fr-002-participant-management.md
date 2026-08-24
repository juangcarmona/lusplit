---
id: FR-002
type: functional-requirement
title: Participant management
status: draft
derived-from: [UC-002, UC-003]
verification:
  - scenario: A user adds participants to a group and assigns them to households
provenance:
  source: src/LuSplit.Application/Groups/Commands/CreateParticipantUseCase.cs, src/LuSplit.Application/Groups/Commands/CreateEconomicUnitUseCase.cs
  confidence: high
  recovered-from: observation
---

## Requirement

The product MUST allow a user to add participants to a group and organize them into households (economic units) with a designated owner.

## Rationale

Participants are the people who share expenses. Households enable dependent handling for families.
