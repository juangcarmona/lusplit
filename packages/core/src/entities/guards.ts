import { DomainError } from '../errors/domain-error';

import { EconomicUnit, Participant } from './index';

export const assertGroupScoped = (
  scoped: { groupId: Participant['groupId'] },
  participants: Participant[],
  economicUnits?: EconomicUnit[]
): void => {
  for (const participant of participants) {
    if (participant.groupId !== scoped.groupId) {
      throw new DomainError(`Participant ${participant.id} is not in group ${scoped.groupId}`);
    }
  }

  if (!economicUnits) {
    return;
  }

  for (const economicUnit of economicUnits) {
    if (economicUnit.groupId !== scoped.groupId) {
      throw new DomainError(`EconomicUnit ${economicUnit.id} is not in group ${scoped.groupId}`);
    }
  }
};
