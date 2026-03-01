export interface GroupRecord {
  id: string;
  currency: string;
  closed: boolean;
}

export interface EconomicUnitRecord {
  id: string;
  groupId: string;
  ownerParticipantId: string;
  name?: string;
}

export interface ParticipantRecord {
  id: string;
  groupId: string;
  economicUnitId: string;
  name: string;
  consumptionCategory: 'FULL' | 'HALF' | 'CUSTOM';
  customConsumptionWeight?: string;
}

export interface ExpenseRecord {
  id: string;
  groupId: string;
  title: string;
  paidByParticipantId: string;
  amountMinor: number;
  date: string;
}

export interface TransferRecord {
  id: string;
  groupId: string;
  fromParticipantId: string;
  toParticipantId: string;
  amountMinor: number;
  date: string;
  type: 'GENERATED' | 'MANUAL';
}
