import {
  EconomicUnitRepository,
  ExpenseRepository,
  GroupRepository,
  ParticipantRepository,
  TransferRepository
} from '@lusplit/application';
import {
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
import { DatabaseSync } from 'node:sqlite';

import { SqliteTransactionRunner } from './transaction';

type GroupRow = {
  id: string;
  currency: string;
  closed: number;
};

type EconomicUnitRow = {
  id: string;
  group_id: string;
  owner_participant_id: string;
  name: string | null;
};

type ParticipantRow = {
  id: string;
  group_id: string;
  economic_unit_id: string;
  name: string;
  consumption_category: Participant['consumptionCategory'];
  custom_consumption_weight: string | null;
};

type ExpenseRow = {
  id: string;
  group_id: string;
  title: string;
  paid_by_participant_id: string;
  amount_minor: number;
  date: string;
  split_definition_json: string;
  notes: string | null;
};

type TransferRow = {
  id: string;
  group_id: string;
  from_participant_id: string;
  to_participant_id: string;
  amount_minor: number;
  date: string;
  type: Transfer['type'];
  note: string | null;
};

const mapGroup = (row: GroupRow): Group => ({
  id: asGroupId(row.id),
  currency: row.currency,
  closed: row.closed === 1
});

const mapEconomicUnit = (row: EconomicUnitRow): EconomicUnit => ({
  id: asEconomicUnitId(row.id),
  groupId: asGroupId(row.group_id),
  ownerParticipantId: asParticipantId(row.owner_participant_id),
  name: row.name ?? undefined
});

const mapParticipant = (row: ParticipantRow): Participant => ({
  id: asParticipantId(row.id),
  groupId: asGroupId(row.group_id),
  economicUnitId: asEconomicUnitId(row.economic_unit_id),
  name: row.name,
  consumptionCategory: row.consumption_category,
  customConsumptionWeight: row.custom_consumption_weight ?? undefined
});

const parseSplitDefinition = (json: string): SplitDefinition => JSON.parse(json) as SplitDefinition;

const mapExpense = (row: ExpenseRow): Expense => ({
  id: asExpenseId(row.id),
  groupId: asGroupId(row.group_id),
  title: row.title,
  paidByParticipantId: asParticipantId(row.paid_by_participant_id),
  amountMinor: row.amount_minor,
  date: row.date,
  splitDefinition: parseSplitDefinition(row.split_definition_json),
  notes: row.notes ?? undefined
});

const mapTransfer = (row: TransferRow): Transfer => ({
  id: asTransferId(row.id),
  groupId: asGroupId(row.group_id),
  fromParticipantId: asParticipantId(row.from_participant_id),
  toParticipantId: asParticipantId(row.to_participant_id),
  amountMinor: row.amount_minor,
  date: row.date,
  type: row.type,
  note: row.note ?? undefined
});

const assertExistingIdBelongsToGroup = (
  db: DatabaseSync,
  table: 'participants' | 'economic_units' | 'expenses' | 'transfers',
  id: string,
  groupId: string
): void => {
  const existing = db
    .prepare(`SELECT group_id FROM ${table} WHERE id = ?`)
    .get(id) as { group_id: string } | undefined;

  if (existing && existing.group_id !== groupId) {
    throw new Error(`Cannot reuse ${table} id in another group: ${id}`);
  }
};

export class GroupRepositorySqlite implements GroupRepository {
  constructor(
    private readonly db: DatabaseSync,
    private readonly transactionRunner: SqliteTransactionRunner
  ) {}

  async getById(groupId: string): Promise<Group | null> {
    const row = this.db
      .prepare('SELECT id, currency, closed FROM groups WHERE id = ?')
      .get(groupId) as GroupRow | undefined;

    return row ? mapGroup(row) : null;
  }

  async save(group: Group): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      this.db
        .prepare(
          `INSERT INTO groups (id, currency, closed)
           VALUES (?, ?, ?)
           ON CONFLICT(id) DO UPDATE SET
             currency = excluded.currency,
             closed = excluded.closed`
        )
        .run(String(group.id), group.currency, group.closed ? 1 : 0);
    });
  }
}

export class ParticipantRepositorySqlite implements ParticipantRepository {
  constructor(
    private readonly db: DatabaseSync,
    private readonly transactionRunner: SqliteTransactionRunner
  ) {}

  async getById(participantId: string): Promise<Participant | null> {
    const row = this.db
      .prepare(
        `SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
         FROM participants
         WHERE id = ?`
      )
      .get(participantId) as ParticipantRow | undefined;

    return row ? mapParticipant(row) : null;
  }

  async listByGroupId(groupId: string): Promise<Participant[]> {
    const rows = this.db
      .prepare(
        `SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
         FROM participants
         WHERE group_id = ?
         ORDER BY id`
      )
      .all(groupId) as ParticipantRow[];

    return rows.map(mapParticipant);
  }

  async save(participant: Participant): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      assertExistingIdBelongsToGroup(this.db, 'participants', String(participant.id), String(participant.groupId));

