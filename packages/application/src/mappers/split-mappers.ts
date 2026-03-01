import { ParticipantId, SplitDefinition, asParticipantId } from '@lusplit/core';

import { SplitDefinitionModel } from '../models/common/split-model';

const toParticipantKeyedRecord = <T>(record: Record<string, T> | undefined): Partial<Record<ParticipantId, T>> => {
  const next: Partial<Record<ParticipantId, T>> = {};
  if (!record) {
    return next;
  }

  for (const [participantId, value] of Object.entries(record)) {
    next[asParticipantId(participantId)] = value;
  }

  return next;
};

const toStringRecord = <T>(record: Partial<Record<ParticipantId, T>> | undefined): Record<string, T> => {
  const next: Record<string, T> = {};
  if (!record) {
    return next;
  }

  for (const [participantId, value] of Object.entries(record)) {
    next[String(participantId)] = value as T;
  }

  return next;
};

export const splitDefinitionModelToCore = (model: SplitDefinitionModel): SplitDefinition => ({
  components: model.components.map((component) => {
    if (component.type === 'FIXED') {
      return {
        type: 'FIXED',
        shares: toParticipantKeyedRecord(component.shares)
      };
    }

    return {
      type: 'REMAINDER',
      participants: component.participants.map(asParticipantId),
      mode: component.mode,
      weights: toParticipantKeyedRecord(component.weights),
      percents: toParticipantKeyedRecord(component.percents)
    };
  })
});

export const splitDefinitionCoreToModel = (splitDefinition: SplitDefinition): SplitDefinitionModel => ({
  components: splitDefinition.components.map((component) => {
    if (component.type === 'FIXED') {
      return {
        type: 'FIXED',
        shares: toStringRecord(component.shares)
      };
    }

    return {
      type: 'REMAINDER',
      participants: component.participants.map(String),
      mode: component.mode,
      weights: component.weights ? toStringRecord(component.weights) : undefined,
      percents: component.percents ? toStringRecord(component.percents) : undefined
    };
  })
});
