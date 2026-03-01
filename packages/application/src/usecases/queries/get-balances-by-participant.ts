import { calculateParticipantBalances } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { BalanceModel } from '../../models/common/balance-model';
import { GetBalancesByParticipantInput } from '../../models/queries/get-balances-by-participant-input';
import { mapBalancesToModel } from '../../mappers/entity-mappers';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { ParticipantRepository } from '../../ports/participant-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class GetBalancesByParticipantUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly expenseRepository: ExpenseRepository
  ) {}

  async execute(input: GetBalancesByParticipantInput, _authContext?: AuthContext): Promise<BalanceModel[]> {
    assertNonEmpty(input.groupId, 'groupId');
    await getRequiredGroup(this.groupRepository, input.groupId);

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const expenses = await this.expenseRepository.listByGroupId(input.groupId);
    return mapBalancesToModel(calculateParticipantBalances(expenses, participants));
  }
}
