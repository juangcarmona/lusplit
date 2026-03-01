import { useFocusEffect } from '@react-navigation/native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useCallback, useMemo, useState } from 'react';
import { FlatList, Pressable, SafeAreaView, StyleSheet, Text, TextInput, View } from 'react-native';

import { colors, radii } from '../theme/tokens';
import { RootStackParamList } from '../navigation/types';
import { useMobileServices } from '../app/services-context';
import { entitiesActions } from '../store/entities-slice';
import { useAppDispatch, useAppSelector } from '../store/hooks';
import { selectGroupList } from '../store/selectors';

type Props = NativeStackScreenProps<RootStackParamList, 'GroupsList'>;

export const GroupsListScreen = ({ navigation }: Props) => {
  const services = useMobileServices();
  const dispatch = useAppDispatch();
  const groups = useAppSelector(selectGroupList);

  const [currencyInput, setCurrencyInput] = useState('USD');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadGroups = useCallback(async () => {
    const loaded = await services.listGroups();
    dispatch(entitiesActions.upsertGroups(loaded));
  }, [dispatch, services]);

  useFocusEffect(
    useCallback(() => {
      void loadGroups();
    }, [loadGroups])
  );

  const normalizedCurrency = useMemo(() => currencyInput.trim().toUpperCase(), [currencyInput]);

  const handleCreateGroup = useCallback(async () => {
    if (!normalizedCurrency) {
      setError('Currency is required');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const created = await services.createGroup({ currency: normalizedCurrency });
      dispatch(entitiesActions.upsertGroups([created]));
      dispatch(entitiesActions.setActiveGroupId(created.id));
      navigation.navigate('GroupDetail', { groupId: created.id });
    } catch (unknownError) {
      const message = unknownError instanceof Error ? unknownError.message : 'Could not create group';
      setError(message);
    } finally {
      setBusy(false);
    }
  }, [dispatch, navigation, normalizedCurrency, services]);

  const renderGroup = useCallback(
    ({ item }: { item: (typeof groups)[number] }) => (
      <Pressable
        accessibilityRole="button"
        style={styles.groupCard}
        onPress={() => {
          dispatch(entitiesActions.setActiveGroupId(item.id));
          navigation.navigate('GroupDetail', { groupId: item.id });
        }}
      >
        <Text style={styles.groupTitle}>{item.id}</Text>
        <Text style={styles.groupMeta}>Currency: {item.currency}</Text>
      </Pressable>
    ),
    [dispatch, groups, navigation]
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Groups</Text>

        <View style={styles.formRow}>
          <TextInput
            value={currencyInput}
            onChangeText={setCurrencyInput}
            autoCapitalize="characters"
            placeholder="Currency (e.g. USD)"
            placeholderTextColor={colors.text.muted.gray}
            style={styles.input}
            editable={!busy}
            maxLength={3}
          />
          <Pressable accessibilityRole="button" style={styles.button} onPress={handleCreateGroup} disabled={busy}>
            <Text style={styles.buttonText}>{busy ? 'Creating…' : 'Create Group'}</Text>
          </Pressable>
        </View>

        {error ? <Text style={styles.errorText}>{error}</Text> : null}

        <FlatList
          data={groups}
          keyExtractor={(item) => item.id}
          renderItem={renderGroup}
          contentContainerStyle={styles.listContent}
          ListEmptyComponent={<Text style={styles.emptyText}>No groups yet.</Text>}
        />
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
  formRow: {
    gap: 8
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
  title: {
    fontSize: 24,
    color: colors.text.primary.navy,
    fontWeight: '600'
  },
  button: {
    minHeight: 44,
    borderRadius: radii.button,
    backgroundColor: colors.panel.left.teal,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 14
  },
  listContent: {
    gap: 8
  },
  groupCard: {
    borderRadius: radii.card,
    backgroundColor: colors.divider.white,
    borderWidth: 1,
    borderColor: colors.text.muted.gray,
    minHeight: 44,
    justifyContent: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8
  },
  groupTitle: {
    color: colors.text.primary.navy,
    fontWeight: '600'
  },
  groupMeta: {
    color: colors.text.muted.gray,
    marginTop: 2
  },
  emptyText: {
    color: colors.text.muted.gray
  },
  errorText: {
    color: colors.state.error.softRed
  },
  buttonText: {
    color: colors.divider.white,
    fontWeight: '500'
  }
});
