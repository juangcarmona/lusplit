import test = require('node:test');
import assert = require('node:assert/strict');

import { CreateGroupUseCase, ValidationError } from '../../src';
import { createFixtureContext } from '../fakes/fixture-context';

test('CreateGroupUseCase creates an open group', async () => {
  const ctx = createFixtureContext();
  const useCase = new CreateGroupUseCase(ctx.groupRepository, ctx.idGenerator);

  const created = await useCase.execute({ currency: 'EUR' });

  assert.equal(created.currency, 'EUR');
  assert.equal(created.closed, false);
  assert.equal(created.id, 'id-1');
});

test('CreateGroupUseCase validates currency', async () => {
  const ctx = createFixtureContext();
  const useCase = new CreateGroupUseCase(ctx.groupRepository, ctx.idGenerator);

  await assert.rejects(() => useCase.execute({ currency: '' }), ValidationError);
});
