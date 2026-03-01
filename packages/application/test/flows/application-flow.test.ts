import test = require('node:test');
import assert = require('node:assert/strict');

import {
  AddExpenseUseCase,
  CreateEconomicUnitUseCase,
  CreateGroupUseCase,
  CreateParticipantUseCase,
  GetBalancesByEconomicUnitOwnerUseCase,
  GetBalancesByParticipantUseCase,
  GetSettlementPlanUseCase
} from '../../src';
import { createFixtureContext } from '../fakes/fixture-context';

test('end-to-end application flow computes deterministic balances and settlement for participant and economic-unit-owner modes', async () => {
  const ctx = createFixtureContext();

  const createGroup = new CreateGroupUseCase(ctx.groupRepository, ctx.idGenerator);
  const createEconomicUnit = new CreateEconomicUnitUseCase(
    ctx.groupRepository,
    ctx.economicUnitRepository,
    ctx.idGenerator
  );
  const createParticipant = new CreateParticipantUseCase(
    ctx.groupRepository,
    ctx.economicUnitRepository,
    ctx.participantRepository,
    ctx.idGenerator
  );
  const addExpense = new AddExpenseUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository,
    ctx.idGenerator,
    ctx.clock
  );
  const getBalancesByParticipant = new GetBalancesByParticipantUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository
  );
  const getBalancesByEconomicUnitOwner = new GetBalancesByEconomicUnitOwnerUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );
  const getSettlementPlan = new GetSettlementPlanUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  );

  const group = await createGroup.execute({ currency: 'USD' });

  const unit1 = await createEconomicUnit.execute({ groupId: group.id, ownerParticipantId: 'p1', name: 'Unit 1' });
  const unit2 = await createEconomicUnit.execute({ groupId: group.id, ownerParticipantId: 'p2', name: 'Unit 2' });

  const p1 = await createParticipant.execute({
    groupId: group.id,
    economicUnitId: unit1.id,
    name: 'Alice',
    consumptionCategory: 'FULL'
  });
  const p2 = await createParticipant.execute({
    groupId: group.id,
    economicUnitId: unit2.id,
    name: 'Bob',
    consumptionCategory: 'FULL'
  });
  const p3 = await createParticipant.execute({
    groupId: group.id,
    economicUnitId: unit2.id,
    name: 'Carol',
    consumptionCategory: 'HALF'
  });

  await addExpense.execute({
    groupId: group.id,
    title: 'Dinner',
    paidByParticipantId: p1.id,
    amountMinor: 900,
    splitDefinition: {
      components: [{ type: 'REMAINDER', participants: [p1.id, p2.id, p3.id], mode: 'EQUAL' }]
    }
  });

  await addExpense.execute({
    groupId: group.id,
    title: 'Groceries',
    paidByParticipantId: p2.id,
    amountMinor: 600,
    splitDefinition: {
      components: [
        { type: 'FIXED', shares: { [p1.id]: 100 } },
        { type: 'REMAINDER', participants: [p2.id, p3.id], mode: 'EQUAL' }
      ]
    }
  });

  const balancesByParticipant = await getBalancesByParticipant.execute({ groupId: group.id });
  const balancesByOwner = await getBalancesByEconomicUnitOwner.execute({ groupId: group.id });
  const participantSettlement = await getSettlementPlan.execute({ groupId: group.id, mode: 'PARTICIPANT' });
  const ownerSettlement = await getSettlementPlan.execute({ groupId: group.id, mode: 'ECONOMIC_UNIT_OWNER' });

  assert.deepEqual(balancesByParticipant, [
    { participantId: p3.id, amountMinor: -550 },
    { participantId: p1.id, amountMinor: 500 },
    { participantId: p2.id, amountMinor: 50 }
  ]);

  assert.deepEqual(balancesByOwner, [
    { participantId: p1.id, amountMinor: 500 },
    { participantId: p2.id, amountMinor: -500 }
  ]);

  assert.deepEqual(participantSettlement.transfers, [
    { fromParticipantId: p3.id, toParticipantId: p1.id, amountMinor: 500 },
    { fromParticipantId: p3.id, toParticipantId: p2.id, amountMinor: 50 }
  ]);

  assert.deepEqual(ownerSettlement.transfers, [{ fromParticipantId: p2.id, toParticipantId: p1.id, amountMinor: 500 }]);
});
