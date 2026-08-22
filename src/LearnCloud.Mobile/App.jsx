/**
 * LearnCloud Mobile App - App.jsx
 * React Native Expo, parents first, teachers second, consumes existing API, no new backend patterns
 * Offline strategy: manual merge for attendance duplicate Keep Server default, server-wins for locked marks, stale thresholds 6/12/24h, background sync always
 */

import React, { useEffect, useState } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import NetInfo from '@react-native-community/netinfo';
import * as LocalAuthentication from 'expo-local-authentication';
import * as SecureStore from 'expo-secure-store';
import { Text, View, AppState } from 'react-native';

import { AuthProvider, useAuth } from './services/AuthContext';
import { OfflineProvider, useOffline } from './services/OfflineContext';
import { SyncService } from './services/SyncService';

import LoginScreen from './screens/LoginScreen';
import ParentHomeScreen from './screens/ParentHomeScreen';
import ParentFeesScreen from './screens/ParentFeesScreen';
import ParentAttendanceScreen from './screens/ParentAttendanceScreen';
import ParentResultsScreen from './screens/ParentResultsScreen';
import TeacherDashboardScreen from './screens/TeacherDashboardScreen';
import AttendanceCaptureScreen from './screens/AttendanceCaptureScreen';
import MarksEntryScreen from './screens/MarksEntryScreen';
import ProfileScreen from './screens/ProfileScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

function StaleBanner() {
  const { isConnected, lastSynced, isStale } = useOffline();
  if (isConnected && !isStale) return null;
  return (
    <View style={{backgroundColor: isConnected ? '#FFF8E1' : '#FFEBEE', padding: 8, borderBottomWidth: 1, borderColor: '#BDBDBD'}}>
      <Text style={{fontSize: 11, textAlign: 'center'}}>
        {!isConnected ? `Offline • Last synced ${lastSynced ? `${Math.round((Date.now()-lastSynced)/60000)}m ago` : 'never'} • Showing cached data` : `Stale • Updated ${lastSynced ? `${Math.round((Date.now()-lastSynced)/3600000)}h ago` : 'never'} • Pull to refresh`}
      </Text>
    </View>
  );
}

function ParentTabs() {
  return (
    <Tab.Navigator screenOptions={{ headerShown: false, tabBarActiveTintColor: '#0F153A', tabBarStyle: { height: 60, paddingBottom: 8 } }}>
      <Tab.Screen name="Home" component={ParentHomeScreen} options={{ tabBarIcon: () => <Text>🏠</Text> }} />
      <Tab.Screen name="Fees" component={ParentFeesScreen} options={{ tabBarIcon: () => <Text>💳</Text> }} />
      <Tab.Screen name="Attendance" component={ParentAttendanceScreen} options={{ tabBarIcon: () => <Text>✓</Text> }} />
      <Tab.Screen name="Results" component={ParentResultsScreen} options={{ tabBarIcon: () => <Text>📄</Text> }} />
      <Tab.Screen name="Profile" component={ProfileScreen} options={{ tabBarIcon: () => <Text>👤</Text> }} />
    </Tab.Navigator>
  );
}

function TeacherTabs() {
  return (
    <Tab.Navigator screenOptions={{ headerShown: false, tabBarActiveTintColor: '#0F153A' }}>
      <Tab.Screen name="Dashboard" component={TeacherDashboardScreen} />
      <Tab.Screen name="AttendanceCapture" component={AttendanceCaptureScreen} options={{ title: 'Attendance' }} />
      <Tab.Screen name="MarksEntry" component={MarksEntryScreen} options={{ title: 'Marks' }} />
      <Tab.Screen name="Profile" component={ProfileScreen} />
    </Tab.Navigator>
  );
}

function RootNavigator() {
  const { user, role, isLoading, isBiometricLocked, unlockWithBiometric } = useAuth();
  const { isConnected } = useOffline();

  if (isLoading) return <View style={{flex:1, justifyContent:'center', alignItems:'center'}}><Text>Loading...</Text></View>;

  if (isBiometricLocked) {
    return (
      <View style={{flex:1, justifyContent:'center', alignItems:'center', padding:20}}>
        <Text style={{fontSize:18, fontWeight:'bold'}}>Biometric Unlock Required</Text>
        <Text style={{fontSize:12, color:'#666', marginTop:8}}>App locked after 5 min background, secure storage refresh token encrypted</Text>
        <Text style={{marginTop:12, padding:12, backgroundColor:'#0F153A', color:'white', borderRadius:24}} onPress={unlockWithBiometric}>Unlock with Biometric / PIN</Text>
      </View>
    );
  }

  if (!user) return <LoginScreen />;

  return (
    <>
      <StaleBanner />
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {role === 'PARENT' || role === 'GUARDIAN' ? (
          <Stack.Screen name="ParentMain" component={ParentTabs} />
        ) : role === 'TEACHER' ? (
          <Stack.Screen name="TeacherMain" component={TeacherTabs} />
        ) : (
          <Stack.Screen name="ParentMain" component={ParentTabs} />
        )}
      </Stack.Navigator>
    </>
  );
}

export default function App() {
  useEffect(()=>{
    // Background sync when connectivity returns
    const unsubscribe = NetInfo.addEventListener(state => {
      if (state.isConnected) {
        SyncService.processQueue();
      }
    });

    // AppState for biometric lock after 5 min background
    let backgroundTime = null;
    const sub = AppState.addEventListener('change', nextState => {
      if (nextState === 'background') backgroundTime = Date.now();
      if (nextState === 'active' && backgroundTime && Date.now() - backgroundTime > 5*60*1000) {
        // trigger biometric lock via AuthContext
      }
    });

    return ()=>{ unsubscribe(); sub.remove(); };
  },[]);

  return (
    <AuthProvider>
      <OfflineProvider>
        <NavigationContainer>
          <RootNavigator />
        </NavigationContainer>
      </OfflineProvider>
    </AuthProvider>
  );
}
