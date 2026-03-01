import assert = require('node:assert/strict');
import test = require('node:test');

import { exportGroupSnapshot, importGroupSnapshot } from '../src/snapshot';
import { MobileSqliteTransactionRunner } from '../src/transaction';

class FakeSqliteDatabase {
  public readonly runCalls: Array<{ sql: string; params: unknown[] }> = [];

  constructor(
    private readonly rows: {
      group?: { id: string; currency: string; closed: number };
      participants?: Array<{
        id: string;
        group_id: string;
        economic_unit_id: string;
        name: string;
        consumption_category: 'FULL' | 'HALF' | 'CUSTOM';
        custom_consumption_weight: string | null;
      }>;
      economicUnits?: Array<{
        id: string;
        group_id: string;
        owner_participant_id: string;
        name: string | null;
      }>;
      expenses?: Array<{
        id: string;
        group_id: string;
        title: string;
        paid_by_participant_id: string;
        amount_minor: number;
        date: string;
        split_definition_json: string;
        notes: string | null;
      }>;
      transfers?: Array<{
        id: string;
        group_id: string;
        from_participant_id: string;
        to_participant_id: string;
        amount_minor: number;
        date: string;
        type: 'GENERATED' | 'MANUAL';
        note: string | null;
      }>;
      existingGroupId?: string;
    } = {}
  ) {}

  async withExclusiveTransactionAsync(task: () => Promise<void>): Promise<void> {
    await task();
  }

  async getFirstAsync<T>(sql: string, params: unknown[]): Promise<T | null> {
    if (sql.includes('FROM groups WHERE id = ?')) {
      const [groupId] = params;
      if (this.rows.group && this.rows.group.id === groupId) {
        return this.rows.group as T;
      }

      return null;
    }

    if (sql.includes('SELECT id FROM groups WHERE id = ?')) {
      const [groupId] = params;
      if (this.rows.existingGroupId && this.rows.existingGroupId === groupId) {
        return { id: String(groupId) } as T;
      }

      return null;
    }

    return null;
  }

  async getAllAsync<T>(sql: string, _params: unknown[]): Promise<T[]> {
    if (sql.includes('FROM participants')) {
      return (this.rows.participants ?? []) as T[];
    }

    if (sql.includes('FROM economic_units')) {
      return (this.rows.economicUnits ?? []) as T[];
    }

    if (sql.includes('FROM expenses')) {
      return (this.rows.expenses ?? []) as T[];
    }

    if (sql.includes('FROM transfers')) {
      return (this.rows.transfers ?? []) as T[];
    }

    return [];
  }

  async runAsync(sql: string, params: unknown[]): Promise<void> {
    this.runCalls.push({ sql, params });
  }
}

