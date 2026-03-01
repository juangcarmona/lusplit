import { useFocusEffect } from '@react-navigation/native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useCallback, useMemo, useState } from 'react';
import { Pressable, SafeAreaView, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

import { useMobileServices } from '../app/services-context';
import { RootStackParamList } from '../navigation/types';
import { entitiesActions } from '../store/entities-slice';
import { useAppDispatch, useAppSelector } from '../store/hooks';
import { selectGroupDetailData } from '../store/selectors';
import { colors, radii } from '../theme/tokens';

type Props = NativeStackScreenProps<RootStackParamList, 'GroupDetail'>;

export const GroupDetailScreen = ({ navigation, route }: Props) => {
  const services = useMobileServices();
  const dispatch = useAppDispatch();
  const detail = useAppSelector(selectGroupDetailData);

  const [economicUnitName, setEconomicUnitName] = useState('');
  const [participantName, setParticipantName] = useState('');
  const [consumptionCategory, setConsumptionCategory] = useState<'FULL' | 'HALF' | 'CUSTOM'>('FULL');
  const [customWeight, setCustomWeight] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [snapshotJson, setSnapshotJson] = useState('');
  const [snapshotBusy, setSnapshotBusy] = useState(false);
  const [snapshotMessage, setSnapshotMessage] = useState<string | null>(null);

  const loadOverview = useCallback(async () => {
    const overview = await services.getGroupOverview(route.params.groupId);
    dispatch(entitiesActions.setActiveGroupId(route.params.groupId));
    dispatch(entitiesActions.upsertGroups([overview.group]));
    dispatch(entitiesActions.upsertParticipants(overview.participants));
    dispatch(entitiesActions.upsertEconomicUnits(overview.economicUnits));
    dispatch(entitiesActions.upsertExpenses(overview.expenses));
    dispatch(entitiesActions.upsertTransfers(overview.transfers));
  }, [dispatch, route.params.groupId, services]);

  useFocusEffect(
    useCallback(() => {
      void loadOverview();
    }, [loadOverview])
  );

  const helperText = useMemo(() => {
    if (consumptionCategory === 'CUSTOM') {
      return 'Custom category requires weight (e.g. 0.75).';
    }

    return 'Owner participant is created together with the economic unit.';
  }, [consumptionCategory]);

  const handleCreateParticipantWithUnit = useCallback(async () => {
    const participantNameValue = participantName.trim();
    if (!participantNameValue) {
      setError('Participant name is required');
      return;
    }

    if (consumptionCategory === 'CUSTOM' && !customWeight.trim()) {
      setError('Custom weight is required for CUSTOM category');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await services.createEconomicUnitWithOwnerParticipant({
        groupId: route.params.groupId,
        economicUnitName: economicUnitName.trim() || undefined,
        participantName: participantNameValue,
        consumptionCategory,
        customConsumptionWeight: consumptionCategory === 'CUSTOM' ? customWeight.trim() : undefined
      });

      setEconomicUnitName('');
      setParticipantName('');
      setCustomWeight('');
      setConsumptionCategory('FULL');
      await loadOverview();
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Could not create participant';
      setError(message);
    } finally {
      setBusy(false);
    }
  }, [consumptionCategory, customWeight, economicUnitName, loadOverview, participantName, route.params.groupId, services]);

  const handleExportSnapshot = useCallback(async () => {
    setSnapshotBusy(true);
    setSnapshotMessage(null);
    try {
      const json = await services.exportGroupSnapshot(route.params.groupId);
      setSnapshotJson(json);
      setSnapshotMessage('Snapshot exported.');
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Export failed';
      setSnapshotMessage(message);
    } finally {
      setSnapshotBusy(false);
    }
  }, [route.params.groupId, services]);

  const handleImportSnapshot = useCallback(async () => {
    const payload = snapshotJson.trim();
    if (!payload) {
      setSnapshotMessage('Paste or export snapshot JSON first.');
      return;
    }

    setSnapshotBusy(true);
    setSnapshotMessage(null);
    try {
      await services.reset();
      await services.importGroupSnapshot(payload);
      await loadOverview();
      setSnapshotMessage('Snapshot imported into fresh state.');
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Import failed';
      setSnapshotMessage(message);
    } finally {
      setSnapshotBusy(false);
    }
  }, [loadOverview, services, snapshotJson]);

  const handleReset = useCallback(async () => {
    setSnapshotBusy(true);
    setSnapshotMessage(null);
    try {
      await services.reset();
      dispatch(entitiesActions.resetEntitiesState());
      setSnapshotMessage('Local data reset.');
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Reset failed';
      setSnapshotMessage(message);
    } finally {
      setSnapshotBusy(false);
    }
  }, [dispatch, services]);

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
        <Text style={styles.title}>Group Detail</Text>
        <Text style={styles.meta}>Group: {route.params.groupId}</Text>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Add participant + economic unit</Text>
          <TextInput
            value={economicUnitName}
            onChangeText={setEconomicUnitName}
            placeholder="Economic unit name (optional)"
            placeholderTextColor={colors.text.muted.gray}
            style={styles.input}
            editable={!busy}
          />
          <TextInput
            value={participantName}
            onChangeText={setParticipantName}
            placeholder="Participant name"
            placeholderTextColor={colors.text.muted.gray}
            style={styles.input}
            editable={!busy}
          />

          <View style={styles.categoryRow}>
            {(['FULL', 'HALF', 'CUSTOM'] as const).map((category) => (
              <Pressable
                key={category}
                style={[
                  styles.categoryButton,
                  consumptionCategory === category ? styles.categoryButtonActive : undefined
                ]}
                onPress={() => setConsumptionCategory(category)}
              >
                <Text
                  style={[
                    styles.categoryButtonText,
                    consumptionCategory === category ? styles.categoryButtonTextActive : undefined
                  ]}
                >
                  {category}
                </Text>
              </Pressable>
            ))}
          </View>

          {consumptionCategory === 'CUSTOM' ? (
            <TextInput
              value={customWeight}
              onChangeText={setCustomWeight}
              placeholder="Custom weight"
              placeholderTextColor={colors.text.muted.gray}
              style={styles.input}
              editable={!busy}
            />
          ) : null}

          <Text style={styles.helperText}>{helperText}</Text>
          {error ? <Text style={styles.errorText}>{error}</Text> : null}

          <Pressable style={styles.button} onPress={handleCreateParticipantWithUnit} disabled={busy}>
            <Text style={styles.buttonText}>{busy ? 'Saving…' : 'Add participant'}</Text>
          </Pressable>
        </View>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Participants ({detail.participants.length})</Text>
          {detail.participants.length === 0 ? (
            <Text style={styles.meta}>No participants yet.</Text>
          ) : (
            detail.participants.map((participant) => (
              <View key={participant.id} style={styles.listRow}>
                <Text style={styles.rowTitle}>{participant.name}</Text>
                <Text style={styles.rowMeta}>Category: {participant.consumptionCategory}</Text>
              </View>
            ))
          )}
        </View>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Expenses ({detail.expenses.length})</Text>
          {detail.expenses.length === 0 ? (
            <Text style={styles.meta}>No expenses yet.</Text>
          ) : (
            detail.expenses.map((expense) => (
              <View key={expense.id} style={styles.listRow}>
                <Text style={styles.rowTitle}>{expense.title}</Text>
                <Text style={styles.rowMeta}>{expense.amountMinor} minor units</Text>
              </View>
            ))
          )}
        </View>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Snapshot JSON</Text>
          <TextInput
            multiline
            value={snapshotJson}
            onChangeText={setSnapshotJson}
            style={styles.snapshotInput}
            placeholder="Exported snapshot JSON"
            placeholderTextColor={colors.text.muted.gray}
            editable={!snapshotBusy}
          />
          {snapshotMessage ? <Text style={styles.meta}>{snapshotMessage}</Text> : null}
          <View style={styles.snapshotActions}>
            <Pressable style={styles.secondaryButton} onPress={handleExportSnapshot} disabled={snapshotBusy}>
              <Text style={styles.secondaryButtonText}>Export JSON</Text>
            </Pressable>
            <Pressable style={styles.button} onPress={handleImportSnapshot} disabled={snapshotBusy}>
              <Text style={styles.buttonText}>Import JSON</Text>
            </Pressable>
            <Pressable style={styles.dangerButton} onPress={handleReset} disabled={snapshotBusy}>
              <Text style={styles.buttonText}>Reset App</Text>
            </Pressable>
          </View>
        </View>

        <Pressable
          style={styles.button}
          onPress={() => navigation.navigate('AddExpense', { groupId: route.params.groupId })}
        >
          <Text style={styles.buttonText}>Add Expense</Text>
        </Pressable>
        <Pressable
          style={styles.button}
          onPress={() => navigation.navigate('Balances', { groupId: route.params.groupId })}
        >
          <Text style={styles.buttonText}>Balances</Text>
        </Pressable>
        <Pressable
          style={styles.button}
          onPress={() => navigation.navigate('Settlement', { groupId: route.params.groupId })}
        >
          <Text style={styles.buttonText}>Settlement</Text>
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
  scroll: {
    flex: 1
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
  section: {
    gap: 8,
    borderRadius: radii.card,
    borderWidth: 1,
    borderColor: colors.text.muted.gray,
    padding: 12,
    backgroundColor: colors.divider.white
  },
  sectionTitle: {
    color: colors.text.primary.navy,
    fontWeight: '600'
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
  categoryRow: {
    flexDirection: 'row',
    gap: 8
  },
  categoryButton: {
    minHeight: 44,
    borderRadius: radii.button,
    borderWidth: 1,
    borderColor: colors.panel.right.indigo,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 12
  },
  categoryButtonActive: {
    backgroundColor: colors.panel.right.indigo
  },
  categoryButtonText: {
    color: colors.panel.right.indigo,
    fontWeight: '500'
  },
  categoryButtonTextActive: {
    color: colors.divider.white
  },
  helperText: {
    color: colors.text.muted.gray
  },
  errorText: {
    color: colors.state.error.softRed
  },
  listRow: {
    borderRadius: radii.input,
    borderWidth: 1,
    borderColor: colors.text.muted.gray,
    paddingHorizontal: 10,
    paddingVertical: 8
  },
  rowTitle: {
    color: colors.text.primary.navy,
    fontWeight: '500'
  },
  rowMeta: {
    color: colors.text.muted.gray
  },
  button: {
    minHeight: 44,
    borderRadius: radii.button,
    backgroundColor: colors.panel.right.indigo,
    alignItems: 'center',
    justifyContent: 'center'
  },
  secondaryButton: {
    minHeight: 44,
    borderRadius: radii.button,
    borderWidth: 1,
    borderColor: colors.panel.left.teal,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 10
  },
  secondaryButtonText: {
    color: colors.panel.left.teal,
    fontWeight: '600'
  },
  dangerButton: {
    minHeight: 44,
    borderRadius: radii.button,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.state.error.softRed,
    paddingHorizontal: 10
  },
  snapshotInput: {
    minHeight: 120,
    borderRadius: radii.input,
    borderColor: colors.text.muted.gray,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 8,
    color: colors.text.primary.navy,
    backgroundColor: colors.divider.white,
    textAlignVertical: 'top'
  },
  snapshotActions: {
    gap: 8
  },
  buttonText: {
    color: colors.divider.white,
    fontWeight: '500'
  }
});
