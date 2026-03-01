import test = require('node:test');
import assert = require('node:assert/strict');

import { CloseGroupUseCase, NotFoundError } from '../../src';
import { createFixtureContext, seedGroup } from '../fakes/fixture-context';

test('CloseGroupUseCase closes an existing group', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  const useCase = new CloseGroupUseCase(ctx.groupRepository);

  const result = await useCase.execute({ groupId: 'g1' });

  assert.equal(result.closed, true);
});

test('CloseGroupUseCase fails when group does not exist', async () => {
  const ctx = createFixtureContext();
  const useCase = new CloseGroupUseCase(ctx.groupRepository);

  await assert.rejects(() => useCase.execute({ groupId: 'g-missing' }), NotFoundError);
});
