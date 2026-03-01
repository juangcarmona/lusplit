import { StatusBar } from 'expo-status-bar';
import { Provider } from 'react-redux';
import { StyleSheet, View } from 'react-native';

import { MobileServicesProvider } from './src/app/services-context';
import { AppNavigator } from './src/navigation/AppNavigator';
import { store } from './src/store';
import { colors } from './src/theme/tokens';

export default function App() {
  return (
    <Provider store={store}>
      <MobileServicesProvider>
        <View style={styles.content}>
          <AppNavigator />
        </View>
        <StatusBar style="auto" />
      </MobileServicesProvider>
    </Provider>
  );
}

const styles = StyleSheet.create({
  content: {
    flex: 1,
    backgroundColor: colors.background.snow
  }
});
