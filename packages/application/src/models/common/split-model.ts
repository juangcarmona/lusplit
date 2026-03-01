export type RemainderModeModel = 'EQUAL' | 'WEIGHT' | 'PERCENT';

export interface FixedContributionModel {
  type: 'FIXED';
  shares: Record<string, number>;
}

export interface RemainderDistributionModel {
  type: 'REMAINDER';
  participants: string[];
  mode: RemainderModeModel;
  weights?: Record<string, string>;
  percents?: Record<string, number>;
}

export type SplitComponentModel = FixedContributionModel | RemainderDistributionModel;

export interface SplitDefinitionModel {
  components: SplitComponentModel[];
}
