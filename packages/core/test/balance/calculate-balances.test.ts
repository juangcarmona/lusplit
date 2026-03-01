import test = require('node:test');
import assert = require('node:assert/strict');

import {
  DomainError,
  EconomicUnit,
  Expense,
  Participant,
  aggregateBalancesByEconomicUnitOwner,
  asEconomicUnitId,
  asExpenseId,
  asGroupId,
  asParticipantId,
  calculateParticipantBalances
} from '../../src';

const groupId = asGroupId('g1');
const p1 = asParticipantId('a');
const p2 = asParticipantId('b');
const p3 = asParticipantId('c');

const participants: Participant[] = [
  { id: p1, groupId, name: 'A', economicUnitId: asEconomicUnitId('u1'), consumptionCategory: 'FULL' },
  { id: p2, groupId, name: 'B', economicUnitId: asEconomicUnitId('u2'), consumptionCategory: 'FULL' },
  { id: p3, groupId, name: 'C', economicUnitId: asEconomicUnitId('u2'), consumptionCategory: 'FULL' }
];
const units: EconomicUnit[] = [
  { id: asEconomicUnitId('u1'), groupId, ownerParticipantId: p1, name: 'Unit 1' },
  { id: asEconomicUnitId('u2'), groupId, ownerParticipantId: p2, name: 'Unit 2' }
];

const expenses: Expense[] = [
  {
    id: asExpenseId('e1'),
    groupId,
    title: 'Dinner',
    paidByParticipantId: p1,
    amountMinor: 900,
    date: '2026-01-01',
    splitDefinition: {
      components: [{ type: 'REMAINDER', participants: [p1, p2, p3], mode: 'EQUAL' }]
    }
  },
  {
    id: asExpenseId('e2'),
    groupId,
    title: 'Taxi',
    paidByParticipantId: p2,
    amountMinor: 600,
    date: '2026-01-02',
    splitDefinition: {
      components: [{ type: 'REMAINDER', participants: [p1, p2], mode: 'EQUAL' }]
    }
  }
];

test('calculates participant balances with zero-sum invariant', () => {
  const balances = calculateParticipantBalances(expenses, participants);

  assert.equal(balances.get(p1), 300);
  assert.equal(balances.get(p2), 0);
  assert.equal(balances.get(p3), -300);
  assert.equal([...balances.values()].reduce((sum, value) => sum + value, 0), 0);
});

test('aggregates balances by economic unit owner', () => {
  const balances = new Map([
    [p1, 400],
    [p2, -100],
    [p3, -300]
  ]);

  const aggregated = aggregateBalancesByEconomicUnitOwner(balances, participants, units);

  assert.equal(aggregated.get(p1), 400);
  assert.equal(aggregated.get(p2), -400);
});

test('throws when unit owner does not belong to its unit', () => {
  assert.throws(
    () =>
      aggregateBalancesByEconomicUnitOwner(new Map([[p1, 0]]), participants, [
        { id: asEconomicUnitId('u1'), groupId, ownerParticipantId: p2, name: 'Broken Unit' }
      ]),
    DomainError
  );
});
