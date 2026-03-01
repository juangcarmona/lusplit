import {
  EconomicUnit,
  Expense,
  Group,
  Participant,
  SettlementTransfer,
  Transfer,
  asEconomicUnitId,
  asExpenseId,
  asGroupId,
  asParticipantId,
  asTransferId
} from '@lusplit/core';

import { BalanceModel } from '../models/common/balance-model';
import { EconomicUnitModel, ExpenseModel, GroupModel, ParticipantModel, TransferModel } from '../models/common/entities-model';
import { SettlementTransferModel } from '../models/common/settlement-model';
import { splitDefinitionCoreToModel, splitDefinitionModelToCore } from './split-mappers';

export const mapGroupToModel = (group: Group): GroupModel => ({
  id: String(group.id),
  currency: group.currency,
  closed: group.closed
});

export const mapParticipantToModel = (participant: Participant): ParticipantModel => ({
  id: String(participant.id),
  groupId: String(participant.groupId),
  economicUnitId: String(participant.economicUnitId),
  name: participant.name,
  consumptionCategory: participant.consumptionCategory,
  customConsumptionWeight: participant.customConsumptionWeight
});

export const mapEconomicUnitToModel = (economicUnit: EconomicUnit): EconomicUnitModel => ({
  id: String(economicUnit.id),
  groupId: String(economicUnit.groupId),
  ownerParticipantId: String(economicUnit.ownerParticipantId),
  name: economicUnit.name
});

export const mapExpenseToModel = (expense: Expense): ExpenseModel => ({
  id: String(expense.id),
  groupId: String(expense.groupId),
  title: expense.title,
  paidByParticipantId: String(expense.paidByParticipantId),
  amountMinor: expense.amountMinor,
  date: expense.date,
  splitDefinition: splitDefinitionCoreToModel(expense.splitDefinition),
  notes: expense.notes
});

export const mapTransferToModel = (transfer: Transfer): TransferModel => ({
  id: String(transfer.id),
  groupId: String(transfer.groupId),
  fromParticipantId: String(transfer.fromParticipantId),
  toParticipantId: String(transfer.toParticipantId),
  amountMinor: transfer.amountMinor,
  date: transfer.date,
  type: transfer.type,
  note: transfer.note
});

export const mapBalancesToModel = (balances: Map<any, number>): BalanceModel[] =>
  [...balances.entries()]
    .map(([participantId, amountMinor]) => ({ participantId: String(participantId), amountMinor }))
    .sort((left, right) => left.participantId.localeCompare(right.participantId));

export const mapSettlementTransfersToModel = (transfers: SettlementTransfer[]): SettlementTransferModel[] =>
  transfers.map((transfer) => ({
    fromParticipantId: String(transfer.fromParticipantId),
    toParticipantId: String(transfer.toParticipantId),
    amountMinor: transfer.amountMinor
  }));

export const mapGroupModelToCore = (group: GroupModel): Group => ({
  id: asGroupId(group.id),
  currency: group.currency,
  closed: group.closed
});

export const mapParticipantModelToCore = (participant: ParticipantModel): Participant => ({
  id: asParticipantId(participant.id),
  groupId: asGroupId(participant.groupId),
  economicUnitId: asEconomicUnitId(participant.economicUnitId),
  name: participant.name,
  consumptionCategory: participant.consumptionCategory,
  customConsumptionWeight: participant.customConsumptionWeight
});

export const mapEconomicUnitModelToCore = (economicUnit: EconomicUnitModel): EconomicUnit => ({
  id: asEconomicUnitId(economicUnit.id),
  groupId: asGroupId(economicUnit.groupId),
  ownerParticipantId: asParticipantId(economicUnit.ownerParticipantId),
  name: economicUnit.name
});

export const mapExpenseModelToCore = (expense: ExpenseModel): Expense => ({
  id: asExpenseId(expense.id),
  groupId: asGroupId(expense.groupId),
  title: expense.title,
  paidByParticipantId: asParticipantId(expense.paidByParticipantId),
  amountMinor: expense.amountMinor,
  date: expense.date,
  splitDefinition: splitDefinitionModelToCore(expense.splitDefinition),
  notes: expense.notes
});

export const mapTransferModelToCore = (transfer: TransferModel): Transfer => ({
  id: asTransferId(transfer.id),
  groupId: asGroupId(transfer.groupId),
  fromParticipantId: asParticipantId(transfer.fromParticipantId),
  toParticipantId: asParticipantId(transfer.toParticipantId),
  amountMinor: transfer.amountMinor,
  date: transfer.date,
  type: transfer.type,
  note: transfer.note
});
