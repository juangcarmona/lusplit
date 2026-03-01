import { SQLiteDatabase } from 'expo-sqlite';

import { MobileSqliteTransactionRunner } from './transaction';

export interface GroupSnapshotV1 {
  version: 1;
  group: {
    id: string;
    currency: string;
    closed: boolean;
  };
  participants: Array<{
    id: string;
    groupId: string;
    economicUnitId: string;
    name: string;
    consumptionCategory: 'FULL' | 'HALF' | 'CUSTOM';
    customConsumptionWeight?: string;
  }>;
  economicUnits: Array<{
    id: string;
    groupId: string;
    ownerParticipantId: string;
    name?: string;
  }>;
  expenses: Array<{
    id: string;
    groupId: string;
    title: string;
    paidByParticipantId: string;
    amountMinor: number;
    date: string;
    splitDefinition: unknown;
    notes?: string;
  }>;
  transfers: Array<{
    id: string;
    groupId: string;
    fromParticipantId: string;
    toParticipantId: string;
    amountMinor: number;
    date: string;
    type: 'GENERATED' | 'MANUAL';
    note?: string;
  }>;
}

const asRecord = (value: unknown, label: string): Record<string, unknown> => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error(`Invalid snapshot ${label}`);
  }

  return value as Record<string, unknown>;
};

const asString = (value: unknown, label: string): string => {
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`Invalid snapshot ${label}`);
  }

  return value;
};

const asInteger = (value: unknown, label: string): number => {
  if (!Number.isInteger(value)) {
    throw new Error(`Invalid snapshot ${label}`);
  }

  return value as number;
};

const assertKnownParticipantIdsInSplit = (splitDefinition: unknown, participantIds: Set<string>): void => {
  const split = asRecord(splitDefinition, 'expense.splitDefinition');
  const components = split.components;

  if (!Array.isArray(components)) {
    throw new Error('Invalid snapshot expense.splitDefinition.components');
  }

  for (const componentValue of components) {
    const component = asRecord(componentValue, 'expense.splitDefinition.component');
    const type = asString(component.type, 'expense.splitDefinition.component.type');

    if (type === 'FIXED') {
      const shares = asRecord(component.shares, 'expense.splitDefinition.component.shares');
      for (const [participantId, amount] of Object.entries(shares)) {
        if (!participantIds.has(participantId)) {
          throw new Error(`Invalid snapshot split share participant: ${participantId}`);
        }

        asInteger(amount, `expense.splitDefinition.component.shares[${participantId}]`);
      }
      continue;
    }

    const participants = component.participants;
    if (!Array.isArray(participants)) {
      throw new Error('Invalid snapshot expense.splitDefinition.component.participants');
    }

    for (const participantId of participants) {
      const id = asString(participantId, 'expense.splitDefinition.component.participant');
      if (!participantIds.has(id)) {
        throw new Error(`Invalid snapshot split participant: ${id}`);
      }
    }

    if (component.weights !== undefined) {
      const weights = asRecord(component.weights, 'expense.splitDefinition.component.weights');
      for (const participantId of Object.keys(weights)) {
        if (!participantIds.has(participantId)) {
          throw new Error(`Invalid snapshot split weight participant: ${participantId}`);
        }
      }
    }

    if (component.percents !== undefined) {
      const percents = asRecord(component.percents, 'expense.splitDefinition.component.percents');
      for (const [participantId, percent] of Object.entries(percents)) {
        if (!participantIds.has(participantId)) {
          throw new Error(`Invalid snapshot split percent participant: ${participantId}`);
        }

        asInteger(percent, `expense.splitDefinition.component.percents[${participantId}]`);
      }
    }
  }
};

const ensureUniqueIds = (ids: string[], label: string): void => {
  if (new Set(ids).size !== ids.length) {
    throw new Error(`Invalid snapshot duplicate ${label} ids`);
  }
};

type GroupRow = {
  id: string;
  currency: string;
  closed: number;
};

