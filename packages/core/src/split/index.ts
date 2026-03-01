import { Participant } from '../entities';
import { DomainError } from '../errors/domain-error';
import { ParticipantId } from '../ids';
import { assertMinorUnits } from '../money';

export type RemainderMode = 'EQUAL' | 'WEIGHT' | 'PERCENT';
export type ParticipantKeyed<T> = Partial<Record<ParticipantId, T>>;

export interface FixedContribution {
  type: 'FIXED';
  shares: ParticipantKeyed<number>;
}

export interface RemainderDistribution {
  type: 'REMAINDER';
  participants: ParticipantId[];
  mode: RemainderMode;
  weights?: ParticipantKeyed<string>;
  percents?: ParticipantKeyed<number>;
}

export type SplitComponent = FixedContribution | RemainderDistribution;

export interface SplitDefinition {
  components: SplitComponent[];
}

const sortParticipantIds = (participantIds: ParticipantId[]): ParticipantId[] =>
  [...participantIds].sort((left, right) => String(left).localeCompare(String(right)));

const parseScaledWeight = (value: string): bigint => {
  if (!/^(0|[1-9]\d*)(\.\d+)?$/.test(value)) {
    throw new DomainError(`Invalid weight value: ${value}`);
  }

  const [integerPart, fractionalPart = ''] = value.split('.');
  if (fractionalPart.length > 6) {
    throw new DomainError(`Weight precision must be <= 6 decimals: ${value}`);
  }

  const fractional = `${fractionalPart}${'0'.repeat(6 - fractionalPart.length)}`;
  const scaled = BigInt(integerPart) * 1_000_000n + BigInt(fractional || '0');

  if (scaled <= 0n) {
    throw new DomainError(`Weight must be > 0: ${value}`);
  }

  return scaled;
};

const allocateByWeights = (
  totalMinor: number,
  sortedParticipants: ParticipantId[],
  weightByParticipant: Map<ParticipantId, bigint>
): Map<ParticipantId, number> => {
  const allocations = new Map<ParticipantId, number>();
  const totalWeight = [...weightByParticipant.values()].reduce((acc, value) => acc + value, 0n);

  if (totalWeight <= 0n && totalMinor !== 0) {
    throw new DomainError('Total weight must be > 0');
  }

  let allocated = 0;
  const remainders: { participantId: ParticipantId; remainder: bigint }[] = [];

  for (const participantId of sortedParticipants) {
    const weight = weightByParticipant.get(participantId) ?? 0n;
    const numerator = BigInt(totalMinor) * weight;
    const base = totalWeight === 0n ? 0 : Number(numerator / totalWeight);
    const remainder = totalWeight === 0n ? 0n : numerator % totalWeight;

    allocations.set(participantId, base);
    allocated += base;
    remainders.push({ participantId, remainder });
  }

  let leftover = totalMinor - allocated;

  remainders.sort((left, right) => {
    if (left.remainder === right.remainder) {
      return String(left.participantId).localeCompare(String(right.participantId));
    }

    return left.remainder > right.remainder ? -1 : 1;
  });

  for (let index = 0; index < remainders.length && leftover > 0; index += 1) {
    const participantId = remainders[index].participantId;
    allocations.set(participantId, (allocations.get(participantId) ?? 0) + 1);
    leftover -= 1;
  }

  if (leftover !== 0) {
    throw new DomainError('Failed to allocate remainder deterministically');
  }

  return allocations;
};

const toParticipantEntries = <T>(values: ParticipantKeyed<T>): [ParticipantId, T][] =>
  Object.entries(values) as [ParticipantId, T][];

const deriveWeightForParticipant = (
  participant: Participant,
  explicitWeight: string | undefined
): bigint => {
  if (explicitWeight !== undefined) {
    return parseScaledWeight(explicitWeight);
  }

  if (participant.consumptionCategory === 'FULL') {
    return parseScaledWeight('1');
  }

  if (participant.consumptionCategory === 'HALF') {
    return parseScaledWeight('0.5');
  }

  if (!participant.customConsumptionWeight) {
    throw new DomainError(`Missing customConsumptionWeight for participant ${participant.id}`);
  }

  return parseScaledWeight(participant.customConsumptionWeight);
};

