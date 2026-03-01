export interface AddManualTransferInput {
  groupId: string;
  fromParticipantId: string;
  toParticipantId: string;
  amountMinor: number;
  date?: string;
  note?: string;
}
