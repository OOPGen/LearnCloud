import React, { createContext, useContext, useState, useEffect } from 'react';
import * as SecureStore from 'expo-secure-store';
import * as LocalAuthentication from 'expo-local-authentication';
import { AppState } from 'react-native';

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [role, setRole] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isBiometricLocked, setIsBiometricLocked] = useState(false);
  const [backgroundTime, setBackgroundTime] = useState(null);

  useEffect(()=>{
    (async()=>{
      const token = await SecureStore.getItemAsync('access_token');
      const userJson = await SecureStore.getItemAsync('user');
      if(token && userJson){
        const u = JSON.parse(userJson);
        setUser(u);
        setRole(u.role || 'PARENT');
        // Check biometric lock
        const lastBg = await SecureStore.getItemAsync('background_time');
        if(lastBg && Date.now() - parseInt(lastBg) > 5*60*1000){
          setIsBiometricLocked(true);
        }
      }
      setIsLoading(false);
    })();

    const sub = AppState.addEventListener('change', async nextState=>{
      if(nextState==='background'){
        setBackgroundTime(Date.now());
        await SecureStore.setItemAsync('background_time', Date.now().toString());
      }
      if(nextState==='active' && backgroundTime && Date.now() - backgroundTime > 5*60*1000){
        setIsBiometricLocked(true);
      }
    });
    return ()=>sub.remove();
  },[]);

  const login = async (email, password, tenantSlug) => {
    const res = await fetch(`${process.env.EXPO_PUBLIC_API_URL||'https://api.learncloud.co.zw'}/api/auth/login`,{
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body: JSON.stringify({email, password, tenantSlug})
    });
    if(!res.ok) throw new Error('Invalid credentials');
    const data = await res.json();
    await SecureStore.setItemAsync('access_token', data.accessToken);
    await SecureStore.setItemAsync('refresh_token', data.refreshToken);
    await SecureStore.setItemAsync('user', JSON.stringify({id:data.userId, displayName:data.displayName, role:'PARENT', tenantId:data.tenantId}));
    setUser({id:data.userId, displayName:data.displayName, tenantId:data.tenantId});
    setRole('PARENT');
  };

  const logout = async ()=>{
    await SecureStore.deleteItemAsync('access_token');
    await SecureStore.deleteItemAsync('refresh_token');
    await SecureStore.deleteItemAsync('user');
    setUser(null);
  };

  const unlockWithBiometric = async ()=>{
    const compatible = await LocalAuthentication.hasHardwareAsync();
    const enrolled = await LocalAuthentication.isEnrolledAsync();
    if(!compatible || !enrolled){
      setIsBiometricLocked(false);
      return;
    }
    const result = await LocalAuthentication.authenticateAsync({promptMessage:'Unlock LearnCloud', fallbackLabel:'Use PIN'});
    if(result.success){
      setIsBiometricLocked(false);
      await SecureStore.deleteItemAsync('background_time');
    }
  };

  return (
    <AuthContext.Provider value={{user, role, isLoading, isBiometricLocked, login, logout, unlockWithBiometric}}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
