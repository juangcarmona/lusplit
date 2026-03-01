import { Transfer } from '@lusplit/core';

export interface TransferRepository {
  listByGroupId(groupId: string): Promise<Transfer[]>;
  save(transfer: Transfer): Promise<void>;
}
