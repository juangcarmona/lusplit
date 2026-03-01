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

const MAX_ID_GENERATION_ATTEMPTS = 100;

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
    assertNonEmpty(String(economicUnit.ownerParticipantId), 'ownerParticipantId');

    const ownerParticipantId = String(economicUnit.ownerParticipantId);
    const economicUnitId = String(economicUnit.id);
    const groupParticipants = await this.participantRepository.listByGroupId(input.groupId);
    const ownerExistsInGroup = groupParticipants.some(
      (participant) => String(participant.id) === ownerParticipantId
    );
    const hasParticipantsInEconomicUnit = groupParticipants.some(
      (participant) => String(participant.economicUnitId) === economicUnitId
    );
    const groupParticipantIds = new Set(groupParticipants.map((participant) => String(participant.id)));
    let participantId: string;
    if (!ownerExistsInGroup && !hasParticipantsInEconomicUnit) {
      participantId = ownerParticipantId;
    } else {
      let attempts = 0;
      participantId = this.idGenerator.nextId();
      while (groupParticipantIds.has(participantId) && attempts < MAX_ID_GENERATION_ATTEMPTS) {
        attempts += 1;
        participantId = this.idGenerator.nextId();
      }
      if (groupParticipantIds.has(participantId)) {
        throw new ValidationError('Unable to generate a unique participant id');
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

    await this.participantRepository.save(participant);

    return mapParticipantToModel(participant);
  }
}
