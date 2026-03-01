import { Expense, Participant } from '../entities';
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
  participants: Participant[]
): Map<ParticipantId, number> => {
  const ownerByUnit = new Map<Participant['economicUnitId'], ParticipantId>();
  for (const participant of participants) {
    if (!ownerByUnit.has(participant.economicUnitId)) {
      ownerByUnit.set(participant.economicUnitId, participant.id);
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
