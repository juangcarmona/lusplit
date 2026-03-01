import { ConsumptionCategory } from '@lusplit/core';

export interface CreateParticipantInput {
  groupId: string;
  economicUnitId: string;
  name: string;
  consumptionCategory: ConsumptionCategory;
  customConsumptionWeight?: string;
}
