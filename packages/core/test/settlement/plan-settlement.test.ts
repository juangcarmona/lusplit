import test = require('node:test');
import assert = require('node:assert/strict');

import { DomainError, asParticipantId, planSettlement } from '../../src';

const a = asParticipantId('a');
const b = asParticipantId('b');
const c = asParticipantId('c');
const d = asParticipantId('d');

test('creates deterministic settlement transfers', () => {
  const balances = new Map([
    [a, 500],
    [b, 200],
    [c, -300],
    [d, -400]
  ]);

  const transfers = planSettlement(balances);

  assert.deepEqual(transfers, [
    { fromParticipantId: c, toParticipantId: a, amountMinor: 300 },
    { fromParticipantId: d, toParticipantId: a, amountMinor: 200 },
    { fromParticipantId: d, toParticipantId: b, amountMinor: 200 }
  ]);
});

test('throws when balances are not zero-sum', () => {
  assert.throws(() => planSettlement(new Map([[a, 1]])), DomainError);
});
