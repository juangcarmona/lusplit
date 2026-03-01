import {
  ConsumptionCategory,
  EconomicUnit,
  Expense,
  Group,
  Participant,
  SplitDefinition,
  Transfer,
  asEconomicUnitId,
  asExpenseId,
  asGroupId,
  asParticipantId,
  asTransferId
} from '@lusplit/core';

import {
  FixedClock,
  InMemoryEconomicUnitRepository,
  InMemoryExpenseRepository,
  InMemoryGroupRepository,
  InMemoryParticipantRepository,
  InMemoryTransferRepository,
  SequentialIdGenerator
} from './in-memory-ports';

export interface FixtureContext {
  groupRepository: InMemoryGroupRepository;
  participantRepository: InMemoryParticipantRepository;
  economicUnitRepository: InMemoryEconomicUnitRepository;
  expenseRepository: InMemoryExpenseRepository;
  transferRepository: InMemoryTransferRepository;
  idGenerator: SequentialIdGenerator;
  clock: FixedClock;
}

export const createFixtureContext = (): FixtureContext => ({
  groupRepository: new InMemoryGroupRepository(),
  participantRepository: new InMemoryParticipantRepository(),
  economicUnitRepository: new InMemoryEconomicUnitRepository(),
  expenseRepository: new InMemoryExpenseRepository(),
  transferRepository: new InMemoryTransferRepository(),
  idGenerator: new SequentialIdGenerator(),
  clock: new FixedClock('2026-01-01T12:00:00.000Z')
});

export const seedGroup = async (ctx: FixtureContext, groupId = 'g1', closed = false): Promise<Group> => {
  const group: Group = { id: asGroupId(groupId), currency: 'USD', closed };
  await ctx.groupRepository.save(group);
  return group;
};

export const seedEconomicUnit = async (
  ctx: FixtureContext,
  overrides: { id?: string; groupId?: string; ownerParticipantId?: string; name?: string } = {}
): Promise<EconomicUnit> => {
  const economicUnit: EconomicUnit = {
    id: asEconomicUnitId(overrides.id ?? 'u1'),
    groupId: asGroupId(overrides.groupId ?? 'g1'),
    ownerParticipantId: asParticipantId(overrides.ownerParticipantId ?? 'p1'),
    name: overrides.name
  };
  await ctx.economicUnitRepository.save(economicUnit);
  return economicUnit;
};

export const seedParticipant = async (
  ctx: FixtureContext,
  overrides: {
    id?: string;
    groupId?: string;
    economicUnitId?: string;
    name?: string;
    consumptionCategory?: ConsumptionCategory;
    customConsumptionWeight?: string;
  } = {}
): Promise<Participant> => {
  const participant: Participant = {
    id: asParticipantId(overrides.id ?? 'p1'),
    groupId: asGroupId(overrides.groupId ?? 'g1'),
    economicUnitId: asEconomicUnitId(overrides.economicUnitId ?? 'u1'),
    name: overrides.name ?? 'Participant',
    consumptionCategory: overrides.consumptionCategory ?? 'FULL',
    customConsumptionWeight: overrides.customConsumptionWeight
  };
  await ctx.participantRepository.save(participant);
  return participant;
};

export const seedExpense = async (
  ctx: FixtureContext,
  overrides: {
    id?: string;
    groupId?: string;
    title?: string;
    paidByParticipantId?: string;
    amountMinor?: number;
    date?: string;
    splitDefinition?: SplitDefinition;
    notes?: string;
  } = {}
): Promise<Expense> => {
  const expense: Expense = {
    id: asExpenseId(overrides.id ?? 'e1'),
    groupId: asGroupId(overrides.groupId ?? 'g1'),
    title: overrides.title ?? 'Expense',
    paidByParticipantId: asParticipantId(overrides.paidByParticipantId ?? 'p1'),
    amountMinor: overrides.amountMinor ?? 100,
    date: overrides.date ?? '2026-01-01T00:00:00.000Z',
    splitDefinition:
      overrides.splitDefinition ?? { components: [{ type: 'REMAINDER', participants: [asParticipantId('p1')], mode: 'EQUAL' }] },
    notes: overrides.notes
  };
  await ctx.expenseRepository.save(expense);
  return expense;
};

export const seedTransfer = async (
  ctx: FixtureContext,
  overrides: {
    id?: string;
    groupId?: string;
    fromParticipantId?: string;
    toParticipantId?: string;
    amountMinor?: number;
    date?: string;
    type?: Transfer['type'];
    note?: string;
  } = {}
): Promise<Transfer> => {
  const transfer: Transfer = {
    id: asTransferId(overrides.id ?? 't1'),
    groupId: asGroupId(overrides.groupId ?? 'g1'),
    fromParticipantId: asParticipantId(overrides.fromParticipantId ?? 'p2'),
    toParticipantId: asParticipantId(overrides.toParticipantId ?? 'p1'),
    amountMinor: overrides.amountMinor ?? 10,
    date: overrides.date ?? '2026-01-01T00:00:00.000Z',
    type: overrides.type ?? 'MANUAL',
    note: overrides.note
  };
  await ctx.transferRepository.save(transfer);
  return transfer;
};
