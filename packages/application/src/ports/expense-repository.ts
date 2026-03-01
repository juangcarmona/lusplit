import { Expense } from '@lusplit/core';

export interface ExpenseRepository {
  getById(expenseId: string): Promise<Expense | null>;
  listByGroupId(groupId: string): Promise<Expense[]>;
  save(expense: Expense): Promise<void>;
  delete(groupId: string, expenseId: string): Promise<void>;
}
