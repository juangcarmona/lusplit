import { SQLiteDatabase } from 'expo-sqlite';

export class MobileSqliteTransactionRunner {
  constructor(private readonly db: SQLiteDatabase) {}

  async runInTransaction<T>(fn: () => Promise<T>): Promise<T> {
    let result!: T;

    await this.db.withExclusiveTransactionAsync(async () => {
      result = await fn();
    });

    return result;
  }
}