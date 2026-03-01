import test = require('node:test');
import assert = require('node:assert/strict');

import { EditExpenseUseCase, NotFoundError } from '../../src';
import { createFixtureContext, seedExpense, seedGroup, seedParticipant } from '../fakes/fixture-context';

test('EditExpenseUseCase updates expense fields and validates split via core', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  await seedExpense(ctx, {
    id: 'e1',
    groupId: 'g1',
    paidByParticipantId: 'p1',
    amountMinor: 100,
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1'], mode: 'EQUAL' }] as any }
  });
  const useCase = new EditExpenseUseCase(ctx.groupRepository, ctx.participantRepository, ctx.expenseRepository);

  const result = await useCase.execute({
    groupId: 'g1',
    expenseId: 'e1',
    title: 'Edited',
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1', 'p2'], mode: 'EQUAL' }] }
  });

  assert.equal(result.title, 'Edited');
  assert.equal(result.splitDefinition.components[0].type, 'REMAINDER');
});

test('EditExpenseUseCase fails for unknown expense', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  const useCase = new EditExpenseUseCase(ctx.groupRepository, ctx.participantRepository, ctx.expenseRepository);

  await assert.rejects(() => useCase.execute({ groupId: 'g1', expenseId: 'missing' }), NotFoundError);
});
