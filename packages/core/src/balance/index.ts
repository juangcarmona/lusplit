import { EconomicUnit, Expense, Participant } from '../entities';
import { assertGroupScoped } from '../entities/guards';
import { DomainError } from '../errors/domain-error';
import { ParticipantId } from '../ids';
import { assertMinorUnits } from '../money';
import { evaluateSplit } from '../split';

export const calculateParticipantBalances = (
  expenses: Expense[],
  participants: Participant[]
): Map<ParticipantId, number> => {
  const balances = new Map<ParticipantId, number>(participants.map((participant) => [participant.id, 0]));

  for (const expense of expenses) {
    assertGroupScoped(expense, participants);
    assertMinorUnits(expense.amountMinor, 'expense.amountMinor');

    if (!balances.has(expense.paidByParticipantId)) {
      throw new DomainError(`Unknown payer ${expense.paidByParticipantId}`);
    }

    balances.set(
      expense.paidByParticipantId,
      (balances.get(expense.paidByParticipantId) ?? 0) + expense.amountMinor
    );

    const shares = evaluateSplit(expense, participants);

    for (const [participantId, share] of shares.entries()) {
      balances.set(participantId, (balances.get(participantId) ?? 0) - share);
    }
  }

  const sum = [...balances.values()].reduce((total, value) => total + value, 0);
  if (sum !== 0) {
    throw new DomainError(`Balance invariant violated: sum=${sum}`);
  }

  return balances;
};

export const aggregateBalancesByEconomicUnitOwner = (
  balances: Map<ParticipantId, number>,
  participants: Participant[],
  economicUnits: EconomicUnit[]
): Map<ParticipantId, number> => {
  if (participants.length === 0 && economicUnits.length === 0) {
    if (balances.size === 0) {
      return new Map();
    }

    throw new DomainError('Cannot aggregate balances without participants or economic units');
  }

  let scopedGroupId: Participant['groupId'];
  if (participants.length > 0) {
    scopedGroupId = participants[0].groupId;
  } else {
    scopedGroupId = economicUnits[0].groupId;
  }
  assertGroupScoped({ groupId: scopedGroupId }, participants, economicUnits);

  const participantsById = new Map(participants.map((participant) => [participant.id, participant]));
  const ownerByUnit = new Map<EconomicUnit['id'], ParticipantId>();
  for (const economicUnit of economicUnits) {
    ownerByUnit.set(economicUnit.id, economicUnit.ownerParticipantId);

    const owner = participantsById.get(economicUnit.ownerParticipantId);
    if (!owner) {
      throw new DomainError(`Economic unit owner is not a participant: ${economicUnit.ownerParticipantId}`);
    }

    if (owner.economicUnitId !== economicUnit.id) {
      throw new DomainError(`Economic unit owner must belong to its own unit: ${economicUnit.id}`);
    }
  }

  const unitByParticipant = new Map(participants.map((participant) => [participant.id, participant.economicUnitId]));
  const aggregated = new Map<ParticipantId, number>();

  for (const [participantId, balance] of balances.entries()) {
    const economicUnitId = unitByParticipant.get(participantId);
    if (!economicUnitId) {
      throw new DomainError(`Unknown participant in balances: ${participantId}`);
    }

    const ownerId = ownerByUnit.get(economicUnitId);
    if (!ownerId) {
      throw new DomainError(`Economic unit without owner: ${String(economicUnitId)}`);
    }

    aggregated.set(ownerId, (aggregated.get(ownerId) ?? 0) + balance);
  }

  return aggregated;
};
