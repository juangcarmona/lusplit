import { Participant, asEconomicUnitId, asGroupId, asParticipantId } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { ParticipantModel } from '../../models/common/entities-model';
import { CreateParticipantInput } from '../../models/commands/create-participant-input';
import { mapParticipantToModel } from '../../mappers/entity-mappers';
import { EconomicUnitRepository } from '../../ports/economic-unit-repository';
import { GroupRepository } from '../../ports/group-repository';
import { IdGenerator } from '../../ports/id-generator';
import { ParticipantRepository } from '../../ports/participant-repository';
import { ValidationError } from '../../errors';
import { assertGroupOpen, assertNonEmpty, getRequiredGroup } from '../common';

export class CreateParticipantUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly economicUnitRepository: EconomicUnitRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly idGenerator: IdGenerator
  ) {}

  async execute(input: CreateParticipantInput, _authContext?: AuthContext): Promise<ParticipantModel> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.economicUnitId, 'economicUnitId');
    assertNonEmpty(input.name, 'name');

    if (input.consumptionCategory === 'CUSTOM' && !input.customConsumptionWeight) {
      throw new ValidationError('customConsumptionWeight is required for CUSTOM consumptionCategory');
    }

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const economicUnit = await this.economicUnitRepository.getById(input.economicUnitId);
    if (!economicUnit || String(economicUnit.groupId) !== input.groupId) {
      throw new ValidationError(`Economic unit is not in group ${input.groupId}`);
    }

    const groupParticipants = await this.participantRepository.listByGroupId(input.groupId);
    const ownerExistsInGroup = groupParticipants.some(
      (participant) => String(participant.id) === String(economicUnit.ownerParticipantId)
    );
    const hasParticipantsInEconomicUnit = groupParticipants.some(
      (participant) => String(participant.economicUnitId) === String(economicUnit.id)
    );
    const groupParticipantIds = new Set(groupParticipants.map((participant) => String(participant.id)));
    let participantId: string;
    if (!ownerExistsInGroup && !hasParticipantsInEconomicUnit) {
      participantId = String(economicUnit.ownerParticipantId);
    } else {
      participantId = this.idGenerator.nextId();
      while (groupParticipantIds.has(participantId)) {
        participantId = this.idGenerator.nextId();
      }
    }

    const participant: Participant = {
      id: asParticipantId(participantId),
      groupId: asGroupId(input.groupId),
      economicUnitId: asEconomicUnitId(input.economicUnitId),
      name: input.name,
      consumptionCategory: input.consumptionCategory,
      customConsumptionWeight: input.customConsumptionWeight
    };

    if (String(participant.id) === String(economicUnit.ownerParticipantId)) {
      if (String(participant.economicUnitId) !== String(economicUnit.id)) {
        throw new ValidationError(`Owner participant must belong to economic unit ${economicUnit.id}`);
      }
      if (String(participant.groupId) !== String(economicUnit.groupId)) {
        throw new ValidationError(`Owner participant must belong to group ${economicUnit.groupId}`);
      }
    }

    await this.participantRepository.save(participant);

    return mapParticipantToModel(participant);
  }
}
