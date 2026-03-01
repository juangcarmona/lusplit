import { AuthContext } from '../../models/common/auth-context';
import { ExpenseModel } from '../../models/common/entities-model';
import { GetExpensesInput } from '../../models/queries/get-expenses-input';
import { mapExpenseToModel } from '../../mappers/entity-mappers';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class GetExpensesUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly expenseRepository: ExpenseRepository
  ) {}

  async execute(input: GetExpensesInput, _authContext?: AuthContext): Promise<ExpenseModel[]> {
    assertNonEmpty(input.groupId, 'groupId');
    await getRequiredGroup(this.groupRepository, input.groupId);

    const expenses = await this.expenseRepository.listByGroupId(input.groupId);
    return expenses.map(mapExpenseToModel);
  }
}
