/**
 * OfflineContext - Offline read of recently viewed data and offline attendance capture that syncs when connectivity returns
 * Clear indicator of what is stale when offline
 * Small install size and low data usage
 */

import React, { createContext, useContext, useState, useEffect } from 'react';
import NetInfo from '@react-native-community/netinfo';
import * as SQLite from 'expo-sqlite';

const OfflineContext = createContext();

let db = null;
async function getDb() {
  if (!db) {
    db = await SQLite.openDatabaseAsync('learncloud_mobile.db');
    await db.execAsync(`
      CREATE TABLE IF NOT EXISTS cached_data (key TEXT PRIMARY KEY, tenant_id INTEGER, user_id INTEGER, data_json TEXT, last_updated_at INTEGER, stale_after_hours INTEGER);
      CREATE TABLE IF NOT EXISTS offline_queue (id INTEGER PRIMARY KEY AUTOINCREMENT, tenant_id INTEGER, user_id INTEGER, endpoint TEXT, method TEXT, body_json TEXT, created_at INTEGER, retry_count INTEGER DEFAULT 0, status TEXT DEFAULT 'pending', base_version TEXT, idempotency_key TEXT);
      CREATE TABLE IF NOT EXISTS offline_conflicts (id INTEGER PRIMARY KEY AUTOINCREMENT, queue_id INTEGER, server_data_json TEXT, local_data_json TEXT, diff_json TEXT, created_at INTEGER);
      CREATE TABLE IF NOT EXISTS attendance_records_local (id INTEGER PRIMARY KEY AUTOINCREMENT, tenant_id INTEGER, grade_id INTEGER, stream_id INTEGER, attendance_date TEXT, period_number INTEGER, student_id INTEGER, status TEXT, absence_reason TEXT, note TEXT, last_updated INTEGER);
      CREATE TABLE IF NOT EXISTS marks_rows_local (id INTEGER PRIMARY KEY AUTOINCREMENT, tenant_id INTEGER, assessment_id INTEGER, student_id INTEGER, score REAL, is_absent INTEGER, comment TEXT, last_updated INTEGER);
    `);
  }
  return db;
}

export function OfflineProvider({ children }) {
  const [isConnected, setIsConnected] = useState(true);
  const [lastSynced, setLastSynced] = useState(null);
  const [isStale, setIsStale] = useState(false);

  useEffect(()=>{
    const unsubscribe = NetInfo.addEventListener(state => {
      setIsConnected(!!state.isConnected);
      if (state.isConnected) setLastSynced(Date.now());
    });
    // Check staleness every minute
    const interval = setInterval(()=>{
      if (lastSynced && Date.now() - lastSynced > 6*3600*1000) setIsStale(true);
      else setIsStale(false);
    }, 60000);
    return ()=>{ unsubscribe(); clearInterval(interval); };
  },[lastSynced]);

  const cacheData = async (key, tenantId, userId, data, staleAfterHours=12) => {
    const database = await getDb();
    const now = Date.now();
    await database.runAsync(
      'INSERT OR REPLACE INTO cached_data (key, tenant_id, user_id, data_json, last_updated_at, stale_after_hours) VALUES (?,?,?,?,?,?)',
      [key, tenantId, userId, JSON.stringify(data), now, staleAfterHours]
    );
    setLastSynced(now);
  };

  const getCachedData = async (key) => {
    const database = await getDb();
    const row = await database.getFirstAsync('SELECT * FROM cached_data WHERE key = ?', [key]);
    if (!row) return { data: null, isStale: true, lastUpdated: null };
    const ageHours = (Date.now() - row.last_updated_at) / 3600000;
    const isStaleData = ageHours > row.stale_after_hours;
    return { data: JSON.parse(row.data_json), isStale: isStaleData, lastUpdated: row.last_updated_at };
  };

  const enqueue = async (tenantId, userId, endpoint, method, body, baseVersion) => {
    const database = await getDb();
    const idempotencyKey = `${tenantId}:${userId}:${Date.now()}:${Math.random().toString(36).slice(2)}`;
    await database.runAsync(
      'INSERT INTO offline_queue (tenant_id, user_id, endpoint, method, body_json, created_at, retry_count, status, base_version, idempotency_key) VALUES (?,?,?,?,?,?,?,?,?,?)',
      [tenantId, userId, endpoint, method, JSON.stringify(body), Date.now(), 0, 'pending', baseVersion||'', idempotencyKey]
    );
    return idempotencyKey;
  };

  return (
    <OfflineContext.Provider value={{ isConnected, lastSynced, isStale, cacheData, getCachedData, enqueue, getDb }}>
      {children}
    </OfflineContext.Provider>
  );
}

export const useOffline = () => useContext(OfflineContext);
