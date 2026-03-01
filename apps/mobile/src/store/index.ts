import { configureStore } from '@reduxjs/toolkit';

import { entitiesSlice } from './entities-slice';

export const store = configureStore({
  reducer: {
    entities: entitiesSlice.reducer
  }
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
