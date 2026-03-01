import {
  EconomicUnitRepository,
  ExpenseRepository,
  GroupRepository,
  ParticipantRepository,
  TransferRepository
} from '@lusplit/application';
import { SQLiteDatabase, openDatabaseAsync } from 'expo-sqlite';

import { applyMigrations } from './migrations';
import {
  EconomicUnitRepositoryMobileSqlite,
  ExpenseRepositoryMobileSqlite,
  GroupRepositoryMobileSqlite,
  ParticipantRepositoryMobileSqlite,
  TransferRepositoryMobileSqlite
} from './repositories';
import { GroupSnapshotV1, exportGroupSnapshot, importGroupSnapshot } from './snapshot';
import { MobileSqliteTransactionRunner } from './transaction';

export interface InfraMobileSqlite {
  readonly db: SQLiteDatabase;
  readonly groupRepository: GroupRepository;
  readonly participantRepository: ParticipantRepository;
  readonly economicUnitRepository: EconomicUnitRepository;
  readonly expenseRepository: ExpenseRepository;
  readonly transferRepository: TransferRepository;
  runInTransaction<T>(fn: () => Promise<T>): Promise<T>;
  exportGroupSnapshot(groupId: string): Promise<GroupSnapshotV1>;
  importGroupSnapshot(snapshot: unknown): Promise<void>;
  reset(): Promise<void>;
  close(): Promise<void>;
}

export interface CreateInfraMobileSqliteOptions {
  databaseName?: string;
}

export const createInfraMobileSqlite = async (
  options: CreateInfraMobileSqliteOptions = {}
): Promise<InfraMobileSqlite> => {
  const db = await openDatabaseAsync(options.databaseName ?? 'lusplit.db');
  await applyMigrations(db);

  const transactionRunner = new MobileSqliteTransactionRunner(db);

  return {
    db,
    groupRepository: new GroupRepositoryMobileSqlite(db, transactionRunner),
    participantRepository: new ParticipantRepositoryMobileSqlite(db, transactionRunner),
    economicUnitRepository: new EconomicUnitRepositoryMobileSqlite(db, transactionRunner),
    expenseRepository: new ExpenseRepositoryMobileSqlite(db, transactionRunner),
    transferRepository: new TransferRepositoryMobileSqlite(db, transactionRunner),
    runInTransaction: (fn) => transactionRunner.runInTransaction(fn),
    exportGroupSnapshot: (groupId) => exportGroupSnapshot(db, groupId),
    importGroupSnapshot: (snapshot) => importGroupSnapshot(db, transactionRunner, snapshot),
    reset: async () => {
      await transactionRunner.runInTransaction(async () => {
        await db.runAsync('DELETE FROM transfers');
        await db.runAsync('DELETE FROM expenses');
        await db.runAsync('DELETE FROM participants');
        await db.runAsync('DELETE FROM economic_units');
        await db.runAsync('DELETE FROM groups');
        await db.runAsync('DELETE FROM projection_snapshots');
      });
    },
    close: async () => db.closeAsync()
  };
};
