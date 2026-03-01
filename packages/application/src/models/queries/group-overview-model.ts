import { BalanceModel } from '../common/balance-model';
import { EconomicUnitModel, ExpenseModel, GroupModel, ParticipantModel, TransferModel } from '../common/entities-model';
import { SettlementPlanModel } from '../common/settlement-model';
import { GroupSummaryModel } from './group-summary-model';

export interface GroupOverviewModel {
  group: GroupModel;
  summary: GroupSummaryModel;
  participants: ParticipantModel[];
  economicUnits: EconomicUnitModel[];
  expenses: ExpenseModel[];
  transfers: TransferModel[];
  balancesByParticipant: BalanceModel[];
  balancesByEconomicUnitOwner: BalanceModel[];
  settlementByParticipant: SettlementPlanModel;
  settlementByEconomicUnitOwner: SettlementPlanModel;
}
