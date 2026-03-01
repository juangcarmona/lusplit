import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';

import { AddExpenseScreen } from '../screens/AddExpenseScreen';
import { BalancesScreen } from '../screens/BalancesScreen';
import { GroupDetailScreen } from '../screens/GroupDetailScreen';
import { GroupsListScreen } from '../screens/GroupsListScreen';
import { SettlementScreen } from '../screens/SettlementScreen';
import { colors } from '../theme/tokens';
import { RootStackParamList } from './types';

const Stack = createNativeStackNavigator<RootStackParamList>();

export const AppNavigator = () => {
  return (
    <NavigationContainer>
      <Stack.Navigator
        initialRouteName="GroupsList"
        screenOptions={{
          headerStyle: {
            backgroundColor: colors.background.snow
          },
          headerTitleStyle: {
            color: colors.text.primary.navy
          },
          contentStyle: {
            backgroundColor: colors.background.snow
          }
        }}
      >
        <Stack.Screen name="GroupsList" component={GroupsListScreen} options={{ title: 'LuSplit' }} />
        <Stack.Screen name="GroupDetail" component={GroupDetailScreen} options={{ title: 'Group' }} />
        <Stack.Screen name="AddExpense" component={AddExpenseScreen} options={{ title: 'Add Expense' }} />
        <Stack.Screen name="Balances" component={BalancesScreen} options={{ title: 'Balances' }} />
        <Stack.Screen name="Settlement" component={SettlementScreen} options={{ title: 'Settlement' }} />
      </Stack.Navigator>
    </NavigationContainer>
  );
};