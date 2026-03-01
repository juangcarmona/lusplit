import { EconomicUnit, Expense, Group, Participant, Transfer } from '@lusplit/core';

import {
  Clock,
  EconomicUnitRepository,
  ExpenseRepository,
  GroupRepository,
  IdGenerator,
  ParticipantRepository,
  TransferRepository
} from '../../src';

export class InMemoryGroupRepository implements GroupRepository {
  readonly groups = new Map<string, Group>();

  async getById(groupId: string): Promise<Group | null> {
    return this.groups.get(groupId) ?? null;
  }

  async save(group: Group): Promise<void> {
    this.groups.set(String(group.id), group);
  }
}

export class InMemoryParticipantRepository implements ParticipantRepository {
  readonly participants = new Map<string, Participant>();

  async getById(participantId: string): Promise<Participant | null> {
    return this.participants.get(participantId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<Participant[]> {
    return [...this.participants.values()]
      .filter((participant) => String(participant.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(participant: Participant): Promise<void> {
    this.participants.set(String(participant.id), participant);
  }
}

export class InMemoryEconomicUnitRepository implements EconomicUnitRepository {
  readonly economicUnits = new Map<string, EconomicUnit>();

  async getById(economicUnitId: string): Promise<EconomicUnit | null> {
    return this.economicUnits.get(economicUnitId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<EconomicUnit[]> {
    return [...this.economicUnits.values()]
      .filter((economicUnit) => String(economicUnit.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(economicUnit: EconomicUnit): Promise<void> {
    this.economicUnits.set(String(economicUnit.id), economicUnit);
  }
}

export class InMemoryExpenseRepository implements ExpenseRepository {
  readonly expenses = new Map<string, Expense>();

  async getById(expenseId: string): Promise<Expense | null> {
    return this.expenses.get(expenseId) ?? null;
  }

  async listByGroupId(groupId: string): Promise<Expense[]> {
    return [...this.expenses.values()]
      .filter((expense) => String(expense.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(expense: Expense): Promise<void> {
    this.expenses.set(String(expense.id), expense);
  }

  async delete(groupId: string, expenseId: string): Promise<void> {
    const current = this.expenses.get(expenseId);
    if (current && String(current.groupId) === groupId) {
      this.expenses.delete(expenseId);
    }
  }
}

export class InMemoryTransferRepository implements TransferRepository {
  readonly transfers = new Map<string, Transfer>();

  async listByGroupId(groupId: string): Promise<Transfer[]> {
    return [...this.transfers.values()]
      .filter((transfer) => String(transfer.groupId) === groupId)
      .sort((left, right) => String(left.id).localeCompare(String(right.id)));
  }

  async save(transfer: Transfer): Promise<void> {
    this.transfers.set(String(transfer.id), transfer);
  }
}

export class SequentialIdGenerator implements IdGenerator {
  private nextValue = 1;

  nextId(): string {
    const id = `id-${this.nextValue}`;
    this.nextValue += 1;
    return id;
  }
}

export class FixedClock implements Clock {
  constructor(private readonly now: string = '2026-01-01T00:00:00.000Z') {}

  nowIso(): string {
    return this.now;
  }
}