      this.db
        .prepare(
           `INSERT INTO participants (
              group_id, id, economic_unit_id, name, consumption_category, custom_consumption_weight
            ) VALUES (?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
              group_id = excluded.group_id,
              economic_unit_id = excluded.economic_unit_id,
              name = excluded.name,
              consumption_category = excluded.consumption_category,
              custom_consumption_weight = excluded.custom_consumption_weight`
        )
        .run(
          String(participant.groupId),
          String(participant.id),
          String(participant.economicUnitId),
          participant.name,
          participant.consumptionCategory,
          participant.customConsumptionWeight ?? null
        );
    });
  }
}

export class EconomicUnitRepositorySqlite implements EconomicUnitRepository {
  constructor(
    private readonly db: DatabaseSync,
    private readonly transactionRunner: SqliteTransactionRunner
  ) {}

  async getById(economicUnitId: string): Promise<EconomicUnit | null> {
    const row = this.db
      .prepare('SELECT id, group_id, owner_participant_id, name FROM economic_units WHERE id = ?')
      .get(economicUnitId) as EconomicUnitRow | undefined;

    return row ? mapEconomicUnit(row) : null;
  }

  async listByGroupId(groupId: string): Promise<EconomicUnit[]> {
    const rows = this.db
      .prepare(
        `SELECT id, group_id, owner_participant_id, name
         FROM economic_units
         WHERE group_id = ?
         ORDER BY id`
      )
      .all(groupId) as EconomicUnitRow[];

    return rows.map(mapEconomicUnit);
  }

  async save(economicUnit: EconomicUnit): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      assertExistingIdBelongsToGroup(this.db, 'economic_units', String(economicUnit.id), String(economicUnit.groupId));

      this.db
        .prepare(
           `INSERT INTO economic_units (group_id, id, owner_participant_id, name)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
              group_id = excluded.group_id,
              owner_participant_id = excluded.owner_participant_id,
              name = excluded.name`
        )
        .run(
          String(economicUnit.groupId),
          String(economicUnit.id),
          String(economicUnit.ownerParticipantId),
          economicUnit.name ?? null
        );
    });
  }
}

export class ExpenseRepositorySqlite implements ExpenseRepository {
  constructor(
    private readonly db: DatabaseSync,
    private readonly transactionRunner: SqliteTransactionRunner
  ) {}

  async getById(expenseId: string): Promise<Expense | null> {
    const row = this.db
      .prepare(
        `SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
         FROM expenses
         WHERE id = ?`
      )
      .get(expenseId) as ExpenseRow | undefined;

    return row ? mapExpense(row) : null;
  }

  async listByGroupId(groupId: string): Promise<Expense[]> {
    const rows = this.db
      .prepare(
        `SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
         FROM expenses
         WHERE group_id = ?
         ORDER BY id`
      )
      .all(groupId) as ExpenseRow[];

    return rows.map(mapExpense);
  }

  async save(expense: Expense): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      assertExistingIdBelongsToGroup(this.db, 'expenses', String(expense.id), String(expense.groupId));

      this.db
        .prepare(
           `INSERT INTO expenses (
              group_id, id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
              group_id = excluded.group_id,
              title = excluded.title,
              paid_by_participant_id = excluded.paid_by_participant_id,
              amount_minor = excluded.amount_minor,
             date = excluded.date,
             split_definition_json = excluded.split_definition_json,
             notes = excluded.notes`
        )
        .run(
          String(expense.groupId),
          String(expense.id),
          expense.title,
          String(expense.paidByParticipantId),
          expense.amountMinor,
          expense.date,
          JSON.stringify(expense.splitDefinition),
          expense.notes ?? null
        );
    });
  }

  async delete(groupId: string, expenseId: string): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      this.db.prepare('DELETE FROM expenses WHERE group_id = ? AND id = ?').run(groupId, expenseId);
    });
  }
}

export class TransferRepositorySqlite implements TransferRepository {
  constructor(
    private readonly db: DatabaseSync,
    private readonly transactionRunner: SqliteTransactionRunner
  ) {}

  async listByGroupId(groupId: string): Promise<Transfer[]> {
    const rows = this.db
      .prepare(
        `SELECT id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
         FROM transfers
         WHERE group_id = ?
         ORDER BY id`
      )
      .all(groupId) as TransferRow[];

    return rows.map(mapTransfer);
  }

  async save(transfer: Transfer): Promise<void> {
    await this.transactionRunner.runInTransaction(async () => {
      assertExistingIdBelongsToGroup(this.db, 'transfers', String(transfer.id), String(transfer.groupId));

      this.db
        .prepare(
           `INSERT INTO transfers (
              group_id, id, from_participant_id, to_participant_id, amount_minor, date, type, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
              group_id = excluded.group_id,
              from_participant_id = excluded.from_participant_id,
              to_participant_id = excluded.to_participant_id,
             amount_minor = excluded.amount_minor,
             date = excluded.date,
             type = excluded.type,
             note = excluded.note`
        )
        .run(
          String(transfer.groupId),
          String(transfer.id),
          String(transfer.fromParticipantId),
          String(transfer.toParticipantId),
          transfer.amountMinor,
          transfer.date,
          transfer.type,
          transfer.note ?? null
        );
    });
  }
}
