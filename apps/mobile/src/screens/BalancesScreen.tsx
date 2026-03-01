import { useFocusEffect } from '@react-navigation/native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useCallback, useState } from 'react';
import { Pressable, SafeAreaView, StyleSheet, Text, View } from 'react-native';

import { useMobileServices } from '../app/services-context';
import { RootStackParamList } from '../navigation/types';
import { colors, radii } from '../theme/tokens';

type Props = NativeStackScreenProps<RootStackParamList, 'Balances'>;

export const BalancesScreen = ({ route }: Props) => {
  const services = useMobileServices();

  const [mode, setMode] = useState<'PARTICIPANT' | 'ECONOMIC_UNIT_OWNER'>('PARTICIPANT');
  const [balances, setBalances] = useState<Array<{ entityId: string; amountMinor: number }>>([]);
  const [error, setError] = useState<string | null>(null);

  const loadBalances = useCallback(async () => {
    try {
      setError(null);
      const data =
        mode === 'PARTICIPANT'
          ? await services.getParticipantBalances(route.params.groupId)
          : await services.getEconomicUnitOwnerBalances(route.params.groupId);
      setBalances(data);
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Could not load balances';
      setError(message);
    }
  }, [mode, route.params.groupId, services]);

  useFocusEffect(
    useCallback(() => {
      void loadBalances();
    }, [loadBalances])
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Balances</Text>
        <Text style={styles.meta}>Group: {route.params.groupId}</Text>

        <View style={styles.toggleRow}>
          {(['PARTICIPANT', 'ECONOMIC_UNIT_OWNER'] as const).map((nextMode) => (
            <Pressable
              key={nextMode}
              style={[styles.toggleButton, mode === nextMode ? styles.toggleButtonActive : undefined]}
              onPress={() => setMode(nextMode)}
            >
              <Text style={[styles.toggleText, mode === nextMode ? styles.toggleTextActive : undefined]}>
                {nextMode === 'PARTICIPANT' ? 'Participant' : 'Owner'}
              </Text>
            </Pressable>
          ))}
        </View>

        {error ? <Text style={styles.errorText}>{error}</Text> : null}

        {balances.length === 0 ? (
          <Text style={styles.meta}>No balances available.</Text>
        ) : (
          balances.map((balance) => (
            <View key={balance.entityId} style={styles.row}>
              <Text style={styles.rowLabel}>{balance.entityId}</Text>
              <Text style={[styles.rowAmount, balance.amountMinor < 0 ? styles.owes : styles.positive]}>
                {balance.amountMinor}
              </Text>
            </View>
          ))
        )}
      </View>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background.snow
  },
  content: {
    flex: 1,
    padding: 16,
    gap: 12
  },
  title: {
    fontSize: 24,
    color: colors.text.primary.navy,
    fontWeight: '600'
  },
  meta: {
    color: colors.text.muted.gray
  },
  toggleRow: {
    flexDirection: 'row',
    gap: 8
  },
  toggleButton: {
    minHeight: 44,
    borderRadius: radii.button,
    borderWidth: 1,
    borderColor: colors.panel.right.indigo,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 12
  },
  toggleButtonActive: {
    backgroundColor: colors.panel.right.indigo
  },
  toggleText: {
    color: colors.panel.right.indigo,
    fontWeight: '500'
  },
  toggleTextActive: {
    color: colors.divider.white
  },
  row: {
    minHeight: 44,
    borderRadius: radii.input,
    borderWidth: 1,
    borderColor: colors.text.muted.gray,
    backgroundColor: colors.divider.white,
    paddingHorizontal: 12,
    justifyContent: 'center'
  },
  rowLabel: {
    color: colors.text.primary.navy,
    fontWeight: '500'
  },
  rowAmount: {
    marginTop: 2
  },
  owes: {
    color: colors.state.owes.warmAmber
  },
  positive: {
    color: colors.state.positive.softGreen
  },
  errorText: {
    color: colors.state.error.softRed
  }
});
