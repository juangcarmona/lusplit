import { ConsumptionCategory, TransferType } from '@lusplit/core';

import { SplitDefinitionModel } from './split-model';

export interface GroupModel {
  id: string;
  currency: string;
  closed: boolean;
}

export interface ParticipantModel {
  id: string;
  groupId: string;
  economicUnitId: string;
  name: string;
  consumptionCategory: ConsumptionCategory;
  customConsumptionWeight?: string;
}

export interface EconomicUnitModel {
  id: string;
  groupId: string;
  ownerParticipantId: string;
  name?: string;
}

export interface ExpenseModel {
  id: string;
  groupId: string;
  title: string;
  paidByParticipantId: string;
  amountMinor: number;
  date: string;
  splitDefinition: SplitDefinitionModel;
  notes?: string;
}

export interface TransferModel {
  id: string;
  groupId: string;
  fromParticipantId: string;
  toParticipantId: string;
  amountMinor: number;
  date: string;
  type: TransferType;
  note?: string;
}
