import assert = require('node:assert/strict');
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test = require('node:test');
import { DatabaseSync } from 'node:sqlite';

import {
  AddExpenseUseCase,
  AddManualTransferUseCase,
  Clock,
  CreateEconomicUnitUseCase,
  CreateGroupUseCase,
  CreateParticipantUseCase,
  EconomicUnitRepository,
  ExpenseRepository,
  GetBalancesByEconomicUnitOwnerUseCase,
  GetBalancesByParticipantUseCase,
  GetGroupOverviewUseCase,
  GetSettlementPlanUseCase,
  GroupRepository,
  IdGenerator,
  ParticipantRepository,
  TransferRepository
} from '@lusplit/application';
import {
  EconomicUnit,
  Expense,
  Group,
  Participant,
  Transfer,
  asEconomicUnitId,
  asExpenseId,
  asGroupId,
  asParticipantId,
  asTransferId
} from '@lusplit/core';

import { createInfraLocalSqlite } from '../src/client';
import { applyMigrations } from '../src/migrations';

class InMemoryGroupRepository implements GroupRepository {
  private readonly groups = new Map<string, Group>();

  async getById(groupId: string): Promise<Group | null> {
    return this.groups.get(groupId) ?? null;
  }

  async save(group: Group): Promise<void> {
    this.groups.set(String(group.id), group);
  }
}

class InMemoryParticipantRepository implements ParticipantRepository {
  private readonly participants = new Map<string, Participant>();

