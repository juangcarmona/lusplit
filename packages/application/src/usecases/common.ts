import { Group, assertMinorUnits } from '@lusplit/core';

import { NotFoundError, ValidationError } from '../errors';
import { Clock } from '../ports/clock';
import { GroupRepository } from '../ports/group-repository';

export const assertNonEmpty = (value: string, fieldName: string): void => {
  if (!value || value.trim().length === 0) {
    throw new ValidationError(`${fieldName} is required`);
  }
};

export const assertIsoDate = (value: string, fieldName: string): void => {
  if (Number.isNaN(Date.parse(value))) {
    throw new ValidationError(`${fieldName} must be a valid ISO date`);
  }
};

export const assertPositiveMinor = (value: number, fieldName: string): void => {
  assertMinorUnits(value, fieldName);
  if (value <= 0) {
    throw new ValidationError(`${fieldName} must be greater than zero`);
  }
};

export const resolveDateIso = (date: string | undefined, clock: Clock): string => {
  const resolved = date ?? clock.nowIso();
  assertIsoDate(resolved, 'date');
  return resolved;
};

export const getRequiredGroup = async (groupRepository: GroupRepository, groupId: string): Promise<Group> => {
  const group = await groupRepository.getById(groupId);
  if (!group) {
    throw new NotFoundError(`Group not found: ${groupId}`);
  }

  return group;
};

export const assertGroupOpen = (group: Group): void => {
  if (group.closed) {
    throw new ValidationError(`Group is closed: ${group.id}`);
  }
};