type ParticipantRow = {
  id: string;
  group_id: string;
  economic_unit_id: string;
  name: string;
  consumption_category: 'FULL' | 'HALF' | 'CUSTOM';
  custom_consumption_weight: string | null;
};

type EconomicUnitRow = {
  id: string;
  group_id: string;
  owner_participant_id: string;
  name: string | null;
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
  type: 'GENERATED' | 'MANUAL';
  note: string | null;
};

export const exportGroupSnapshot = async (db: SQLiteDatabase, groupId: string): Promise<GroupSnapshotV1> => {
  const group = await db.getFirstAsync<GroupRow>('SELECT id, currency, closed FROM groups WHERE id = ?', [groupId]);

  if (!group) {
    throw new Error(`Group not found: ${groupId}`);
  }

  const participants = await db.getAllAsync<ParticipantRow>(
    `SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
     FROM participants WHERE group_id = ? ORDER BY id`,
    [groupId]
  );

  const economicUnits = await db.getAllAsync<EconomicUnitRow>(
    `SELECT id, group_id, owner_participant_id, name
     FROM economic_units WHERE group_id = ? ORDER BY id`,
    [groupId]
  );

  const expenses = await db.getAllAsync<ExpenseRow>(
    `SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
     FROM expenses WHERE group_id = ? ORDER BY id`,
    [groupId]
  );

  const transfers = await db.getAllAsync<TransferRow>(
    `SELECT id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
     FROM transfers WHERE group_id = ? ORDER BY id`,
    [groupId]
  );

  return {
    version: 1,
    group: {
      id: group.id,
      currency: group.currency,
      closed: group.closed === 1
    },
    participants: participants.map((participant) => ({
      id: participant.id,
      groupId: participant.group_id,
      economicUnitId: participant.economic_unit_id,
      name: participant.name,
      consumptionCategory: participant.consumption_category,
      customConsumptionWeight: participant.custom_consumption_weight ?? undefined
    })),
    economicUnits: economicUnits.map((economicUnit) => ({
      id: economicUnit.id,
      groupId: economicUnit.group_id,
      ownerParticipantId: economicUnit.owner_participant_id,
      name: economicUnit.name ?? undefined
    })),
    expenses: expenses.map((expense) => ({
      id: expense.id,
      groupId: expense.group_id,
      title: expense.title,
      paidByParticipantId: expense.paid_by_participant_id,
      amountMinor: expense.amount_minor,
      date: expense.date,
      splitDefinition: JSON.parse(expense.split_definition_json) as unknown,
      notes: expense.notes ?? undefined
    })),
    transfers: transfers.map((transfer) => ({
      id: transfer.id,
      groupId: transfer.group_id,
      fromParticipantId: transfer.from_participant_id,
      toParticipantId: transfer.to_participant_id,
      amountMinor: transfer.amount_minor,
      date: transfer.date,
      type: transfer.type,
      note: transfer.note ?? undefined
    }))
  };
};

