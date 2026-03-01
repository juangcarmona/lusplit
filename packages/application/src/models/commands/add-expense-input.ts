import { SplitDefinitionModel } from '../common/split-model';

export interface AddExpenseInput {
  groupId: string;
  title: string;
  paidByParticipantId: string;
  amountMinor: number;
  splitDefinition: SplitDefinitionModel;
  date?: string;
  notes?: string;
}
