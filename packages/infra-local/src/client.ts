import { DatabaseSync } from 'node:sqlite';

import { applyMigrations } from './migrations';
import {
  EconomicUnitRepositorySqlite,
  ExpenseRepositorySqlite,
  GroupRepositorySqlite,
  ParticipantRepositorySqlite,
  TransferRepositorySqlite
} from './repositories';
import { exportGroupSnapshot, GroupSnapshotV1, importGroupSnapshot } from './snapshot';
import { SqliteTransactionRunner } from './transaction';

export interface InfraLocalSqlite {
  readonly db: DatabaseSync;
  readonly groupRepository: GroupRepositorySqlite;
  readonly participantRepository: ParticipantRepositorySqlite;
  readonly economicUnitRepository: EconomicUnitRepositorySqlite;
  readonly expenseRepository: ExpenseRepositorySqlite;
  readonly transferRepository: TransferRepositorySqlite;
  runInTransaction<T>(fn: () => Promise<T>): Promise<T>;
  exportGroupSnapshot(groupId: string): Promise<GroupSnapshotV1>;
  importGroupSnapshot(snapshot: unknown): Promise<void>;
  close(): void;
}

export interface CreateInfraLocalSqliteOptions {
  databasePath?: string;
}

export const createInfraLocalSqlite = async (
  options: CreateInfraLocalSqliteOptions = {}
): Promise<InfraLocalSqlite> => {
  const db = new DatabaseSync(options.databasePath ?? ':memory:');
  await applyMigrations(db);

  const transactionRunner = new SqliteTransactionRunner(db);

  return {
    db,
    groupRepository: new GroupRepositorySqlite(db, transactionRunner),
    participantRepository: new ParticipantRepositorySqlite(db, transactionRunner),
    economicUnitRepository: new EconomicUnitRepositorySqlite(db, transactionRunner),
    expenseRepository: new ExpenseRepositorySqlite(db, transactionRunner),
    transferRepository: new TransferRepositorySqlite(db, transactionRunner),
    runInTransaction: (fn) => transactionRunner.runInTransaction(fn),
    exportGroupSnapshot: (groupId) => exportGroupSnapshot(db, groupId),
    importGroupSnapshot: (snapshot) => importGroupSnapshot(db, transactionRunner, snapshot),
    close: () => db.close()
  };
};
