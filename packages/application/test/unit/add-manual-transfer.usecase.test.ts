import test = require('node:test');
import assert = require('node:assert/strict');

import { AddManualTransferUseCase, ValidationError } from '../../src';
import { createFixtureContext, seedGroup, seedParticipant } from '../fakes/fixture-context';

test('AddManualTransferUseCase stores manual transfer', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  await seedParticipant(ctx, { id: 'p2', groupId: 'g1', economicUnitId: 'u2' });
  const useCase = new AddManualTransferUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.transferRepository,
    ctx.idGenerator,
    ctx.clock
  );

  const result = await useCase.execute({
    groupId: 'g1',
    fromParticipantId: 'p2',
    toParticipantId: 'p1',
    amountMinor: 50
  });

  assert.equal(result.id, 'id-1');
  assert.equal(result.type, 'MANUAL');
});

test('AddManualTransferUseCase validates different participants', async () => {
  const ctx = createFixtureContext();
  await seedGroup(ctx, 'g1');
  await seedParticipant(ctx, { id: 'p1', groupId: 'g1', economicUnitId: 'u1' });
  const useCase = new AddManualTransferUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.transferRepository,
    ctx.idGenerator,
    ctx.clock
  );

  await assert.rejects(
    () =>
      useCase.execute({
        groupId: 'g1',
        fromParticipantId: 'p1',
        toParticipantId: 'p1',
        amountMinor: 50
      }),
    ValidationError
  );
});
