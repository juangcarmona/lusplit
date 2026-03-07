# MAUI Parity Matrix (Milestone 1)

This matrix tracks parity from current TypeScript behavior to initial .NET tests.

## Domain invariants

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `packages/core/README.md` | Money uses integer minor units only. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `RejectsFractionalMinorUnits` |
| `packages/core/test/split/evaluate-split.test.ts` | Deterministic remainder allocation by ordering assumptions. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `EqualSplitUsesDeterministicLexicalOrderingForRemainder` |
| `packages/core/test/split/evaluate-split.test.ts` | FIXED + REMAINDER sequencing, EQUAL/WEIGHT/PERCENT modes, duplicate/group guards. | `apps/maui/tests/LuSplit.Domain.Tests/SplitParityTests.cs` |
| `packages/core/test/balance/calculate-balances.test.ts` | Participant balance zero-sum and economic-unit-owner aggregation guards. | `apps/maui/tests/LuSplit.Domain.Tests/BalanceParityTests.cs` |
| `packages/core/test/settlement/plan-settlement.test.ts` | Deterministic settlement transfer plan with zero-sum validation. | `apps/maui/tests/LuSplit.Domain.Tests/SettlementParityTests.cs` |

## Application boundary constraints

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `packages/application/src/usecases/**` | Commands validate required IDs and payload constraints. | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseCommandTests.cs` `CreateRejectsMissingGroupId` |
| `docs/ARCHITECTURE.md` | No floating money semantics in app flow. | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseCommandTests.cs` `CreateRejectsFractionalMinorUnits` |

## Infrastructure contract constraints

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `docs/EXPORT_FORMAT.md` + `packages/infra-local/src/snapshot.ts` | Snapshot schema contract has `version: 1`. | `apps/maui/tests/LuSplit.Infrastructure.Tests/SnapshotContractTests.cs` |

## Canonical fixture

- Fixture file: `apps/maui/tests/LuSplit.Domain.Tests/Fixtures/foundation-parity.fixture.json`
- Fixture purpose: lock deterministic split expectations (`10 -> {a:4,b:3,c:3}`) to keep migration checks stable.

## Next parity expansions (Milestone 2)

- Add TS parity for explicit custom weight edge cases (`CUSTOM` category + precision constraints).
- Add additional tie-breaker/ordering cases for weighted and percent remainder allocation.
- Add negative/boundary invariant cases for split components (e.g., fixed exceeds remainder).
- Extend parity coverage from core domain into application use-case flows in Milestone 2 continuation.
