import { aggregateBalancesByEconomicUnitOwner, calculateParticipantBalances } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { BalanceModel } from '../../models/common/balance-model';
import { GetBalancesByEconomicUnitOwnerInput } from '../../models/queries/get-balances-by-economic-unit-owner-input';
import { mapBalancesToModel } from '../../mappers/entity-mappers';
import { EconomicUnitRepository } from '../../ports/economic-unit-repository';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { ParticipantRepository } from '../../ports/participant-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class GetBalancesByEconomicUnitOwnerUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly economicUnitRepository: EconomicUnitRepository,
    private readonly expenseRepository: ExpenseRepository
  ) {}

  async execute(input: GetBalancesByEconomicUnitOwnerInput, _authContext?: AuthContext): Promise<BalanceModel[]> {
    assertNonEmpty(input.groupId, 'groupId');
    await getRequiredGroup(this.groupRepository, input.groupId);

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const economicUnits = await this.economicUnitRepository.listByGroupId(input.groupId);
    const expenses = await this.expenseRepository.listByGroupId(input.groupId);
    const balances = calculateParticipantBalances(expenses, participants);

    return mapBalancesToModel(aggregateBalancesByEconomicUnitOwner(balances, participants, economicUnits));
  }
}
