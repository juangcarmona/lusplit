---
id: TERM-PARTICIPANT
type: domain-term
title: Participant
status: draft
defined-in: BC-001
synonyms: [member, person]
provenance:
  source: src/LuSplit.Domain/Groups/Participant.cs
  confidence: high
  recovered-from: observation
---

## Definition

A person who is part of a group and can pay expenses, owe shares, and make or receive payments. Each participant belongs to exactly one economic unit within the group.

## Distinguish From

- **Economic Unit**: A household that may contain multiple participants.
- **Dependent**: A participant whose economic unit is owned by another participant.

## Usage

Participants are added to groups by the Group Creator. They appear as payers in expenses, as senders/receivers in transfers, and as entries in balance calculations.
