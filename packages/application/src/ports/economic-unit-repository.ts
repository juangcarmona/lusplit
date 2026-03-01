import { EconomicUnit } from '@lusplit/core';

export interface EconomicUnitRepository {
  getById(economicUnitId: string): Promise<EconomicUnit | null>;
  listByGroupId(groupId: string): Promise<EconomicUnit[]>;
  save(economicUnit: EconomicUnit): Promise<void>;
}
