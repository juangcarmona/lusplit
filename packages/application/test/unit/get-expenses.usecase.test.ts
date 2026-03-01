import test = require('node:test');
import assert = require('node:assert/strict');

import { GetExpensesUseCase, NotFoundError } from '../../src';
import { createFixtureContext, seedExpense, seedGroup } from '../fakes/fixture-context';

test('GetExpensesUseCase returns mapped expenses', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedExpense(ctx, { id: 'e1', groupId: 'g1', paidByParticipantId: 'p1', title: 'A' });
  await seedExpense(ctx, { id: 'e2', groupId: 'g1', paidByParticipantId: 'p1', title: 'B' });
  const useCase = new GetExpensesUseCase(ctx.groupRepository, ctx.expenseRepository);

  const result = await useCase.execute({ groupId: 'g1' });

  assert.equal(result.length, 2);
  assert.equal(result[0].id, 'e1');
});

test('GetExpensesUseCase fails for unknown group', async () => {
  const ctx = createFixtureContext();
  const useCase = new GetExpensesUseCase(ctx.groupRepository, ctx.expenseRepository);

  await assert.rejects(() => useCase.execute({ groupId: 'missing' }), NotFoundError);
});
