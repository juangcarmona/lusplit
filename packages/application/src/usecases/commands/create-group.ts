import { Group, asGroupId } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { GroupModel } from '../../models/common/entities-model';
import { CreateGroupInput } from '../../models/commands/create-group-input';
import { mapGroupToModel } from '../../mappers/entity-mappers';
import { IdGenerator } from '../../ports/id-generator';
import { GroupRepository } from '../../ports/group-repository';
import { assertNonEmpty } from '../common';

export class CreateGroupUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly idGenerator: IdGenerator
  ) {}

  async execute(input: CreateGroupInput, _authContext?: AuthContext): Promise<GroupModel> {
    assertNonEmpty(input.currency, 'currency');

    const group: Group = {
      id: asGroupId(this.idGenerator.nextId()),
      currency: input.currency,
      closed: false
    };

    await this.groupRepository.save(group);

    return mapGroupToModel(group);
  }
}
