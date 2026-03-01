import test = require('node:test');
import assert = require('node:assert/strict');

import { GetGroupOverviewUseCase, NotFoundError } from '../../src';
import {
  createFixtureContext,
  seedEconomicUnit,
  seedExpense,
  seedGroup,
  seedParticipant,
  seedTransfer
} from '../fakes/fixture-context';

test('GetGroupOverviewUseCase returns composed view model', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' });
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u1' });
  await seedExpense(ctx, {
    id: 'e1',
    groupId: 'g1',
    paidByParticipantId: 'p1',
    amountMinor: 100,
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1'], mode: 'EQUAL' }] as any }
  });
  await seedTransfer(ctx, { id: 't1', groupId: 'g1', fromParticipantId: 'p2', toParticipantId: 'p1' });

  const useCase = new GetGroupOverviewUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository,
    ctx.transferRepository
  );

  const result = await useCase.execute({ groupId: 'g1' });

  assert.equal(result.group.id, 'g1');
  assert.equal(result.summary.expenseCount, 1);
  assert.equal(result.summary.transferCount, 1);
  assert.equal(result.settlementByParticipant.mode, 'PARTICIPANT');
});

test('GetGroupOverviewUseCase fails for missing group', async () => {
  const ctx = createFixtureContext();
  const useCase = new GetGroupOverviewUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository,
    ctx.transferRepository
  );

  await assert.rejects(() => useCase.execute({ groupId: 'missing' }), NotFoundError);
});
