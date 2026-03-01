import test = require('node:test');
import assert = require('node:assert/strict');

import { DeleteExpenseUseCase, NotFoundError } from '../../src';
import { createFixtureContext, seedExpense, seedGroup } from '../fakes/fixture-context';

test('DeleteExpenseUseCase deletes an existing expense', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedExpense(ctx, { id: 'e1', groupId: 'g1', paidByParticipantId: 'p1' });
  const useCase = new DeleteExpenseUseCase(ctx.groupRepository, ctx.expenseRepository);

  await useCase.execute({ groupId: 'g1', expenseId: 'e1' });

  assert.equal(await ctx.expenseRepository.getById('e1'), null);
});

test('DeleteExpenseUseCase fails when expense does not exist', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  const useCase = new DeleteExpenseUseCase(ctx.groupRepository, ctx.expenseRepository);

  await assert.rejects(() => useCase.execute({ groupId: 'g1', expenseId: 'missing' }), NotFoundError);
});
