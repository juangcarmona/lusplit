import { createSelector } from '@reduxjs/toolkit';

import {
  economicUnitsSelectors,
  expensesSelectors,
  groupsSelectors,
  participantsSelectors,
  transfersSelectors
} from './entities-slice';
import { RootState } from './index';

export const selectActiveGroupId = (state: RootState): string | null => state.entities.activeGroupId;

export const selectGroupList = createSelector(
  [(state: RootState) => groupsSelectors.selectAll(state)],
  (groups) => [...groups].sort((left, right) => left.id.localeCompare(right.id))
);

export const selectGroupDetailData = createSelector(
  [selectActiveGroupId, (state: RootState) => participantsSelectors.selectAll(state), (state: RootState) => economicUnitsSelectors.selectAll(state), (state: RootState) => expensesSelectors.selectAll(state), (state: RootState) => transfersSelectors.selectAll(state)],
  (groupId, participants, economicUnits, expenses, transfers) => {
    if (!groupId) {
      return {
        participants: [],
        economicUnits: [],
        expenses: [],
        transfers: []
      };
    }

    return {
      participants: participants.filter((participant) => participant.groupId === groupId),
      economicUnits: economicUnits.filter((economicUnit) => economicUnit.groupId === groupId),
      expenses: expenses.filter((expense) => expense.groupId === groupId),
      transfers: transfers.filter((transfer) => transfer.groupId === groupId)
    };
  }
);
