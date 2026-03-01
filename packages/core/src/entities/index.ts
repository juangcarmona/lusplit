import { EconomicUnitId, ExpenseId, GroupId, ParticipantId, TransferId } from '../ids';
import { SplitDefinition } from '../split';

export type ConsumptionCategory = 'FULL' | 'HALF' | 'CUSTOM';

export interface Group {
  id: GroupId;
  currency: string;
  closed: boolean;
}

export interface Participant {
  id: ParticipantId;
  groupId: GroupId;
  economicUnitId: EconomicUnitId;
  name: string;
  consumptionCategory: ConsumptionCategory;
  customConsumptionWeight?: string;
}

export interface EconomicUnit {
  id: EconomicUnitId;
  groupId: GroupId;
  ownerParticipantId: ParticipantId;
  name?: string;
}

export interface Expense {
  id: ExpenseId;
  groupId: GroupId;
  title: string;
  paidByParticipantId: ParticipantId;
  amountMinor: number;
  date: string;
  splitDefinition: SplitDefinition;
  notes?: string;
}

export type TransferType = 'GENERATED' | 'MANUAL';

export interface Transfer {
  id: TransferId;
  groupId: GroupId;
  fromParticipantId: ParticipantId;
  toParticipantId: ParticipantId;
  amountMinor: number;
  date: string;
  type: TransferType;
  note?: string;
}
