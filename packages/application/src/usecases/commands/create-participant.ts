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
    const ownerAlreadyExists = groupParticipants.some(
      (participant) => participant.id === economicUnit.ownerParticipantId
    );

    const participant: Participant = {
      id: ownerAlreadyExists ? asParticipantId(this.idGenerator.nextId()) : economicUnit.ownerParticipantId,
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
