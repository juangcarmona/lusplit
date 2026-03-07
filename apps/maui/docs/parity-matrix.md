# MAUI Parity Matrix (Milestone 1)

This matrix tracks parity from current TypeScript behavior to initial .NET tests.

## Domain invariants

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `packages/core/README.md` | Money uses integer minor units only. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `RejectsFractionalMinorUnits` |
| `packages/core/test/split/evaluate-split.test.ts` | Deterministic remainder allocation by ordering assumptions. | `apps/maui/tests/LuSplit.Domain.Tests/FoundationParityTests.cs` `EqualSplitUsesDeterministicLexicalOrderingForRemainder` |
| `packages/core/test/split/evaluate-split.test.ts` | FIXED + REMAINDER sequencing, EQUAL/WEIGHT/PERCENT modes, duplicate/group guards. | `apps/maui/tests/LuSplit.Domain.Tests/SplitParityTests.cs` |
| `packages/core/src/split/index.ts` | Edge constraints: fixed overflow rejection, invalid percent handling, deterministic tie-breakers, custom-weight precision (<= 6 decimals). | `apps/maui/tests/LuSplit.Domain.Tests/SplitParityTests.cs` |
| `packages/core/test/balance/calculate-balances.test.ts` | Participant balance zero-sum and economic-unit-owner aggregation guards. | `apps/maui/tests/LuSplit.Domain.Tests/BalanceParityTests.cs` |
| `packages/core/test/settlement/plan-settlement.test.ts` | Deterministic settlement transfer plan with zero-sum validation. | `apps/maui/tests/LuSplit.Domain.Tests/SettlementParityTests.cs` |

## Application boundary constraints

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `packages/application/src/usecases/**` | Commands validate required IDs and payload constraints. | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseCommandTests.cs` `CreateRejectsMissingGroupId` |
| `docs/ARCHITECTURE.md` | No floating money semantics in app flow. | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseCommandTests.cs` `CreateRejectsFractionalMinorUnits` |
| `packages/application/test/unit/get-balances-by-participant.usecase.test.ts` | Query returns sorted participant balances and fails for missing group. | `apps/maui/tests/LuSplit.Application.Tests/GetBalancesByParticipantUseCaseTests.cs` |
| `packages/application/test/unit/get-balances-by-economic-unit-owner.usecase.test.ts` | Query aggregates balances by owner and validates owner/unit consistency. | `apps/maui/tests/LuSplit.Application.Tests/GetBalancesByEconomicUnitOwnerUseCaseTests.cs` |
| `packages/application/test/unit/get-settlement-plan.usecase.test.ts` | Query returns deterministic settlement plan in participant and owner modes; missing group yields not-found error. | `apps/maui/tests/LuSplit.Application.Tests/GetSettlementPlanUseCaseTests.cs` |
| `packages/application/test/unit/add-expense.usecase.test.ts` | Command stores expense with provided split definition and validates payer membership. | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseUseCaseTests.cs` |
| `packages/application/test/unit/edit-expense.usecase.test.ts` | Command updates expense fields, revalidates split, and errors for unknown expense. | `apps/maui/tests/LuSplit.Application.Tests/EditExpenseUseCaseTests.cs` |
| `packages/application/test/unit/delete-expense.usecase.test.ts` | Command deletes existing expense and errors for missing expense. | `apps/maui/tests/LuSplit.Application.Tests/DeleteExpenseUseCaseTests.cs` |
| `packages/application/test/unit/create-group.usecase.test.ts` | Command creates open group and validates currency input. | `apps/maui/tests/LuSplit.Application.Tests/CreateGroupUseCaseTests.cs` |
| `packages/application/test/unit/create-participant.usecase.test.ts` | Command validates CUSTOM weight, aligns first participant id with economic-unit owner, then generates unique IDs. | `apps/maui/tests/LuSplit.Application.Tests/CreateParticipantUseCaseTests.cs` |
| `packages/application/test/unit/create-economic-unit.usecase.test.ts` | Command creates unit in open group and rejects closed groups. | `apps/maui/tests/LuSplit.Application.Tests/CreateEconomicUnitUseCaseTests.cs` |
| `packages/application/test/unit/add-manual-transfer.usecase.test.ts` | Command stores MANUAL transfer and validates distinct participants in group. | `apps/maui/tests/LuSplit.Application.Tests/AddManualTransferUseCaseTests.cs` |
| `packages/application/test/unit/close-group.usecase.test.ts` | Command closes an existing group and errors for missing group. | `apps/maui/tests/LuSplit.Application.Tests/CloseGroupUseCaseTests.cs` |
| `packages/application/src/usecases/common.ts` | Validation and not-found messages use lower-camel field names (for example, `groupId is required`). | `apps/maui/tests/LuSplit.Application.Tests/AddExpenseUseCaseTests.cs`, `apps/maui/tests/LuSplit.Application.Tests/EditExpenseUseCaseTests.cs`, `apps/maui/tests/LuSplit.Application.Tests/DeleteExpenseUseCaseTests.cs`, `apps/maui/tests/LuSplit.Application.Tests/CloseGroupUseCaseTests.cs` |

## Infrastructure contract constraints

| Current source | Behavior in current implementation | .NET baseline test |
|---|---|---|
| `docs/EXPORT_FORMAT.md` + `packages/infra-local/src/snapshot.ts` | Snapshot schema contract has `version: 1`. | `apps/maui/tests/LuSplit.Infrastructure.Tests/SnapshotContractTests.cs` |

## Canonical fixture

- Fixture file: `apps/maui/tests/LuSplit.Domain.Tests/Fixtures/foundation-parity.fixture.json`
- Fixture purpose: lock deterministic split expectations (`10 -> {a:4,b:3,c:3}`) to keep migration checks stable.

## Next parity expansions (Milestone 2)

- Port additional split edge cases from TS that use explicit `weights` maps in `REMAINDER/WEIGHT` mode.
- Add parity tests for application query sorting/stability across larger datasets and multi-expense fixtures.
- Port remaining command parity not yet covered beyond `add/edit/delete/close-group` (if new TS command tests are added).
- Continue tightening error-message parity for any command/query tests still missing explicit message assertions.
