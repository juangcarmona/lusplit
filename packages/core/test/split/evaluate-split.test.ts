import test = require('node:test');
import assert = require('node:assert/strict');

import { DomainError, Participant, asEconomicUnitId, asGroupId, asParticipantId, evaluateSplit } from '../../src';

const groupId = asGroupId('g1');
const participantA = asParticipantId('a');
const participantB = asParticipantId('b');
const participantC = asParticipantId('c');

const participants: Participant[] = [
  {
    id: participantA,
    groupId,
    name: 'A',
    economicUnitId: asEconomicUnitId('u1'),
    consumptionCategory: 'FULL'
  },
  {
    id: participantB,
    groupId,
    name: 'B',
    economicUnitId: asEconomicUnitId('u2'),
    consumptionCategory: 'HALF'
  },
  {
    id: participantC,
    groupId,
    name: 'C',
    economicUnitId: asEconomicUnitId('u3'),
    consumptionCategory: 'FULL'
  }
];

test('evaluates FIXED then REMAINDER sequentially', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 1_000,
      splitDefinition: {
        components: [
          { type: 'FIXED', shares: { [participantA]: 300 } },
          { type: 'REMAINDER', participants: [participantB, participantC], mode: 'EQUAL' }
        ]
      }
    },
    participants
  );

  assert.equal(shares.get(participantA), 300);
  assert.equal(shares.get(participantB), 350);
  assert.equal(shares.get(participantC), 350);
  assert.equal([...shares.values()].reduce((sum, value) => sum + value, 0), 1_000);
});

test('uses deterministic rounding by participant ordering', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 10,
      splitDefinition: {
        components: [{ type: 'REMAINDER', participants: [participantC, participantA, participantB], mode: 'EQUAL' }]
      }
    },
    participants
  );

  assert.equal(shares.get(participantA), 4);
  assert.equal(shares.get(participantB), 3);
  assert.equal(shares.get(participantC), 3);
});

test('supports WEIGHT mode from participant categories and explicit weights', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 7,
      splitDefinition: {
        components: [
          {
            type: 'REMAINDER',
            participants: [participantA, participantB],
            mode: 'WEIGHT'
          }
        ]
      }
    },
    participants
  );

  assert.equal(shares.get(participantA), 5);
  assert.equal(shares.get(participantB), 2);
});

test('supports PERCENT mode and consumes full remainder', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 101,
      splitDefinition: {
        components: [
          {
            type: 'REMAINDER',
            participants: [participantA, participantB],
            mode: 'PERCENT',
            percents: { [participantA]: 50, [participantB]: 50 }
          }
        ]
      }
    },
    participants
  );

  assert.equal(shares.get(participantA), 51);
  assert.equal(shares.get(participantB), 50);
});

test('single participant can consume all remainder', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 250,
      splitDefinition: {
        components: [{ type: 'REMAINDER', participants: [participantA], mode: 'EQUAL' }]
      }
    },
    participants
  );

  assert.equal(shares.get(participantA), 250);
  assert.equal(shares.get(participantB), 0);
  assert.equal(shares.get(participantC), 0);
});

test('throws if split does not consume full expense amount', () => {
  assert.throws(
    () =>
      evaluateSplit(
        {
          groupId,
          amountMinor: 100,
          splitDefinition: { components: [] }
        },
        participants
      ),
    DomainError
  );
});

test('handles empty participants with zero amount', () => {
  const shares = evaluateSplit(
    {
      groupId,
      amountMinor: 0,
      splitDefinition: { components: [] }
    },
    []
  );

  assert.equal(shares.size, 0);
});

test('throws when participants are duplicated in REMAINDER', () => {
  assert.throws(
    () =>
      evaluateSplit(
        {
          groupId,
          amountMinor: 100,
          splitDefinition: {
            components: [
              {
                type: 'REMAINDER',
                participants: [participantA, participantA],
                mode: 'EQUAL'
              }
            ]
          }
        },
        participants
      ),
    DomainError
  );
});

test('throws when split participants contain another group', () => {
  const differentGroupParticipantId = asParticipantId('out');
  assert.throws(
    () =>
      evaluateSplit(
        {
          groupId,
          amountMinor: 100,
          splitDefinition: {
            components: [{ type: 'REMAINDER', participants: [participantA, differentGroupParticipantId], mode: 'EQUAL' }]
          }
        },
        [
          ...participants,
          {
            id: differentGroupParticipantId,
            groupId: asGroupId('g2'),
            name: 'Out',
            economicUnitId: asEconomicUnitId('u-out'),
            consumptionCategory: 'FULL'
          }
        ]
      ),
    DomainError
  );
});
