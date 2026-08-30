---
id: BR-003
type: business-rule
title: Split must consume full expense amount
status: draft
applies-to: [UC-004, UC-005]
provenance:
  source: src/LuSplit.Domain/Expenses/SplitEvaluator.cs, tests/LuSplit.Domain.Tests/SplitParityTests.cs
  confidence: high
  recovered-from: observation
---

## Rule

A split definition must allocate the entire expense amount. No remainder may be left unassigned after all split components are evaluated.

## Rationale

Any unassigned amount represents money that is neither the payer's credit nor any participant's debt, breaking the zero-sum invariant.

## Examples

- A $100 expense split as fixed $60 + equal remainder among 2 people → $60 + $20 + $20 = $100 ✓
- A $100 expense with only a fixed $60 component → rejected (remainder of $40 unassigned).

## Exceptions

None.
