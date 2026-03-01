import { Expense, asExpenseId, asGroupId, asParticipantId, evaluateSplit } from '@lusplit/core';

import { AuthContext } from '../../models/common/auth-context';
import { ExpenseModel } from '../../models/common/entities-model';
import { AddExpenseInput } from '../../models/commands/add-expense-input';
import { mapExpenseToModel } from '../../mappers/entity-mappers';
import { splitDefinitionModelToCore } from '../../mappers/split-mappers';
import { Clock } from '../../ports/clock';
import { ExpenseRepository } from '../../ports/expense-repository';
import { GroupRepository } from '../../ports/group-repository';
import { IdGenerator } from '../../ports/id-generator';
import { ParticipantRepository } from '../../ports/participant-repository';
import { ValidationError } from '../../errors';
import { assertGroupOpen, assertNonEmpty, assertPositiveMinor, getRequiredGroup, resolveDateIso } from '../common';

export class AddExpenseUseCase {
  constructor(
    private readonly groupRepository: GroupRepository,
    private readonly participantRepository: ParticipantRepository,
    private readonly expenseRepository: ExpenseRepository,
    private readonly idGenerator: IdGenerator,
    private readonly clock: Clock
  ) {}

  async execute(input: AddExpenseInput, _authContext?: AuthContext): Promise<ExpenseModel> {
    assertNonEmpty(input.groupId, 'groupId');
    assertNonEmpty(input.title, 'title');
    assertNonEmpty(input.paidByParticipantId, 'paidByParticipantId');
    assertPositiveMinor(input.amountMinor, 'amountMinor');

    const group = await getRequiredGroup(this.groupRepository, input.groupId);
    assertGroupOpen(group);

    const participants = await this.participantRepository.listByGroupId(input.groupId);
    const payerExists = participants.some((participant) => String(participant.id) === input.paidByParticipantId);
    if (!payerExists) {
      throw new ValidationError(`Payer is not in group ${input.groupId}`);
    }

    const expense: Expense = {
      id: asExpenseId(this.idGenerator.nextId()),
      groupId: asGroupId(input.groupId),
      title: input.title,
      paidByParticipantId: asParticipantId(input.paidByParticipantId),
      amountMinor: input.amountMinor,
      date: resolveDateIso(input.date, this.clock),
      splitDefinition: splitDefinitionModelToCore(input.splitDefinition),
      notes: input.notes
    };

    evaluateSplit(expense, participants);
    await this.expenseRepository.save(expense);

    return mapExpenseToModel(expense);
  }
}
