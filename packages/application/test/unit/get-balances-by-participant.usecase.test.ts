import test = require('node:test');
import assert = require('node:assert/strict');

import { GetBalancesByParticipantUseCase, NotFoundError } from '../../src';
import { createFixtureContext, seedExpense, seedGroup, seedParticipant } from '../fakes/fixture-context';

test('GetBalancesByParticipantUseCase returns zero-sum balances', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  await seedExpense(ctx, {
    id: 'e1',
    groupId: 'g1',
    paidByParticipantId: 'p1',
    amountMinor: 100,
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1', 'p2'], mode: 'EQUAL' }] as any }
  });
  const useCase = new GetBalancesByParticipantUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository
  );

  const result = await useCase.execute({ groupId: 'g1' });

  assert.deepEqual(result, [
    { entityId: 'p1', amountMinor: 50 },
    { entityId: 'p2', amountMinor: -50 }
  ]);
});

test('GetBalancesByParticipantUseCase fails for unknown group', async () => {
  const ctx = createFixtureContext();
  const useCase = new GetBalancesByParticipantUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository
  );

  await assert.rejects(() => useCase.execute({ groupId: 'missing' }), NotFoundError);
});
