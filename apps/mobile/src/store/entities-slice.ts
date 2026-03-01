import { PayloadAction, createEntityAdapter, createSlice } from '@reduxjs/toolkit';

import { EconomicUnitRecord, ExpenseRecord, GroupRecord, ParticipantRecord, TransferRecord } from './models';

const groupsAdapter = createEntityAdapter<GroupRecord>();
const economicUnitsAdapter = createEntityAdapter<EconomicUnitRecord>();
const participantsAdapter = createEntityAdapter<ParticipantRecord>();
const expensesAdapter = createEntityAdapter<ExpenseRecord>();
const transfersAdapter = createEntityAdapter<TransferRecord>();

export interface EntitiesState {
  groups: ReturnType<typeof groupsAdapter.getInitialState>;
  economicUnits: ReturnType<typeof economicUnitsAdapter.getInitialState>;
  participants: ReturnType<typeof participantsAdapter.getInitialState>;
  expenses: ReturnType<typeof expensesAdapter.getInitialState>;
  transfers: ReturnType<typeof transfersAdapter.getInitialState>;
  activeGroupId: string | null;
}

const initialState: EntitiesState = {
  groups: groupsAdapter.getInitialState(),
  economicUnits: economicUnitsAdapter.getInitialState(),
  participants: participantsAdapter.getInitialState(),
  expenses: expensesAdapter.getInitialState(),
  transfers: transfersAdapter.getInitialState(),
  activeGroupId: null
};

export const entitiesSlice = createSlice({
  name: 'entities',
  initialState,
  reducers: {
    upsertGroups(state, action: PayloadAction<GroupRecord[]>) {
      groupsAdapter.upsertMany(state.groups, action.payload);
    },
    upsertEconomicUnits(state, action: PayloadAction<EconomicUnitRecord[]>) {
      economicUnitsAdapter.upsertMany(state.economicUnits, action.payload);
    },
    upsertParticipants(state, action: PayloadAction<ParticipantRecord[]>) {
      participantsAdapter.upsertMany(state.participants, action.payload);
    },
    upsertExpenses(state, action: PayloadAction<ExpenseRecord[]>) {
      expensesAdapter.upsertMany(state.expenses, action.payload);
    },
    upsertTransfers(state, action: PayloadAction<TransferRecord[]>) {
      transfersAdapter.upsertMany(state.transfers, action.payload);
    },
    setActiveGroupId(state, action: PayloadAction<string | null>) {
      state.activeGroupId = action.payload;
    },
    resetEntitiesState() {
      return initialState;
    }
  }
});

export const entitiesActions = entitiesSlice.actions;

export const groupsSelectors = groupsAdapter.getSelectors<{
  entities: EntitiesState;
}>((state) => state.entities.groups);

export const economicUnitsSelectors = economicUnitsAdapter.getSelectors<{
  entities: EntitiesState;
}>((state) => state.entities.economicUnits);

export const participantsSelectors = participantsAdapter.getSelectors<{
  entities: EntitiesState;
}>((state) => state.entities.participants);

export const expensesSelectors = expensesAdapter.getSelectors<{
  entities: EntitiesState;
}>((state) => state.entities.expenses);

export const transfersSelectors = transfersAdapter.getSelectors<{
  entities: EntitiesState;
}>((state) => state.entities.transfers);
