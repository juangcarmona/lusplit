import test = require('node:test');
import assert = require('node:assert/strict');

import { CreateParticipantUseCase, ValidationError } from '../../src';
import { createFixtureContext, seedEconomicUnit, seedGroup } from '../fakes/fixture-context';

test('CreateParticipantUseCase creates participant in existing economic unit', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' });
  const useCase = new CreateParticipantUseCase(
    ctx.groupRepository,
    ctx.economicUnitRepository,
    ctx.participantRepository,
    ctx.idGenerator
  );

  const result = await useCase.execute({
    groupId: 'g1',
    economicUnitId: 'u1',
    name: 'Alice',
    consumptionCategory: 'FULL'
  });

  assert.equal(result.id, 'p1');
  assert.equal(result.name, 'Alice');
});

test('CreateParticipantUseCase generates a new id after owner participant exists in unit', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' });
  const useCase = new CreateParticipantUseCase(
    ctx.groupRepository,
    ctx.economicUnitRepository,
    ctx.participantRepository,
    ctx.idGenerator
  );

  const owner = await useCase.execute({
    groupId: 'g1',
    economicUnitId: 'u1',
    name: 'Alice',
    consumptionCategory: 'FULL'
  });
  const another = await useCase.execute({
    groupId: 'g1',
    economicUnitId: 'u1',
    name: 'Bob',
    consumptionCategory: 'FULL'
  });

  assert.equal(owner.id, 'p1');
  assert.equal(another.id, 'id-1');
});

test('CreateParticipantUseCase validates custom weight for CUSTOM category', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedEconomicUnit(ctx, { id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' });
  const useCase = new CreateParticipantUseCase(
    ctx.groupRepository,
    ctx.economicUnitRepository,
    ctx.participantRepository,
    ctx.idGenerator
  );

  await assert.rejects(
    () =>
      useCase.execute({
        groupId: 'g1',
        economicUnitId: 'u1',
        name: 'Alice',
        consumptionCategory: 'CUSTOM'
      }),
    ValidationError
  );
});
