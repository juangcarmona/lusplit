import { aggregateBalancesByEconomicUnitOwner, calculateParticipantBalances, planSettlement } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { SettlementPlanModel } from '../../models/common/settlement-model';
import { GetSettlementPlanInput } from '../../models/queries/get-settlement-plan-input';
import { mapSettlementTransfersToModel } from '../../mappers/entity-mappers';
import { EconomicUnitRepository } from '../../ports/economic-unit-repository';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { ParticipantRepository } from '../../ports/participant-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class GetSettlementPlanUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly economicUnitRepository: EconomicUnitRepository,
    private readonly expenseRepository: ExpenseRepository
  ) {}

  async execute(input: GetSettlementPlanInput, _authContext?: AuthContext): Promise<SettlementPlanModel> {
    assertNonEmpty(input.groupId, 'groupId');
    await getRequiredGroup(this.groupRepository, input.groupId);

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const expenses = await this.expenseRepository.listByGroupId(input.groupId);
    const participantBalances = calculateParticipantBalances(expenses, participants);

    const balances =
      input.mode === 'PARTICIPANT'
        ? participantBalances
        : aggregateBalancesByEconomicUnitOwner(
            participantBalances,
            participants,
            await this.economicUnitRepository.listByGroupId(input.groupId)
          );

    return {
      mode: input.mode,
      transfers: mapSettlementTransfersToModel(planSettlement(balances))
    };
  }
}
