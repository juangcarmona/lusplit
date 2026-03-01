import { DatabaseSync } from 'node:sqlite';

export class SqliteTransactionRunner {
  private depth = 0;

  constructor(private readonly db: DatabaseSync) {}

  async runInTransaction<T>(fn: () => Promise<T>): Promise<T> {
    if (this.depth > 0) {
      return fn();
    }

    this.db.exec('BEGIN');
    this.depth += 1;

    try {
      const result = await fn();
      this.db.exec('COMMIT');
      return result;
    } catch (error) {
      this.db.exec('ROLLBACK');
      throw error;
    } finally {
      this.depth -= 1;
    }
  }
}
