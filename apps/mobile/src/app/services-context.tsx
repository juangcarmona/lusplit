import { PropsWithChildren, createContext, useContext, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { MobileServices, createMobileServices } from './services';
import { colors } from '../theme/tokens';

interface ServicesContextValue {
  services: MobileServices | null;
  loading: boolean;
  error: string | null;
}

const ServicesContext = createContext<ServicesContextValue>({
  services: null,
  loading: true,
  error: null
});

const LoadingScreen = ({ error }: { error: string | null }) => {
  return (
    <View style={styles.container}>
      {error ? <Text style={styles.errorText}>{error}</Text> : <ActivityIndicator color={colors.panel.left.teal} />}
      {!error && <Text style={styles.caption}>Preparing local data store…</Text>}
    </View>
  );
};

export const MobileServicesProvider = ({ children }: PropsWithChildren) => {
  const [services, setServices] = useState<MobileServices | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let createdServices: MobileServices | null = null;

    const init = async () => {
      try {
        const instance = await createMobileServices();
        if (cancelled) {
          await instance.close();
          return;
        }
        createdServices = instance;
        setServices(instance);
      } catch (unknownError) {
        if (!cancelled) {
          const message = unknownError instanceof Error ? unknownError.message : 'Failed to initialize services';
          setError(message);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void init();

    return () => {
      cancelled = true;
      if (createdServices) {
        void createdServices.close();
      }
    };
  }, []);

  const contextValue = useMemo(
    () => ({
      services,
      loading,
      error
    }),
    [services, loading, error]
  );

  if (loading || error || !services) {
    return <LoadingScreen error={error} />;
  }

  return <ServicesContext.Provider value={contextValue}>{children}</ServicesContext.Provider>;
};

export const useMobileServices = (): MobileServices => {
  const { services } = useContext(ServicesContext);

  if (!services) {
    throw new Error('Mobile services are not initialized');
  }

  return services;
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.background.snow,
    paddingHorizontal: 16,
    gap: 12
  },
  caption: {
    color: colors.text.muted.gray
  },
  errorText: {
    color: colors.state.error.softRed,
    textAlign: 'center'
  }
});