test('importGroupSnapshot sorts inserts by id for deterministic ordering', async () => {
  const db = new FakeSqliteDatabase();
  const transactionRunner = new MobileSqliteTransactionRunner(db as never);

  await importGroupSnapshot(
    db as never,
    transactionRunner,
    {
      version: 1,
      group: { id: 'g-1', currency: 'USD', closed: false },
      economicUnits: [
        { id: 'eu-2', groupId: 'g-1', ownerParticipantId: 'p-2', name: 'EU 2' },
        { id: 'eu-1', groupId: 'g-1', ownerParticipantId: 'p-1', name: 'EU 1' }
      ],
      participants: [
        { id: 'p-2', groupId: 'g-1', economicUnitId: 'eu-2', name: 'Bob', consumptionCategory: 'FULL' },
        { id: 'p-1', groupId: 'g-1', economicUnitId: 'eu-1', name: 'Alice', consumptionCategory: 'FULL' }
      ],
      expenses: [
        {
          id: 'e-2',
          groupId: 'g-1',
          title: 'Dinner',
          paidByParticipantId: 'p-2',
          amountMinor: 200,
          date: '2026-01-01T00:00:00.000Z',
          splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p-1', 'p-2'], mode: 'EQUAL' }] }
        },
        {
          id: 'e-1',
          groupId: 'g-1',
          title: 'Lunch',
          paidByParticipantId: 'p-1',
          amountMinor: 100,
          date: '2026-01-01T00:00:00.000Z',
          splitDefinition: { components: [{ type: 'REMAINDER', participants: ['p-1', 'p-2'], mode: 'EQUAL' }] }
        }
      ],
      transfers: [
        {
          id: 't-2',
          groupId: 'g-1',
          fromParticipantId: 'p-2',
          toParticipantId: 'p-1',
          amountMinor: 20,
          date: '2026-01-02T00:00:00.000Z',
          type: 'MANUAL'
        },
        {
          id: 't-1',
          groupId: 'g-1',
          fromParticipantId: 'p-1',
          toParticipantId: 'p-2',
          amountMinor: 10,
          date: '2026-01-02T00:00:00.000Z',
          type: 'MANUAL'
        }
      ]
    }
  );

  const economicUnitInserts = db.runCalls
    .filter((call) => call.sql.includes('INSERT INTO economic_units'))
    .map((call) => String(call.params[0]));
  const participantInserts = db.runCalls
    .filter((call) => call.sql.includes('INSERT INTO participants'))
    .map((call) => String(call.params[0]));
  const expenseInserts = db.runCalls
    .filter((call) => call.sql.includes('INSERT INTO expenses'))
    .map((call) => String(call.params[0]));
  const transferInserts = db.runCalls
    .filter((call) => call.sql.includes('INSERT INTO transfers'))
    .map((call) => String(call.params[0]));

  assert.deepEqual(economicUnitInserts, ['eu-1', 'eu-2']);
  assert.deepEqual(participantInserts, ['p-1', 'p-2']);
  assert.deepEqual(expenseInserts, ['e-1', 'e-2']);
  assert.deepEqual(transferInserts, ['t-1', 't-2']);
});

test('importGroupSnapshot rejects duplicate ids', async () => {
  const db = new FakeSqliteDatabase();
  const transactionRunner = new MobileSqliteTransactionRunner(db as never);

  await assert.rejects(
    importGroupSnapshot(
      db as never,
      transactionRunner,
      {
        version: 1,
        group: { id: 'g-1', currency: 'USD', closed: false },
        economicUnits: [{ id: 'eu-1', groupId: 'g-1', ownerParticipantId: 'p-1' }],
        participants: [
          { id: 'p-1', groupId: 'g-1', economicUnitId: 'eu-1', name: 'Alice', consumptionCategory: 'FULL' },
          { id: 'p-1', groupId: 'g-1', economicUnitId: 'eu-1', name: 'Alice2', consumptionCategory: 'FULL' }
        ],
        expenses: [],
        transfers: []
      }
    ),
    /duplicate participant ids/
  );
});

test('exportGroupSnapshot returns canonical shape', async () => {
  const db = new FakeSqliteDatabase({
    group: { id: 'g-1', currency: 'USD', closed: 0 },
    economicUnits: [{ id: 'eu-1', group_id: 'g-1', owner_participant_id: 'p-1', name: 'Unit' }],
    participants: [
      {
        id: 'p-1',
        group_id: 'g-1',
        economic_unit_id: 'eu-1',
        name: 'Alice',
        consumption_category: 'FULL',
        custom_consumption_weight: null
      }
    ],
    expenses: [
      {
        id: 'e-1',
        group_id: 'g-1',
        title: 'Dinner',
        paid_by_participant_id: 'p-1',
        amount_minor: 120,
        date: '2026-01-01T00:00:00.000Z',
        split_definition_json: JSON.stringify({ components: [{ type: 'REMAINDER', participants: ['p-1'], mode: 'EQUAL' }] }),
        notes: null
      }
    ],
    transfers: []
  });

  const snapshot = await exportGroupSnapshot(db as never, 'g-1');

  assert.equal(snapshot.version, 1);
  assert.equal(snapshot.group.id, 'g-1');
  assert.equal(snapshot.participants.length, 1);
  assert.equal(snapshot.expenses.length, 1);
});
