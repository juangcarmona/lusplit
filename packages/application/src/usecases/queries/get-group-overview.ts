import {
  aggregateBalancesByEconomicUnitOwner,
  calculateParticipantBalances,
  planSettlement
} from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { GroupOverviewModel } from '../../models/queries/group-overview-model';
import { GetGroupOverviewInput } from '../../models/queries/get-group-overview-input';
import {
  mapBalancesToModel,
  mapEconomicUnitToModel,
  mapExpenseToModel,
  mapGroupToModel,
  mapParticipantToModel,
  mapSettlementTransfersToModel,
  mapTransferToModel
} from '../../mappers/entity-mappers';
import { EconomicUnitRepository } from '../../ports/economic-unit-repository';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { ParticipantRepository } from '../../ports/participant-repository';
import { TransferRepository } from '../../ports/transfer-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class GetGroupOverviewUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly economicUnitRepository: EconomicUnitRepository,
    private readonly expenseRepository: ExpenseRepository,
    private readonly transferRepository: TransferRepository
  ) {}

  async execute(input: GetGroupOverviewInput, _authContext?: AuthContext): Promise<GroupOverviewModel> {
    assertNonEmpty(input.groupId, 'groupId');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const economicUnits = await this.economicUnitRepository.listByGroupId(input.groupId);
    const expenses = await this.expenseRepository.listByGroupId(input.groupId);
    const transfers = await this.transferRepository.listByGroupId(input.groupId);

    const balancesByParticipant = calculateParticipantBalances(expenses, participants);
    const balancesByEconomicUnitOwner = aggregateBalancesByEconomicUnitOwner(
      balancesByParticipant,
      participants,
      economicUnits
    );

    return {
      group: mapGroupToModel(group),
      summary: {
        groupId: input.groupId,
        participantCount: participants.length,
        economicUnitCount: economicUnits.length,
        expenseCount: expenses.length,
        transferCount: transfers.length
      },
      participants: participants.map(mapParticipantToModel),
      economicUnits: economicUnits.map(mapEconomicUnitToModel),
      expenses: expenses.map(mapExpenseToModel),
      transfers: transfers.map(mapTransferToModel),
      balancesByParticipant: mapBalancesToModel(balancesByParticipant),
      balancesByEconomicUnitOwner: mapBalancesToModel(balancesByEconomicUnitOwner),
      settlementByParticipant: {
        mode: 'PARTICIPANT',
        transfers: mapSettlementTransfersToModel(planSettlement(balancesByParticipant))
      },
      settlementByEconomicUnitOwner: {
        mode: 'ECONOMIC_UNIT_OWNER',
        transfers: mapSettlementTransfersToModel(planSettlement(balancesByEconomicUnitOwner))
      }
    };
  }
}
