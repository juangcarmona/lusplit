# MAUI Parity Matrix (Milestone 1)

This matrix tracks parity from current TypeScript behavior to initial .NET tests.

## Domain invariants

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `packages/core/README.md` | Money uses integer minor units only. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `RejectsFractionalMinorUnits` |
| `packages/core/test/split/evaluate-split.test.ts` | Deterministic remainder allocation by ordering assumptions. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `EqualSplitUsesDeterministicLexicalOrderingForRemainder` |
| `packages/core/test/split/evaluate-split.test.ts` | Split output must consume full amount. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `EqualSplitIsZeroSumToExpenseAmount` |

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

- Port split modes: FIXED, REMAINDER/EQUAL, REMAINDER/WEIGHT, REMAINDER/PERCENT.
- Port participant/economic-unit guards from `packages/core/src/entities`.
- Port balance and settlement algorithms and their full test scenarios.
- Add deterministic collection-order tests matching current TypeScript case-by-case behavior.