  async getById(participantId: string): Promise<Participant | null> {
    return this.participants.get(participantId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<Participant[]> {
    return [...this.participants.values()]
      .filter((participant) => String(participant.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(participant: Participant): Promise<void> {
    this.participants.set(String(participant.id), participant);
  }
}

class InMemoryEconomicUnitRepository implements EconomicUnitRepository {
  private readonly economicUnits = new Map<string, EconomicUnit>();

  async getById(economicUnitId: string): Promise<EconomicUnit | null> {
    return this.economicUnits.get(economicUnitId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<EconomicUnit[]> {
    return [...this.economicUnits.values()]
      .filter((economicUnit) => String(economicUnit.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(economicUnit: EconomicUnit): Promise<void> {
    this.economicUnits.set(String(economicUnit.id), economicUnit);
  }
}

class InMemoryExpenseRepository implements ExpenseRepository {
  private readonly expenses = new Map<string, Expense>();

  async getById(expenseId: string): Promise<Expense | null> {
    return this.expenses.get(expenseId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<Expense[]> {
    return [...this.expenses.values()]
      .filter((expense) => String(expense.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(expense: Expense): Promise<void> {
    this.expenses.set(String(expense.id), expense);
  }

  async delete(groupId: string, expenseId: string): Promise<void> {
    const current = this.expenses.get(expenseId);
    if (current && String(current.groupId) === groupId) {
      this.expenses.delete(expenseId);
    }
  }
}

class InMemoryTransferRepository implements TransferRepository {
  private readonly transfers = new Map<string, Transfer>();

  async listByGroupId(groupId: string): Promise<Transfer[]> {
    return [...this.transfers.values()]
      .filter((transfer) => String(transfer.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(transfer: Transfer): Promise<void> {
    this.transfers.set(String(transfer.id), transfer);
  }
}

class SequentialIdGenerator implements IdGenerator {
  private current = 1;

  nextId(): string {
    const value = `id-${this.current}`;
    this.current += 1;
    return value;
  }
}

class FixedClock implements Clock {
  nowIso(): string {
    return '2026-01-01T12:00:00.000Z';
  }
}

interface ScenarioContext {
  groupRepository: GroupRepository;
  participantRepository: ParticipantRepository;
  economicUnitRepository: EconomicUnitRepository;
  expenseRepository: ExpenseRepository;
  transferRepository: TransferRepository;
  idGenerator: IdGenerator;
  clock: Clock;
}

const buildContext = (): ScenarioContext => ({
  groupRepository: new InMemoryGroupRepository(),
  participantRepository: new InMemoryParticipantRepository(),
  economicUnitRepository: new InMemoryEconomicUnitRepository(),
  expenseRepository: new InMemoryExpenseRepository(),
  transferRepository: new InMemoryTransferRepository(),
  idGenerator: new SequentialIdGenerator(),
  clock: new FixedClock()
});

const runScenario = async (ctx: ScenarioContext) => {
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
  const addManualTransfer = new AddManualTransferUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.transferRepository,
    ctx.idGenerator,
    ctx.clock
  );

  const group = await createGroup.execute({ currency: 'USD' });
  const unit1 = await createEconomicUnit.execute({ groupId: group.id, ownerParticipantId: 'id-4', name: 'Unit 1' });
  const unit2 = await createEconomicUnit.execute({ groupId: group.id, ownerParticipantId: 'id-5', name: 'Unit 2' });

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
    splitDefinition: { components: [{ type: 'REMAINDER', participants: [p1.id, p2.id, p3.id], mode: 'EQUAL' }] }
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

  await addManualTransfer.execute({
    groupId: group.id,
    fromParticipantId: p3.id,
    toParticipantId: p1.id,
    amountMinor: 50
  });

  const balancesByParticipant = await new GetBalancesByParticipantUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.expenseRepository
  ).execute({ groupId: group.id });

  const balancesByOwner = await new GetBalancesByEconomicUnitOwnerUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  ).execute({ groupId: group.id });

  const settlementByParticipant = await new GetSettlementPlanUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  ).execute({ groupId: group.id, mode: 'PARTICIPANT' });

  const settlementByOwner = await new GetSettlementPlanUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository
  ).execute({ groupId: group.id, mode: 'ECONOMIC_UNIT_OWNER' });

  const overview = await new GetGroupOverviewUseCase(
    ctx.groupRepository,
    ctx.participantRepository,
    ctx.economicUnitRepository,
    ctx.expenseRepository,
    ctx.transferRepository
  ).execute({ groupId: group.id });

  return {
    groupId: group.id,
    balancesByParticipant,
    balancesByOwner,
    settlementByParticipant,
    settlementByOwner,
    overview
  };
};

const runContractSuite = (name: string, createContext: () => Promise<ScenarioContext>) => {
  test(`${name} repository contract flow`, async () => {
    const ctx = await createContext();
    const result = await runScenario(ctx);

    assert.deepEqual(result.balancesByParticipant, [
      { entityId: 'id-4', amountMinor: 500 },
      { entityId: 'id-5', amountMinor: 50 },
      { entityId: 'id-6', amountMinor: -550 }
    ]);
    assert.deepEqual(result.balancesByOwner, [
      { entityId: 'id-4', amountMinor: 500 },
      { entityId: 'id-5', amountMinor: -500 }
    ]);
    assert.deepEqual(result.settlementByParticipant.transfers, [
      { fromParticipantId: 'id-6', toParticipantId: 'id-4', amountMinor: 500 },
      { fromParticipantId: 'id-6', toParticipantId: 'id-5', amountMinor: 50 }
    ]);
    assert.deepEqual(result.settlementByOwner.transfers, [
      { fromParticipantId: 'id-5', toParticipantId: 'id-4', amountMinor: 500 }
    ]);
    assert.equal(result.overview.summary.transferCount, 1);
  });
};

runContractSuite('in-memory', async () => buildContext());
runContractSuite('sqlite', async () => {
  const sqlite = await createInfraLocalSqlite();
  return {
    groupRepository: sqlite.groupRepository,
    participantRepository: sqlite.participantRepository,
    economicUnitRepository: sqlite.economicUnitRepository,
    expenseRepository: sqlite.expenseRepository,
    transferRepository: sqlite.transferRepository,
    idGenerator: new SequentialIdGenerator(),
    clock: new FixedClock()
  };
});

test('sqlite migrations are idempotent', async () => {
  const db = new DatabaseSync(':memory:');

  await applyMigrations(db);
  await applyMigrations(db);

  const versionCount = db.prepare('SELECT COUNT(*) as count FROM schema_version WHERE version = 1').get() as { count: number };
  assert.equal(versionCount.count, 1);

  const tables = db
    .prepare(
      `SELECT name FROM sqlite_master
       WHERE type = 'table' AND name IN ('groups','participants','economic_units','expenses','transfers','projection_snapshots')`
    )
    .all() as Array<{ name: string }>;

  assert.equal(tables.length, 6);
  db.close();
});

test('sqlite preserves deterministic results after reload', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'lusplit-infra-local-'));
  const dbPath = join(directory, 'data.sqlite');

  const first = await createInfraLocalSqlite({ databasePath: dbPath });
  const firstResult = await runScenario({
    groupRepository: first.groupRepository,
    participantRepository: first.participantRepository,
    economicUnitRepository: first.economicUnitRepository,
    expenseRepository: first.expenseRepository,
    transferRepository: first.transferRepository,
    idGenerator: new SequentialIdGenerator(),
    clock: new FixedClock()
  });

  first.close();

  const second = await createInfraLocalSqlite({ databasePath: dbPath });
  const secondResult = await new GetGroupOverviewUseCase(
    second.groupRepository,
    second.participantRepository,
    second.economicUnitRepository,
    second.expenseRepository,
    second.transferRepository
  ).execute({ groupId: firstResult.groupId });

  assert.deepEqual(secondResult.balancesByParticipant, firstResult.overview.balancesByParticipant);
  assert.deepEqual(secondResult.balancesByEconomicUnitOwner, firstResult.overview.balancesByEconomicUnitOwner);
  assert.deepEqual(secondResult.settlementByParticipant, firstResult.overview.settlementByParticipant);
  assert.deepEqual(secondResult.settlementByEconomicUnitOwner, firstResult.overview.settlementByEconomicUnitOwner);

  second.close();
  rmSync(directory, { recursive: true, force: true });
});

test('sqlite determinism is independent from insertion order', async () => {
  const writeScenario = async (order: 'A' | 'B') => {
    const sqlite = await createInfraLocalSqlite();

    await sqlite.groupRepository.save({ id: asGroupId('g1'), currency: 'USD', closed: false });
    await sqlite.economicUnitRepository.save({
      id: asEconomicUnitId('u1'),
      groupId: asGroupId('g1'),
      ownerParticipantId: asParticipantId('p1'),
      name: 'Unit 1'
    });
    await sqlite.economicUnitRepository.save({
      id: asEconomicUnitId('u2'),
      groupId: asGroupId('g1'),
      ownerParticipantId: asParticipantId('p2'),
      name: 'Unit 2'
    });

    const participants: Participant[] = [
      {
        id: asParticipantId('p1'),
        groupId: asGroupId('g1'),
        economicUnitId: asEconomicUnitId('u1'),
        name: 'Alice',
        consumptionCategory: 'FULL'
      },
      {
        id: asParticipantId('p2'),
        groupId: asGroupId('g1'),
        economicUnitId: asEconomicUnitId('u2'),
        name: 'Bob',
        consumptionCategory: 'FULL'
      },
      {
        id: asParticipantId('p3'),
        groupId: asGroupId('g1'),
        economicUnitId: asEconomicUnitId('u2'),
        name: 'Carol',
        consumptionCategory: 'HALF'
      }
    ];

    const expenses: Expense[] = [
      {
        id: asExpenseId('e1'),
        groupId: asGroupId('g1'),
        title: 'Dinner',
        paidByParticipantId: asParticipantId('p1'),
        amountMinor: 900,
        date: '2026-01-01T00:00:00.000Z',
        splitDefinition: { components: [{ type: 'REMAINDER', participants: [asParticipantId('p1'), asParticipantId('p2'), asParticipantId('p3')], mode: 'EQUAL' }] }
      },
      {
        id: asExpenseId('e2'),
        groupId: asGroupId('g1'),
        title: 'Groceries',
        paidByParticipantId: asParticipantId('p2'),
        amountMinor: 600,
        date: '2026-01-02T00:00:00.000Z',
        splitDefinition: {
          components: [
            { type: 'FIXED', shares: { [asParticipantId('p1')]: 100 } },
            { type: 'REMAINDER', participants: [asParticipantId('p2'), asParticipantId('p3')], mode: 'EQUAL' }
          ]
        }
      }
    ];

    if (order === 'A') {
      await sqlite.participantRepository.save(participants[0]);
      await sqlite.participantRepository.save(participants[1]);
      await sqlite.participantRepository.save(participants[2]);
      await sqlite.expenseRepository.save(expenses[0]);
      await sqlite.expenseRepository.save(expenses[1]);
    } else {
      await sqlite.participantRepository.save(participants[2]);
      await sqlite.participantRepository.save(participants[0]);
      await sqlite.participantRepository.save(participants[1]);
      await sqlite.expenseRepository.save(expenses[1]);
      await sqlite.expenseRepository.save(expenses[0]);
    }

    const balances = await new GetBalancesByParticipantUseCase(
      sqlite.groupRepository,
      sqlite.participantRepository,
      sqlite.expenseRepository
    ).execute({ groupId: 'g1' });

    const settlement = await new GetSettlementPlanUseCase(
      sqlite.groupRepository,
      sqlite.participantRepository,
      sqlite.economicUnitRepository,
      sqlite.expenseRepository
    ).execute({ groupId: 'g1', mode: 'PARTICIPANT' });

    sqlite.close();

    return { balances, settlement };
  };

  const first = await writeScenario('A');
  const second = await writeScenario('B');

  assert.deepEqual(second.balances, first.balances);
  assert.deepEqual(second.settlement, first.settlement);
});

test('sqlite export/import roundtrip preserves balances and settlement', async () => {
  const source = await createInfraLocalSqlite();
  const sourceResult = await runScenario({
    groupRepository: source.groupRepository,
    participantRepository: source.participantRepository,
    economicUnitRepository: source.economicUnitRepository,
    expenseRepository: source.expenseRepository,
    transferRepository: source.transferRepository,
    idGenerator: new SequentialIdGenerator(),
    clock: new FixedClock()
  });

  const snapshot = await source.exportGroupSnapshot(sourceResult.groupId);

  const target = await createInfraLocalSqlite();
  await target.importGroupSnapshot(snapshot);

  const importedResult = await new GetGroupOverviewUseCase(
    target.groupRepository,
    target.participantRepository,
    target.economicUnitRepository,
    target.expenseRepository,
    target.transferRepository
  ).execute({ groupId: sourceResult.groupId });

  assert.deepEqual(importedResult.balancesByParticipant, sourceResult.overview.balancesByParticipant);
  assert.deepEqual(importedResult.balancesByEconomicUnitOwner, sourceResult.overview.balancesByEconomicUnitOwner);
  assert.deepEqual(importedResult.settlementByParticipant, sourceResult.overview.settlementByParticipant);
  assert.deepEqual(importedResult.settlementByEconomicUnitOwner, sourceResult.overview.settlementByEconomicUnitOwner);

  const reExport = await target.exportGroupSnapshot(sourceResult.groupId);
  assert.deepEqual(reExport, snapshot);

  source.close();
  target.close();
});

test('sqlite import rejects malformed snapshot references', async () => {
  const sqlite = await createInfraLocalSqlite();

  await assert.rejects(
    sqlite.importGroupSnapshot({
      version: 1,
      group: { id: 'g1', currency: 'USD', closed: false },
      participants: [
        {
          id: 'p1',
          groupId: 'other-group',
          economicUnitId: 'u1',
          name: 'Alice',
          consumptionCategory: 'FULL'
        }
      ],
      economicUnits: [{ id: 'u1', groupId: 'g1', ownerParticipantId: 'p1' }],
      expenses: [],
      transfers: []
    })
  );

  sqlite.close();
});

test('sqlite repositories enforce globally unique ids across groups', async () => {
  const sqlite = await createInfraLocalSqlite();

  await sqlite.groupRepository.save({ id: asGroupId('g1'), currency: 'USD', closed: false });
  await sqlite.groupRepository.save({ id: asGroupId('g2'), currency: 'USD', closed: false });

  await sqlite.economicUnitRepository.save({
    id: asEconomicUnitId('u1'),
    groupId: asGroupId('g1'),
    ownerParticipantId: asParticipantId('p1'),
    name: 'Unit 1'
  });
  await sqlite.economicUnitRepository.save({
    id: asEconomicUnitId('u2'),
    groupId: asGroupId('g2'),
    ownerParticipantId: asParticipantId('p2'),
    name: 'Unit 2'
  });

  await sqlite.participantRepository.save({
    id: asParticipantId('p1'),
    groupId: asGroupId('g1'),
    economicUnitId: asEconomicUnitId('u1'),
    name: 'Alice',
    consumptionCategory: 'FULL'
  });

  await assert.rejects(
    sqlite.participantRepository.save({
      id: asParticipantId('p1'),
      groupId: asGroupId('g2'),
      economicUnitId: asEconomicUnitId('u2'),
      name: 'Alice clone',
      consumptionCategory: 'FULL'
    })
  );

  const participant = await sqlite.participantRepository.getById('p1');
  assert.equal(participant?.groupId, 'g1');
  sqlite.close();
});

test('sqlite import rejects wrong snapshot version and missing fields', async () => {
  const sqlite = await createInfraLocalSqlite();

  await assert.rejects(sqlite.importGroupSnapshot({ version: 2 }), /Unsupported snapshot version/);
  await assert.rejects(sqlite.importGroupSnapshot({ version: 1 }), /Missing snapshot field: group/);

  sqlite.close();
});
