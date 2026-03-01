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
import { SQLiteDatabase } from 'expo-sqlite';

import { MobileSqliteTransactionRunner } from './transaction';

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

type GroupOwnershipRow = {
	group_id: string;
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

const assertExistingIdBelongsToGroup = async (
	db: SQLiteDatabase,
	table: 'participants' | 'economic_units' | 'expenses' | 'transfers',
	id: string,
	groupId: string
): Promise<void> => {
	const idOwnershipQueryByTable = {
		participants: 'SELECT group_id FROM participants WHERE id = ?',
		economic_units: 'SELECT group_id FROM economic_units WHERE id = ?',
		expenses: 'SELECT group_id FROM expenses WHERE id = ?',
		transfers: 'SELECT group_id FROM transfers WHERE id = ?'
	} as const;

	const existing = await db.getFirstAsync<GroupOwnershipRow>(idOwnershipQueryByTable[table], [id]);

	if (existing && existing.group_id !== groupId) {
		throw new Error(`Cannot reuse ${table} id in another group: ${id}`);
	}
};

export class GroupRepositoryMobileSqlite implements GroupRepository {
	constructor(
		private readonly db: SQLiteDatabase,
		private readonly transactionRunner: MobileSqliteTransactionRunner
	) {}

	async getById(groupId: string): Promise<Group | null> {
		const row = await this.db.getFirstAsync<GroupRow>('SELECT id, currency, closed FROM groups WHERE id = ?', [groupId]);
		return row ? mapGroup(row) : null;
	}

	async save(group: Group): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await this.db.runAsync(
				`INSERT INTO groups (id, currency, closed)
				 VALUES (?, ?, ?)
				 ON CONFLICT(id) DO UPDATE SET
					 currency = excluded.currency,
					 closed = excluded.closed`,
				[String(group.id), group.currency, group.closed ? 1 : 0]
			);
		});
	}
}

export class ParticipantRepositoryMobileSqlite implements ParticipantRepository {
	constructor(
		private readonly db: SQLiteDatabase,
		private readonly transactionRunner: MobileSqliteTransactionRunner
	) {}

	async getById(participantId: string): Promise<Participant | null> {
		const row = await this.db.getFirstAsync<ParticipantRow>(
			`SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
			 FROM participants
			 WHERE id = ?`,
			[participantId]
		);

		return row ? mapParticipant(row) : null;
	}

	async listByGroupId(groupId: string): Promise<Participant[]> {
		const rows = await this.db.getAllAsync<ParticipantRow>(
			`SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
			 FROM participants
			 WHERE group_id = ?
			 ORDER BY id`,
			[groupId]
		);

		return rows.map(mapParticipant);
	}

	async save(participant: Participant): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await assertExistingIdBelongsToGroup(this.db, 'participants', String(participant.id), String(participant.groupId));

			await this.db.runAsync(
				`INSERT INTO participants (
					 group_id, id, economic_unit_id, name, consumption_category, custom_consumption_weight
				 ) VALUES (?, ?, ?, ?, ?, ?)
				 ON CONFLICT(id) DO UPDATE SET
					 economic_unit_id = excluded.economic_unit_id,
					 name = excluded.name,
					 consumption_category = excluded.consumption_category,
					 custom_consumption_weight = excluded.custom_consumption_weight`,
				[
					String(participant.groupId),
					String(participant.id),
					String(participant.economicUnitId),
					participant.name,
					participant.consumptionCategory,
					participant.customConsumptionWeight ?? null
				]
			);
		});
	}
}

export class EconomicUnitRepositoryMobileSqlite implements EconomicUnitRepository {
	constructor(
		private readonly db: SQLiteDatabase,
		private readonly transactionRunner: MobileSqliteTransactionRunner
	) {}

	async getById(economicUnitId: string): Promise<EconomicUnit | null> {
		const row = await this.db.getFirstAsync<EconomicUnitRow>(
			'SELECT id, group_id, owner_participant_id, name FROM economic_units WHERE id = ?',
			[economicUnitId]
		);

		return row ? mapEconomicUnit(row) : null;
	}

