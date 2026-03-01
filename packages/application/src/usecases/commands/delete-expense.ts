import { AuthContext } from '../../models/common/auth-context';
import { DeleteExpenseInput } from '../../models/commands/delete-expense-input';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { NotFoundError } from '../../errors';
import { assertGroupOpen, assertNonEmpty, getRequiredGroup } from '../common';

export class DeleteExpenseUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly expenseRepository: ExpenseRepository
  ) {}

  async execute(input: DeleteExpenseInput, _authContext?: AuthContext): Promise<void> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.expenseId, 'expenseId');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const expense = await this.expenseRepository.getById(input.expenseId);
    if (!expense || String(expense.groupId) !== input.groupId) {
      throw new NotFoundError(`Expense not found: ${input.expenseId}`);
    }

    await this.expenseRepository.delete(input.groupId, input.expenseId);
  }
}
