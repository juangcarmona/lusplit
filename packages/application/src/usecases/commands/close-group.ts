import { AuthContext } from '../../models/common/auth-context';
import { GroupModel } from '../../models/common/entities-model';
import { CloseGroupInput } from '../../models/commands/close-group-input';
import { mapGroupToModel } from '../../mappers/entity-mappers';
import { GroupRepository } from '../../ports/group-repository';
import { assertNonEmpty, getRequiredGroup } from '../common';

export class CloseGroupUseCase {
  constructor(private readonly groupRepository: GroupRepository) {}

  async execute(input: CloseGroupInput, _authContext?: AuthContext): Promise<GroupModel> {
    assertNonEmpty(input.groupId, 'groupId');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    const closedGroup = { ...group, closed: true };

    await this.groupRepository.save(closedGroup);

    return mapGroupToModel(closedGroup);
  }
}