	async listByGroupId(groupId: string): Promise<EconomicUnit[]> {
		const rows = await this.db.getAllAsync<EconomicUnitRow>(
			`SELECT id, group_id, owner_participant_id, name
			 FROM economic_units
			 WHERE group_id = ?
			 ORDER BY id`,
			[groupId]
		);

		return rows.map(mapEconomicUnit);
	}

	async save(economicUnit: EconomicUnit): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await assertExistingIdBelongsToGroup(this.db, 'economic_units', String(economicUnit.id), String(economicUnit.groupId));

			await this.db.runAsync(
				`INSERT INTO economic_units (group_id, id, owner_participant_id, name)
				 VALUES (?, ?, ?, ?)
				 ON CONFLICT(id) DO UPDATE SET
					 owner_participant_id = excluded.owner_participant_id,
					 name = excluded.name`,
				[
					String(economicUnit.groupId),
					String(economicUnit.id),
					String(economicUnit.ownerParticipantId),
					economicUnit.name ?? null
				]
			);
		});
	}
}

export class ExpenseRepositoryMobileSqlite implements ExpenseRepository {
	constructor(
		private readonly db: SQLiteDatabase,
		private readonly transactionRunner: MobileSqliteTransactionRunner
	) {}

	async getById(expenseId: string): Promise<Expense | null> {
		const row = await this.db.getFirstAsync<ExpenseRow>(
			`SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
			 FROM expenses
			 WHERE id = ?`,
			[expenseId]
		);

		return row ? mapExpense(row) : null;
	}

	async listByGroupId(groupId: string): Promise<Expense[]> {
		const rows = await this.db.getAllAsync<ExpenseRow>(
			`SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
			 FROM expenses
			 WHERE group_id = ?
			 ORDER BY id`,
			[groupId]
		);

		return rows.map(mapExpense);
	}

	async save(expense: Expense): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await assertExistingIdBelongsToGroup(this.db, 'expenses', String(expense.id), String(expense.groupId));

			await this.db.runAsync(
				`INSERT INTO expenses (
					 group_id, id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
				 ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
				 ON CONFLICT(id) DO UPDATE SET
					 title = excluded.title,
					 paid_by_participant_id = excluded.paid_by_participant_id,
					 amount_minor = excluded.amount_minor,
					 date = excluded.date,
					 split_definition_json = excluded.split_definition_json,
					 notes = excluded.notes`,
				[
					String(expense.groupId),
					String(expense.id),
					expense.title,
					String(expense.paidByParticipantId),
					expense.amountMinor,
					expense.date,
					JSON.stringify(expense.splitDefinition),
					expense.notes ?? null
				]
			);
		});
	}

	async delete(groupId: string, expenseId: string): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await this.db.runAsync('DELETE FROM expenses WHERE group_id = ? AND id = ?', [groupId, expenseId]);
		});
	}
}

export class TransferRepositoryMobileSqlite implements TransferRepository {
	constructor(
		private readonly db: SQLiteDatabase,
		private readonly transactionRunner: MobileSqliteTransactionRunner
	) {}

	async listByGroupId(groupId: string): Promise<Transfer[]> {
		const rows = await this.db.getAllAsync<TransferRow>(
			`SELECT id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
			 FROM transfers
			 WHERE group_id = ?
			 ORDER BY id`,
			[groupId]
		);

		return rows.map(mapTransfer);
	}

	async save(transfer: Transfer): Promise<void> {
		await this.transactionRunner.runInTransaction(async () => {
			await assertExistingIdBelongsToGroup(this.db, 'transfers', String(transfer.id), String(transfer.groupId));

			await this.db.runAsync(
				`INSERT INTO transfers (
					 group_id, id, from_participant_id, to_participant_id, amount_minor, date, type, note
				 ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
				 ON CONFLICT(id) DO UPDATE SET
					 from_participant_id = excluded.from_participant_id,
					 to_participant_id = excluded.to_participant_id,
					 amount_minor = excluded.amount_minor,
					 date = excluded.date,
					 type = excluded.type,
					 note = excluded.note`,
				[
					String(transfer.groupId),
					String(transfer.id),
					String(transfer.fromParticipantId),
					String(transfer.toParticipantId),
					transfer.amountMinor,
					transfer.date,
					transfer.type,
					transfer.note ?? null
				]
			);
		});
	}
}
