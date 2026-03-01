import test = require('node:test');
import assert = require('node:assert/strict');

import { AddExpenseUseCase, ValidationError } from '../../src';
import { createFixtureContext, seedGroup, seedParticipant } from '../fakes/fixture-context';

test('AddExpenseUseCase stores expense with provided split definition', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  const useCase = new AddExpenseUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository,
    ctx.idGenerator,
    ctx.clock
  );

  const result = await useCase.execute({
    groupId: 'g1',
    title: 'Dinner',
    paidByParticipantId: 'p1',
    amountMinor: 100,
    splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1', 'p2'], mode: 'EQUAL' }] }
  });

  assert.equal(result.id, 'id-1');
  assert.equal(result.splitDefinition.components[0].type, 'REMAINDER');
});

test('AddExpenseUseCase validates payer in group', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  const useCase = new AddExpenseUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository,
    ctx.idGenerator,
    ctx.clock
  );

  await assert.rejects(
    () =>
      useCase.execute({
        groupId: 'g1',
        title: 'Dinner',
        paidByParticipantId: 'p2',
        amountMinor: 100,
        splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p1'], mode: 'EQUAL' }] }
      }),
    ValidationError
  );
});
