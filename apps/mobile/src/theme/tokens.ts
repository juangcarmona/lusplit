export const colors = {
  panel: {
    left: {
      teal: '#2CB1A1'
    },
    right: {
      indigo: '#5A74FF'
    }
  },
  text: {
    primary: {
      navy: '#1F2937'
    },
    muted: {
      gray: '#6B7280'
    }
  },
  background: {
    snow: '#F6F8FB',
    dark: '#0F172A'
  },
  divider: {
    white: '#FFFFFF'
  },
  state: {
    positive: {
      softGreen: '#16A34A'
    },
    owes: {
      warmAmber: '#F59E0B'
    },
    error: {
      softRed: '#DC2626'
    }
  }
} as const;

export const typography = {
  fontFamilies: ['Inter Rounded', 'SF Pro Rounded', 'Nunito', 'Manrope'] as const,
  wordmarkWeight: '500',
  wordmarkTrackingPercent: 2
} as const;

export const radii = {
  card: 16,
  button: 12,
  input: 10
} as const;

export const elevation = {
  soft1: {
    shadowColor: 'rgba(0,0,0,0.05)',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 1,
    shadowRadius: 8,
    elevation: 1
  }
} as const;

export const motion = {
  duration: {
    fast: 120,
    standard: 180
  },
  easing: {
    default: 'ease-out'
  }
} as const;

export const accessibility = {
  minTouchTarget: 44,
  minContrastRatio: 4.5
} as const;