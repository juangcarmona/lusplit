import { SplitDefinitionModel } from '../common/split-model';

export interface EditExpenseInput {
  groupId: string;
  expenseId: string;
  title?: string;
  paidByParticipantId?: string;
  amountMinor?: number;
  splitDefinition?: SplitDefinitionModel;
  date?: string;
  notes?: string;
}
