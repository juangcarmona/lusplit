import { Participant } from '@lusplit/core';

export interface ParticipantRepository {
  getById(participantId: string): Promise<Participant | null>;
  listByGroupId(groupId: string): Promise<Participant[]>;
  save(participant: Participant): Promise<void>;
}
