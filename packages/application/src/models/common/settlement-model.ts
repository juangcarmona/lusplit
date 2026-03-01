export interface SettlementTransferModel {
  fromParticipantId: string;
  toParticipantId: string;
  amountMinor: number;
}

export interface SettlementPlanModel {
  mode: 'PARTICIPANT' | 'ECONOMIC_UNIT_OWNER';
  transfers: SettlementTransferModel[];
}
