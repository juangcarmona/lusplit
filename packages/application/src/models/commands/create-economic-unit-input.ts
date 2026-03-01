export interface CreateEconomicUnitInput {
  groupId: string;
  // Owner participant may be created later; owner-mode queries require this participant to eventually exist in this unit.
  ownerParticipantId: string;
  name?: string;
}
