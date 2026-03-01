import { useFocusEffect } from '@react-navigation/native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useCallback, useMemo, useState } from 'react';
import { Pressable, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { useMobileServices } from '../app/services-context';

import { RootStackParamList } from '../navigation/types';
import { colors, radii } from '../theme/tokens';

type Props = NativeStackScreenProps<RootStackParamList, 'AddExpense'>;

export const AddExpenseScreen = ({ route }: Props) => {
  const services = useMobileServices();

  const [title, setTitle] = useState('');
  const [amountMinor, setAmountMinor] = useState('');
  const [paidByParticipantId, setPaidByParticipantId] = useState('');
  const [participants, setParticipants] = useState<Array<{ id: string; name: string }>>([]);
  const [splitMode, setSplitMode] = useState<'EQUAL' | 'FIXED_REMAINDER' | 'WEIGHT'>('EQUAL');
  const [fixedShares, setFixedShares] = useState<Record<string, string>>({});
  const [weights, setWeights] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadParticipants = useCallback(async () => {
    const overview = await services.getGroupOverview(route.params.groupId);
    const mapped = overview.participants.map((participant) => ({ id: participant.id, name: participant.name }));
    setParticipants(mapped);
    if (!paidByParticipantId && mapped.length > 0) {
      setPaidByParticipantId(mapped[0].id);
    }

    setWeights((current) => {
      const next: Record<string, string> = {};
      for (const participant of mapped) {
        next[participant.id] = current[participant.id] ?? '1';
      }
      return next;
    });
    setFixedShares((current) => {
      const next: Record<string, string> = {};
      for (const participant of mapped) {
        next[participant.id] = current[participant.id] ?? '';
      }
      return next;
    });
  }, [paidByParticipantId, route.params.groupId, services]);

  useFocusEffect(
    useCallback(() => {
      void loadParticipants();
    }, [loadParticipants])
  );

  const splitDefinition = useMemo(() => {
    if (splitMode === 'EQUAL') {
      return {
        components: [{ type: 'REMAINDER' as const, participants: participants.map((participant) => participant.id), mode: 'EQUAL' as const }]
      };
    }

    if (splitMode === 'WEIGHT') {
      const weightsMap: Record<string, string> = {};
      for (const participant of participants) {
        weightsMap[participant.id] = (weights[participant.id] ?? '1').trim() || '1';
      }

      return {
        components: [
          {
            type: 'REMAINDER' as const,
            participants: participants.map((participant) => participant.id),
            mode: 'WEIGHT' as const,
            weights: weightsMap
          }
        ]
      };
    }

    const fixed: Record<string, number> = {};
    for (const participant of participants) {
      const rawValue = (fixedShares[participant.id] ?? '').trim();
      if (!rawValue) {
        continue;
      }

      const parsed = Number.parseInt(rawValue, 10);
      if (Number.isFinite(parsed) && parsed > 0) {
        fixed[participant.id] = parsed;
      }
    }

    const remainderParticipants = participants
      .filter((participant) => fixed[participant.id] === undefined)
      .map((participant) => participant.id);

    return {
      components: [
        { type: 'FIXED' as const, shares: fixed },
        {
          type: 'REMAINDER' as const,
          participants: remainderParticipants.length > 0 ? remainderParticipants : participants.map((participant) => participant.id),
          mode: 'EQUAL' as const
        }
      ]
    };
  }, [fixedShares, participants, splitMode, weights]);

  const handleSubmit = useCallback(async () => {
    const parsedAmountMinor = Number.parseInt(amountMinor.trim(), 10);
    if (!title.trim()) {
      setError('Title is required');
      return;
    }
    if (!Number.isFinite(parsedAmountMinor) || parsedAmountMinor <= 0) {
      setError('Amount must be a positive integer in minor units');
      return;
    }
    if (!paidByParticipantId) {
      setError('Payer is required');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await services.addExpense({
        groupId: route.params.groupId,
        title: title.trim(),
        paidByParticipantId,
        amountMinor: parsedAmountMinor,
        splitDefinition
      });

      setTitle('');
      setAmountMinor('');
      setSplitMode('EQUAL');
      setFixedShares({});
      setWeights({});
      await loadParticipants();
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Could not add expense';
      setError(message);
    } finally {
      setBusy(false);
    }
  }, [amountMinor, loadParticipants, paidByParticipantId, route.params.groupId, services, splitDefinition, title]);

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>Add Expense</Text>
        <Text style={styles.meta}>Group: {route.params.groupId}</Text>

        <TextInput
          value={title}
          onChangeText={setTitle}
          placeholder="Title"
          placeholderTextColor={colors.text.muted.gray}
          style={styles.input}
          editable={!busy}
        />
        <TextInput
          value={amountMinor}
          onChangeText={setAmountMinor}
          placeholder="Amount in minor units"
          placeholderTextColor={colors.text.muted.gray}
          keyboardType="number-pad"
          style={styles.input}
          editable={!busy}
        />

        <Text style={styles.sectionLabel}>Paid by</Text>
        <View style={styles.chipRow}>
          {participants.map((participant) => (
            <Pressable
              key={participant.id}
              style={[styles.chip, paidByParticipantId === participant.id ? styles.chipActive : undefined]}
              onPress={() => setPaidByParticipantId(participant.id)}
            >
              <Text style={[styles.chipText, paidByParticipantId === participant.id ? styles.chipTextActive : undefined]}>
                {participant.name}
              </Text>
            </Pressable>
          ))}
        </View>

        <Text style={styles.sectionLabel}>Split mode</Text>
        <View style={styles.chipRow}>
          {(['EQUAL', 'FIXED_REMAINDER', 'WEIGHT'] as const).map((mode) => (
            <Pressable
              key={mode}
              style={[styles.chip, splitMode === mode ? styles.chipActive : undefined]}
              onPress={() => setSplitMode(mode)}
            >
              <Text style={[styles.chipText, splitMode === mode ? styles.chipTextActive : undefined]}>{mode}</Text>
            </Pressable>
          ))}
        </View>

        {splitMode === 'FIXED_REMAINDER' ? (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Fixed shares (minor units)</Text>
            {participants.map((participant) => (
              <View key={participant.id} style={styles.row}>
                <Text style={styles.rowLabel}>{participant.name}</Text>
                <TextInput
                  value={fixedShares[participant.id] ?? ''}
                  onChangeText={(value) =>
                    setFixedShares((current) => ({
                      ...current,
                      [participant.id]: value
                    }))
                  }
                  placeholder="0"
                  placeholderTextColor={colors.text.muted.gray}
                  keyboardType="number-pad"
                  style={styles.rowInput}
                  editable={!busy}
                />
              </View>
            ))}
          </View>
        ) : null}

        {splitMode === 'WEIGHT' ? (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Weights</Text>
            {participants.map((participant) => (
              <View key={participant.id} style={styles.row}>
                <Text style={styles.rowLabel}>{participant.name}</Text>
                <TextInput
                  value={weights[participant.id] ?? '1'}
                  onChangeText={(value) =>
                    setWeights((current) => ({
                      ...current,
                      [participant.id]: value
                    }))
                  }
                  placeholder="1"
                  placeholderTextColor={colors.text.muted.gray}
                  style={styles.rowInput}
                  editable={!busy}
                />
              </View>
            ))}
          </View>
        ) : null}

        {error ? <Text style={styles.errorText}>{error}</Text> : null}

        <Pressable style={styles.button} onPress={handleSubmit} disabled={busy || participants.length === 0}>
          <Text style={styles.buttonText}>{busy ? 'Saving…' : 'Save Expense'}</Text>
        </Pressable>
      </ScrollView>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background.snow
  },
  content: {
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
  input: {
    minHeight: 44,
    borderRadius: radii.input,
    borderColor: colors.text.muted.gray,
    borderWidth: 1,
    paddingHorizontal: 12,
    color: colors.text.primary.navy,
    backgroundColor: colors.divider.white
  },
  section: {
    borderRadius: radii.card,
    borderWidth: 1,
    borderColor: colors.text.muted.gray,
    backgroundColor: colors.divider.white,
    padding: 12,
    gap: 8
  },
  sectionLabel: {
    color: colors.text.primary.navy,
    fontWeight: '600'
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8
  },
  chip: {
    minHeight: 44,
    borderRadius: radii.button,
    borderWidth: 1,
    borderColor: colors.panel.right.indigo,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 12
  },
  chipActive: {
    backgroundColor: colors.panel.right.indigo
  },
  chipText: {
    color: colors.panel.right.indigo,
    fontWeight: '500'
  },
  chipTextActive: {
    color: colors.divider.white
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8
  },
  rowLabel: {
    flex: 1,
    color: colors.text.primary.navy
  },
  rowInput: {
    width: 100,
    minHeight: 44,
    borderRadius: radii.input,
    borderColor: colors.text.muted.gray,
    borderWidth: 1,
    paddingHorizontal: 12,
    color: colors.text.primary.navy,
    backgroundColor: colors.divider.white
  },
  errorText: {
    color: colors.state.error.softRed
  },
  button: {
    minHeight: 44,
    borderRadius: radii.button,
    backgroundColor: colors.panel.left.teal,
    alignItems: 'center',
    justifyContent: 'center'
  },
  buttonText: {
    color: colors.divider.white,
    fontWeight: '500'
  }
});