const assertKnownParticipant = (
  participantId: ParticipantId,
  participantMap: Map<ParticipantId, Participant>
): void => {
  if (!participantMap.has(participantId)) {
    throw new DomainError(`Unknown participant ${participantId}`);
  }
};

const assertUniqueParticipants = (participantIds: ParticipantId[]): void => {
  const unique = new Set(participantIds);
  if (unique.size !== participantIds.length) {
    throw new DomainError('Duplicate participants are not allowed in a remainder component');
  }
};

export const evaluateSplit = (
  expense: { amountMinor: number; splitDefinition: SplitDefinition },
  participants: Participant[]
): Map<ParticipantId, number> => {
  assertMinorUnits(expense.amountMinor, 'expense.amountMinor');

  const participantMap = new Map<ParticipantId, Participant>(participants.map((participant) => [participant.id, participant]));
  const shares = new Map<ParticipantId, number>(participants.map((participant) => [participant.id, 0]));
  let remaining = expense.amountMinor;

  for (const component of expense.splitDefinition.components) {
    if (component.type === 'FIXED') {
      let assigned = 0;

      for (const [participantId, amount] of toParticipantEntries(component.shares)) {
        assertKnownParticipant(participantId, participantMap);
        assertMinorUnits(amount, `fixedShare(${participantId})`);

        if (amount < 0) {
          throw new DomainError('Fixed share must be >= 0');
        }

        assigned += amount;
        shares.set(participantId, (shares.get(participantId) ?? 0) + amount);
      }

      if (assigned > remaining) {
        throw new DomainError('Fixed shares exceed remaining amount');
      }

      remaining -= assigned;
      continue;
    }

    const remainderParticipants = sortParticipantIds(component.participants);
    assertUniqueParticipants(remainderParticipants);

    for (const participantId of remainderParticipants) {
      assertKnownParticipant(participantId, participantMap);
    }

    if (component.mode === 'PERCENT') {
      if (!component.percents) {
        throw new DomainError('PERCENT mode requires percents');
      }

      let percentSum = 0;
      const weightByParticipant = new Map<ParticipantId, bigint>();

      for (const participantId of remainderParticipants) {
        const percent = component.percents[participantId];
        if (percent === undefined || !Number.isInteger(percent) || percent < 0) {
          throw new DomainError(`Invalid percent for ${participantId}`);
        }

        percentSum += percent;
        weightByParticipant.set(participantId, BigInt(percent));
      }

      if (percentSum !== 100) {
        throw new DomainError(`Percent sum must be exactly 100, got ${percentSum}`);
      }

      const allocations = allocateByWeights(remaining, remainderParticipants, weightByParticipant);
      for (const [participantId, amount] of allocations.entries()) {
        shares.set(participantId, (shares.get(participantId) ?? 0) + amount);
      }

      remaining = 0;
      continue;
    }

    const weightByParticipant = new Map<ParticipantId, bigint>();

    for (const participantId of remainderParticipants) {
      if (component.mode === 'EQUAL') {
        weightByParticipant.set(participantId, 1n);
        continue;
      }

      const participant = participantMap.get(participantId);
      if (!participant) {
        throw new DomainError(`Unknown participant ${participantId}`);
      }

      const explicitWeight = component.weights?.[participantId];
      weightByParticipant.set(participantId, deriveWeightForParticipant(participant, explicitWeight));
    }

    const allocations = allocateByWeights(remaining, remainderParticipants, weightByParticipant);

    for (const [participantId, amount] of allocations.entries()) {
      shares.set(participantId, (shares.get(participantId) ?? 0) + amount);
    }

    remaining = 0;
  }

  if (remaining !== 0) {
    throw new DomainError(`Split definition did not consume full amount. Remaining=${remaining}`);
  }

  return shares;
};
