import { useFocusEffect } from '@react-navigation/native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useCallback, useState } from 'react';
import { Pressable, SafeAreaView, StyleSheet, Text, View } from 'react-native';

import { useMobileServices } from '../app/services-context';
import { RootStackParamList } from '../navigation/types';
import { colors, radii } from '../theme/tokens';

type Props = NativeStackScreenProps<RootStackParamList, 'Settlement'>;

export const SettlementScreen = ({ route }: Props) => {
  const services = useMobileServices();

  const [mode, setMode] = useState<'PARTICIPANT' | 'ECONOMIC_UNIT_OWNER'>('PARTICIPANT');
  const [transfers, setTransfers] = useState<
    Array<{ fromParticipantId: string; toParticipantId: string; amountMinor: number }>
  >([]);
  const [error, setError] = useState<string | null>(null);

  const loadSettlement = useCallback(async () => {
    try {
      setError(null);
      const plan = await services.getSettlementPlan(route.params.groupId, mode);
      setTransfers(plan.transfers);
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Could not load settlement';
      setError(message);
    }
  }, [mode, route.params.groupId, services]);

  useFocusEffect(
    useCallback(() => {
      void loadSettlement();
    }, [loadSettlement])
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Settlement</Text>
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

        {transfers.length === 0 ? (
          <Text style={styles.meta}>No settlement transfers required.</Text>
        ) : (
          transfers.map((transfer, index) => (
            <View key={`${transfer.fromParticipantId}-${transfer.toParticipantId}-${index}`} style={styles.row}>
              <Text style={styles.rowTitle}>
                {transfer.fromParticipantId} → {transfer.toParticipantId}
              </Text>
              <Text style={styles.rowAmount}>{transfer.amountMinor}</Text>
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
  rowTitle: {
    color: colors.text.primary.navy,
    fontWeight: '500'
  },
  rowAmount: {
    color: colors.state.positive.softGreen,
    marginTop: 2
  },
  errorText: {
    color: colors.state.error.softRed
  }
});
