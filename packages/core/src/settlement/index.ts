import { ParticipantId } from '../ids';
import { DomainError } from '../errors/domain-error';

export interface SettlementTransfer {
  fromParticipantId: ParticipantId;
  toParticipantId: ParticipantId;
  amountMinor: number;
}

interface Bucket {
  participantId: ParticipantId;
  amountMinor: number;
}

const sortByParticipantId = (left: Bucket, right: Bucket): number =>
  String(left.participantId).localeCompare(String(right.participantId));

export const planSettlement = (balances: Map<ParticipantId, number>): SettlementTransfer[] => {
  const sum = [...balances.values()].reduce((acc, value) => acc + value, 0);
  if (sum !== 0) {
    throw new DomainError(`Settlement invariant violated: sum=${sum}`);
  }

  const creditors: Bucket[] = [...balances.entries()]
    .filter(([, balance]) => balance > 0)
    .map(([participantId, amountMinor]) => ({ participantId, amountMinor }))
    .sort(sortByParticipantId);

  const debtors: Bucket[] = [...balances.entries()]
    .filter(([, balance]) => balance < 0)
    .map(([participantId, amountMinor]) => ({ participantId, amountMinor: -amountMinor }))
    .sort(sortByParticipantId);

  const transfers: SettlementTransfer[] = [];
  let creditorIndex = 0;
  let debtorIndex = 0;

  while (creditorIndex < creditors.length && debtorIndex < debtors.length) {
    const creditor = creditors[creditorIndex];
    const debtor = debtors[debtorIndex];
    const amountMinor = Math.min(creditor.amountMinor, debtor.amountMinor);

    if (amountMinor > 0) {
      transfers.push({
        fromParticipantId: debtor.participantId,
        toParticipantId: creditor.participantId,
        amountMinor
      });
    }

    creditor.amountMinor -= amountMinor;
    debtor.amountMinor -= amountMinor;

    if (creditor.amountMinor === 0) {
      creditorIndex += 1;
    }

    if (debtor.amountMinor === 0) {
      debtorIndex += 1;
    }
  }

  return transfers;
};
