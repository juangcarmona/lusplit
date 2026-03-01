import test = require('node:test');
import assert = require('node:assert/strict');

import { CreateEconomicUnitUseCase, ValidationError } from '../../src';
import { createFixtureContext, seedGroup } from '../fakes/fixture-context';

test('CreateEconomicUnitUseCase creates economic unit in open group', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  const useCase = new CreateEconomicUnitUseCase(ctx.groupRepository, ctx.economicUnitRepository, ctx.idGenerator);

  const result = await useCase.execute({ groupId: 'g1', ownerParticipantId: 'p1', name: 'Household' });

  assert.equal(result.id, 'id-1');
  assert.equal(result.groupId, 'g1');
});

test('CreateEconomicUnitUseCase fails when group is closed', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1', true);
  const useCase = new CreateEconomicUnitUseCase(ctx.groupRepository, ctx.economicUnitRepository, ctx.idGenerator);

  await assert.rejects(() => useCase.execute({ groupId: 'g1', ownerParticipantId: 'p1' }), ValidationError);
});
