import { DomainError } from '../errors/domain-error';

export const assertMinorUnits = (value: number, fieldName = 'value'): void => {
  if (!Number.isInteger(value)) {
    throw new DomainError(`${fieldName} must be an integer minor-unit value`);
  }
};

export const addMinor = (left: number, right: number): number => {
  assertMinorUnits(left, 'left');
  assertMinorUnits(right, 'right');
  return left + right;
};

export const subtractMinor = (left: number, right: number): number => {
  assertMinorUnits(left, 'left');
  assertMinorUnits(right, 'right');
  return left - right;
};
