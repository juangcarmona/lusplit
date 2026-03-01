import { DatabaseSync } from 'node:sqlite';

export class SqliteTransactionRunner {
  private depth = 0;

  constructor(private readonly db: DatabaseSync) {}

  async runInTransaction<T>(fn: () => Promise<T>): Promise<T> {
    this.depth += 1;
    const isOutermost = this.depth === 1;
    if (isOutermost) {
      this.db.exec('BEGIN');
    }

    try {
      const result = await fn();
      if (isOutermost) {
        this.db.exec('COMMIT');
      }
      return result;
    } catch (error) {
      if (isOutermost) {
        this.db.exec('ROLLBACK');
      }
      throw error;
    } finally {
      this.depth -= 1;
    }
  }
}
