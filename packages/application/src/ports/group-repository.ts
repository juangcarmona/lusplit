import { Group } from '@lusplit/core';

export interface GroupRepository {
  getById(groupId: string): Promise<Group | null>;
  save(group: Group): Promise<void>;
}
