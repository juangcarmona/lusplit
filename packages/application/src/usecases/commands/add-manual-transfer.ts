import { Transfer, asGroupId, asParticipantId, asTransferId } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { TransferModel } from '../../models/common/entities-model';
import { AddManualTransferInput } from '../../models/commands/add-manual-transfer-input';
import { mapTransferToModel } from '../../mappers/entity-mappers';
import { Clock } from '../../ports/clock';
import { GroupRepository } from '../../ports/group-repository';
import { IdGenerator } from '../../ports/id-generator';
import { ParticipantRepository } from '../../ports/participant-repository';
import { TransferRepository } from '../../ports/transfer-repository';
import { ValidationError } from '../../errors';
import { assertGroupOpen, assertNonEmpty, assertPositiveMinor, getRequiredGroup, resolveDateIso } from '../common';

export class AddManualTransferUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly transferRepository: TransferRepository,
    private readonly idGenerator: IdGenerator,
    private readonly clock: Clock
  ) {}

  async execute(input: AddManualTransferInput, _authContext?: AuthContext): Promise<TransferModel> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.fromParticipantId, 'fromParticipantId');
    assertNonEmpty(input.toParticipantId, 'toParticipantId');
    assertPositiveMinor(input.amountMinor, 'amountMinor');

    if (input.fromParticipantId === input.toParticipantId) {
      throw new ValidationError('fromParticipantId and toParticipantId must be different');
    }

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const participantIds = new Set(participants.map((participant) => String(participant.id)));
    if (!participantIds.has(input.fromParticipantId) || !participantIds.has(input.toParticipantId)) {
      throw new ValidationError(`Transfer participants must belong to group ${input.groupId}`);
    }

    const transfer: Transfer = {
      id: asTransferId(this.idGenerator.nextId()),
      groupId: asGroupId(input.groupId),
      fromParticipantId: asParticipantId(input.fromParticipantId),
      toParticipantId: asParticipantId(input.toParticipantId),
      amountMinor: input.amountMinor,
      date: resolveDateIso(input.date, this.clock),
      type: 'MANUAL',
      note: input.note
    };

    await this.transferRepository.save(transfer);

    return mapTransferToModel(transfer);
  }
}
