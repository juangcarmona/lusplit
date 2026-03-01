import test = require('node:test');
import assert = require('node:assert/strict');

import { GetBalancesByEconomicUnitOwnerUseCase } from '../../src';
import {
  createFixtureContext,
  seedEconomicUnit,
  seedExpense,
  seedGroup,
  seedParticipant
} from '../fakes/fixture-context';

test('GetBalancesByEconomicUnitOwnerUseCase aggregates balances by owner', async () => {
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
  const useCase = new GetBalancesByEconomicUnitOwnerUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );

  const result = await useCase.execute({ groupId: 'g1' });

  assert.deepEqual(result, [
    { entityId: 'p1', amountMinor: 60 },
    { entityId: 'p2', amountMinor: -60 }
  ]);
});

test('GetBalancesByEconomicUnitOwnerUseCase validates owner/unit relationship', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p2' });
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  const useCase = new GetBalancesByEconomicUnitOwnerUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );

  await assert.rejects(() => useCase.execute({ groupId: 'g1' }));
});
