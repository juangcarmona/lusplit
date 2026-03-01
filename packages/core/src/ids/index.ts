export type Opaque<T, Name extends string> = T & { readonly __brand: Name };

export type GroupId = Opaque<string, 'GroupId'>;
export type ParticipantId = Opaque<string, 'ParticipantId'>;
export type EconomicUnitId = Opaque<string, 'EconomicUnitId'>;
export type ExpenseId = Opaque<string, 'ExpenseId'>;
export type TransferId = Opaque<string, 'TransferId'>;

export const asGroupId = (value: string): GroupId => value as GroupId;
export const asParticipantId = (value: string): ParticipantId => value as ParticipantId;
export const asEconomicUnitId = (value: string): EconomicUnitId => value as EconomicUnitId;
export const asExpenseId = (value: string): ExpenseId => value as ExpenseId;
export const asTransferId = (value: string): TransferId => value as TransferId;
