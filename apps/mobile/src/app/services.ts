import {
  AddExpenseInput,
  AddExpenseUseCase,
  BalanceModel,
  Clock,
  CreateEconomicUnitInput,
  CreateEconomicUnitUseCase,
  CreateGroupInput,
  CreateGroupUseCase,
  CreateParticipantInput,
  CreateParticipantUseCase,
  ExpenseModel,
  GetBalancesByEconomicUnitOwnerUseCase,
  GetBalancesByParticipantUseCase,
  GetGroupOverviewUseCase,
  GetSettlementPlanUseCase,
  GroupModel,
  GroupOverviewModel,
  IdGenerator,
  SettlementPlanModel,
  SplitDefinitionModel
} from '@lusplit/application';
import { InfraMobileSqlite, createInfraMobileSqlite } from '@lusplit/infra-mobile';

class TimestampClock implements Clock {
  nowIso(): string {
    return new Date().toISOString();
  }
}

class PrefixedIdGenerator implements IdGenerator {
  private counter = 0;

  nextId(): string {
    this.counter += 1;
    return `mobile-${Date.now()}-${this.counter}`;
  }
}

interface GroupListRow {
  id: string;
  currency: string;
  closed: number;
}

export interface AddExpenseDraft {
  groupId: string;
  title: string;
  paidByParticipantId: string;
  amountMinor: number;
  splitDefinition: SplitDefinitionModel;
  date?: string;
  notes?: string;
}

export interface MobileServices {
  listGroups(): Promise<GroupModel[]>;
  createGroup(input: CreateGroupInput): Promise<GroupModel>;
  createEconomicUnit(input: CreateEconomicUnitInput): Promise<void>;
  createParticipant(input: CreateParticipantInput): Promise<void>;
  createEconomicUnitWithOwnerParticipant(input: {
    groupId: string;
    economicUnitName?: string;
    participantName: string;
    consumptionCategory: 'FULL' | 'HALF' | 'CUSTOM';
    customConsumptionWeight?: string;
  }): Promise<void>;
  addExpense(input: AddExpenseDraft): Promise<ExpenseModel>;
  getGroupOverview(groupId: string): Promise<GroupOverviewModel>;
  getParticipantBalances(groupId: string): Promise<BalanceModel[]>;
  getEconomicUnitOwnerBalances(groupId: string): Promise<BalanceModel[]>;
  getSettlementPlan(groupId: string, mode: 'PARTICIPANT' | 'ECONOMIC_UNIT_OWNER'): Promise<SettlementPlanModel>;
  exportGroupSnapshot(groupId: string): Promise<string>;
  importGroupSnapshot(snapshotJson: string): Promise<void>;
  reset(): Promise<void>;
  close(): Promise<void>;
}

export const createMobileServices = async (): Promise<MobileServices> => {
  const infra = await createInfraMobileSqlite();
  const idGenerator = new PrefixedIdGenerator();
  const clock = new TimestampClock();

  const createGroup = new CreateGroupUseCase(infra.groupRepository, idGenerator);
  const createEconomicUnit = new CreateEconomicUnitUseCase(
    infra.groupRepository,
    infra.economicUnitRepository,
    idGenerator
  );
  const createParticipant = new CreateParticipantUseCase(
    infra.groupRepository,
    infra.economicUnitRepository,
    infra.participantRepository,
    idGenerator
  );
  const addExpense = new AddExpenseUseCase(
    infra.groupRepository,
    infra.participantRepository,
    infra.expenseRepository,
    idGenerator,
    clock
  );
  const getOverview = new GetGroupOverviewUseCase(
    infra.groupRepository,
    infra.participantRepository,
    infra.economicUnitRepository,
    infra.expenseRepository,
    infra.transferRepository
  );
  const getBalancesByParticipant = new GetBalancesByParticipantUseCase(
    infra.groupRepository,
    infra.participantRepository,
    infra.expenseRepository
  );
  const getBalancesByEconomicUnitOwner = new GetBalancesByEconomicUnitOwnerUseCase(
    infra.groupRepository,
    infra.participantRepository,
    infra.economicUnitRepository,
    infra.expenseRepository
  );
  const getSettlement = new GetSettlementPlanUseCase(
    infra.groupRepository,
    infra.participantRepository,
    infra.economicUnitRepository,
    infra.expenseRepository
  );

  return {
    listGroups: async () => {
      const rows = await infra.db.getAllAsync<GroupListRow>('SELECT id, currency, closed FROM groups ORDER BY id');
      return rows.map((row) => ({
        id: row.id,
        currency: row.currency,
        closed: row.closed === 1
      }));
    },
    createGroup: (input) => createGroup.execute(input),
    createEconomicUnit: async (input) => {
      await createEconomicUnit.execute(input);
    },
    createParticipant: async (input) => {
      await createParticipant.execute(input);
    },
    createEconomicUnitWithOwnerParticipant: async (input) => {
      const ownerParticipantId = idGenerator.nextId();
      const economicUnit = await createEconomicUnit.execute({
        groupId: input.groupId,
        ownerParticipantId,
        name: input.economicUnitName
      });

      await createParticipant.execute({
        groupId: input.groupId,
        economicUnitId: economicUnit.id,
        name: input.participantName,
        consumptionCategory: input.consumptionCategory,
        customConsumptionWeight: input.customConsumptionWeight
      });
    },
    addExpense: (input) => {
      const payload: AddExpenseInput = {
        groupId: input.groupId,
        title: input.title,
        paidByParticipantId: input.paidByParticipantId,
        amountMinor: input.amountMinor,
        splitDefinition: input.splitDefinition,
        date: input.date,
        notes: input.notes
      };

      return addExpense.execute(payload);
    },
    getGroupOverview: (groupId) => getOverview.execute({ groupId }),
    getParticipantBalances: (groupId) => getBalancesByParticipant.execute({ groupId }),
    getEconomicUnitOwnerBalances: (groupId) => getBalancesByEconomicUnitOwner.execute({ groupId }),
    getSettlementPlan: (groupId, mode) => getSettlement.execute({ groupId, mode }),
    exportGroupSnapshot: async (groupId) => {
      const snapshot = await infra.exportGroupSnapshot(groupId);
      return JSON.stringify(snapshot, null, 2);
    },
    importGroupSnapshot: async (snapshotJson) => {
      const parsed = JSON.parse(snapshotJson) as unknown;
      await infra.importGroupSnapshot(parsed);
    },
    reset: () => infra.reset(),
    close: () => infra.close()
  };
};

export type MobileInfraReference = InfraMobileSqlite;
