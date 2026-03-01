import test = require('node:test');
import assert = require('node:assert/strict');

import { GetSettlementPlanUseCase } from '../../src';
import {
  createFixtureContext,
  seedEconomicUnit,
  seedExpense,
  seedGroup,
  seedParticipant
} from '../fakes/fixture-context';

test('GetSettlementPlanUseCase returns deterministic participant and owner mode plans', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' });
  await seedEconomicUnit(ctx, { id: 'u2', groupId: 'g1', ownerParticipantId: 'p2' });
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  await seedParticipant(ctx, { id: 'p3', groupId: 'g1', economicUnitId: 'u2' });
  await seedExpense(ctx, {
    id: 'e1',
    groupId: 'g1',
    paidByParticipantId: 'p1',
    amountMinor: 90,
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1', 'p2', 'p3'], mode: 'EQUAL' }] as any }
  });

  const useCase = new GetSettlementPlanUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );

  const participantPlan = await useCase.execute({ groupId: 'g1', mode: 'PARTICIPANT' });
  const ownerPlan = await useCase.execute({ groupId: 'g1', mode: 'ECONOMIC_UNIT_OWNER' });

  assert.deepEqual(participantPlan.transfers, [
    { fromParticipantId: 'p2', toParticipantId: 'p1', amountMinor: 30 },
    { fromParticipantId: 'p3', toParticipantId: 'p1', amountMinor: 30 }
  ]);
  assert.deepEqual(ownerPlan.transfers, [{ fromParticipantId: 'p2', toParticipantId: 'p1', amountMinor: 60 }]);
});

test('GetSettlementPlanUseCase errors for invalid group', async () => {
  const ctx = createFixtureContext();
  const useCase = new GetSettlementPlanUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );

  await assert.rejects(() => useCase.execute({ groupId: 'missing', mode: 'PARTICIPANT' }));
});
