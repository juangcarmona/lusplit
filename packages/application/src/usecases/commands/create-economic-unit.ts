import { EconomicUnit, asEconomicUnitId, asGroupId, asParticipantId } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { EconomicUnitModel } from '../../models/common/entities-model';
import { CreateEconomicUnitInput } from '../../models/commands/create-economic-unit-input';
import { mapEconomicUnitToModel } from '../../mappers/entity-mappers';
import { EconomicUnitRepository } from '../../ports/economic-unit-repository';
import { GroupRepository } from '../../ports/group-repository';
import { IdGenerator } from '../../ports/id-generator';
import { assertGroupOpen, assertNonEmpty, getRequiredGroup } from '../common';

export class CreateEconomicUnitUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly economicUnitRepository: EconomicUnitRepository,
    private readonly idGenerator: IdGenerator
  ) {}

  async execute(input: CreateEconomicUnitInput, _authContext?: AuthContext): Promise<EconomicUnitModel> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.ownerParticipantId, 'ownerParticipantId');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const economicUnit: EconomicUnit = {
      id: asEconomicUnitId(this.idGenerator.nextId()),
      groupId: asGroupId(input.groupId),
      ownerParticipantId: asParticipantId(input.ownerParticipantId),
      name: input.name
    };

    await this.economicUnitRepository.save(economicUnit);

    return mapEconomicUnitToModel(economicUnit);
  }
}
