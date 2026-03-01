import { evaluateSplit, asParticipantId } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { ExpenseModel } from '../../models/common/entities-model';
import { EditExpenseInput } from '../../models/commands/edit-expense-input';
import { mapExpenseToModel } from '../../mappers/entity-mappers';
import { splitDefinitionModelToCore } from '../../mappers/split-mappers';
import { Clock } from '../../ports/clock';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { ParticipantRepository } from '../../ports/participant-repository';
import { NotFoundError, ValidationError } from '../../errors';
import { assertGroupOpen, assertNonEmpty, assertPositiveMinor, getRequiredGroup, resolveDateIso } from '../common';

export class EditExpenseUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly expenseRepository: ExpenseRepository,
    private readonly clock: Clock
  ) {}

  async execute(input: EditExpenseInput, _authContext?: AuthContext): Promise<ExpenseModel> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.expenseId, 'expenseId');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const existing = await this.expenseRepository.getById(input.expenseId);
    if (!existing || String(existing.groupId) !== input.groupId) {
      throw new NotFoundError(`Expense not found: ${input.expenseId}`);
    }

    if (input.amountMinor !== undefined) {
      assertPositiveMinor(input.amountMinor, 'amountMinor');
    }

    const nextExpense = {
      ...existing,
      title: input.title ?? existing.title,
      paidByParticipantId: input.paidByParticipantId ? asParticipantId(input.paidByParticipantId) : existing.paidByParticipantId,
      amountMinor: input.amountMinor ?? existing.amountMinor,
      splitDefinition: input.splitDefinition ? splitDefinitionModelToCore(input.splitDefinition) : existing.splitDefinition,
      date: resolveDateIso(input.date ?? existing.date, this.clock),
      notes: input.notes ?? existing.notes
    };

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const payerExists = participants.some((participant) => participant.id === nextExpense.paidByParticipantId);
    if (!payerExists) {
      throw new ValidationError(`Payer is not in group ${input.groupId}`);
    }

    evaluateSplit(nextExpense, participants);
    await this.expenseRepository.save(nextExpense);

    return mapExpenseToModel(nextExpense);
  }
}