export const importGroupSnapshot = async (
  db: SQLiteDatabase,
  transactionRunner: MobileSqliteTransactionRunner,
  snapshot: unknown
): Promise<void> => {
  const root = asRecord(snapshot, 'root');

  if (!Object.prototype.hasOwnProperty.call(root, 'version')) {
    throw new Error('Missing snapshot field: version');
  }
  if (root.version !== 1) {
    throw new Error(`Unsupported snapshot version: ${String(root.version)}`);
  }
  if (!Object.prototype.hasOwnProperty.call(root, 'group')) {
    throw new Error('Missing snapshot field: group');
  }
  if (!Object.prototype.hasOwnProperty.call(root, 'participants')) {
    throw new Error('Missing snapshot field: participants');
  }
  if (!Object.prototype.hasOwnProperty.call(root, 'economicUnits')) {
    throw new Error('Missing snapshot field: economicUnits');
  }
  if (!Object.prototype.hasOwnProperty.call(root, 'expenses')) {
    throw new Error('Missing snapshot field: expenses');
  }
  if (!Object.prototype.hasOwnProperty.call(root, 'transfers')) {
    throw new Error('Missing snapshot field: transfers');
  }

  const group = asRecord(root.group, 'group');
  const participants = Array.isArray(root.participants) ? root.participants : null;
  const economicUnits = Array.isArray(root.economicUnits) ? root.economicUnits : null;
  const expenses = Array.isArray(root.expenses) ? root.expenses : null;
  const transfers = Array.isArray(root.transfers) ? root.transfers : null;

  if (!participants || !economicUnits || !expenses || !transfers) {
    throw new Error('Invalid snapshot arrays');
  }

  const groupId = asString(group.id, 'group.id');
  const groupCurrency = asString(group.currency, 'group.currency');
  if (typeof group.closed !== 'boolean') {
    throw new Error('Invalid snapshot group.closed');
  }

  const economicUnitRecords = economicUnits.map((economicUnitValue) => {
    const economicUnit = asRecord(economicUnitValue, 'economicUnit');
    return {
      id: asString(economicUnit.id, 'economicUnit.id'),
      groupId: asString(economicUnit.groupId, 'economicUnit.groupId'),
      ownerParticipantId: asString(economicUnit.ownerParticipantId, 'economicUnit.ownerParticipantId'),
      name: economicUnit.name === undefined ? undefined : asString(economicUnit.name, 'economicUnit.name')
    };
  });

  const participantRecords = participants.map((participantValue) => {
    const participant = asRecord(participantValue, 'participant');
    const category = asString(participant.consumptionCategory, 'participant.consumptionCategory');
    if (category !== 'FULL' && category !== 'HALF' && category !== 'CUSTOM') {
      throw new Error(`Invalid snapshot participant.consumptionCategory: ${category}`);
    }

    return {
      id: asString(participant.id, 'participant.id'),
      groupId: asString(participant.groupId, 'participant.groupId'),
      economicUnitId: asString(participant.economicUnitId, 'participant.economicUnitId'),
      name: asString(participant.name, 'participant.name'),
      consumptionCategory: category,
      customConsumptionWeight:
        participant.customConsumptionWeight === undefined
          ? undefined
          : asString(participant.customConsumptionWeight, 'participant.customConsumptionWeight')
    };
  });

  const participantIds = new Set(participantRecords.map((participant) => participant.id));
  const economicUnitIds = new Set(economicUnitRecords.map((economicUnit) => economicUnit.id));

  const expenseRecords = expenses.map((expenseValue) => {
    const expense = asRecord(expenseValue, 'expense');

    return {
      id: asString(expense.id, 'expense.id'),
      groupId: asString(expense.groupId, 'expense.groupId'),
      title: asString(expense.title, 'expense.title'),
      paidByParticipantId: asString(expense.paidByParticipantId, 'expense.paidByParticipantId'),
      amountMinor: asInteger(expense.amountMinor, 'expense.amountMinor'),
      date: asString(expense.date, 'expense.date'),
      splitDefinition: expense.splitDefinition,
      notes: expense.notes === undefined ? undefined : asString(expense.notes, 'expense.notes')
    };
  });

  const transferRecords = transfers.map((transferValue) => {
    const transfer = asRecord(transferValue, 'transfer');
    const type = asString(transfer.type, 'transfer.type');
    if (type !== 'GENERATED' && type !== 'MANUAL') {
      throw new Error(`Invalid snapshot transfer.type: ${type}`);
    }

    return {
      id: asString(transfer.id, 'transfer.id'),
      groupId: asString(transfer.groupId, 'transfer.groupId'),
      fromParticipantId: asString(transfer.fromParticipantId, 'transfer.fromParticipantId'),
      toParticipantId: asString(transfer.toParticipantId, 'transfer.toParticipantId'),
      amountMinor: asInteger(transfer.amountMinor, 'transfer.amountMinor'),
      date: asString(transfer.date, 'transfer.date'),
      type,
      note: transfer.note === undefined ? undefined : asString(transfer.note, 'transfer.note')
    };
  });

  ensureUniqueIds(economicUnitRecords.map((economicUnit) => economicUnit.id), 'economicUnit');
  ensureUniqueIds(participantRecords.map((participant) => participant.id), 'participant');
  ensureUniqueIds(expenseRecords.map((expense) => expense.id), 'expense');
  ensureUniqueIds(transferRecords.map((transfer) => transfer.id), 'transfer');

  for (const economicUnit of economicUnitRecords) {
    if (economicUnit.groupId !== groupId) {
      throw new Error(`Invalid snapshot economicUnit.groupId for ${economicUnit.id}`);
    }
  }

  for (const participant of participantRecords) {
    if (participant.groupId !== groupId) {
      throw new Error(`Invalid snapshot participant.groupId for ${participant.id}`);
    }

    if (!economicUnitIds.has(participant.economicUnitId)) {
      throw new Error(`Invalid snapshot participant.economicUnitId for ${participant.id}`);
    }
  }

  for (const expense of expenseRecords) {
    if (expense.groupId !== groupId) {
      throw new Error(`Invalid snapshot expense.groupId for ${expense.id}`);
    }

    if (!participantIds.has(expense.paidByParticipantId)) {
      throw new Error(`Invalid snapshot expense.paidByParticipantId for ${expense.id}`);
    }

    assertKnownParticipantIdsInSplit(expense.splitDefinition, participantIds);
  }

  for (const transfer of transferRecords) {
    if (transfer.groupId !== groupId) {
      throw new Error(`Invalid snapshot transfer.groupId for ${transfer.id}`);
    }

    if (!participantIds.has(transfer.fromParticipantId) || !participantIds.has(transfer.toParticipantId)) {
      throw new Error(`Invalid snapshot transfer participant reference for ${transfer.id}`);
    }
  }

  await transactionRunner.runInTransaction(async () => {
    const existing = await db.getFirstAsync<{ id: string }>('SELECT id FROM groups WHERE id = ?', [groupId]);
    if (existing) {
      throw new Error(`Group already exists: ${groupId}`);
    }

    await db.runAsync('INSERT INTO groups (id, currency, closed) VALUES (?, ?, ?)', [groupId, groupCurrency, group.closed ? 1 : 0]);

    for (const economicUnit of [...economicUnitRecords].sort((left, right) => left.id.localeCompare(right.id))) {
      await db.runAsync('INSERT INTO economic_units (id, group_id, owner_participant_id, name) VALUES (?, ?, ?, ?)', [
        economicUnit.id,
        economicUnit.groupId,
        economicUnit.ownerParticipantId,
        economicUnit.name ?? null
      ]);
    }

    for (const participant of [...participantRecords].sort((left, right) => left.id.localeCompare(right.id))) {
      await db.runAsync(
        `INSERT INTO participants (
          id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
        ) VALUES (?, ?, ?, ?, ?, ?)`,
        [
          participant.id,
          participant.groupId,
          participant.economicUnitId,
          participant.name,
          participant.consumptionCategory,
          participant.customConsumptionWeight ?? null
        ]
      );
    }

    for (const expense of [...expenseRecords].sort((left, right) => left.id.localeCompare(right.id))) {
      await db.runAsync(
        `INSERT INTO expenses (
          id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
        [
          expense.id,
          expense.groupId,
          expense.title,
          expense.paidByParticipantId,
          expense.amountMinor,
          expense.date,
          JSON.stringify(expense.splitDefinition),
          expense.notes ?? null
        ]
      );
    }

    for (const transfer of [...transferRecords].sort((left, right) => left.id.localeCompare(right.id))) {
      await db.runAsync(
        `INSERT INTO transfers (
          id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
        [
          transfer.id,
          transfer.groupId,
          transfer.fromParticipantId,
          transfer.toParticipantId,
          transfer.amountMinor,
          transfer.date,
          transfer.type,
          transfer.note ?? null
        ]
      );
    }
  });
};
