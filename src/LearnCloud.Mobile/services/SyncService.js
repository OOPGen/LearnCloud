/**
 * SyncService - Offline queue that syncs when connectivity returns, with explicit conflict handling
 * Conflict resolution rules: Attendance duplicate = manual merge dialog default Keep Server, Marks locked = server wins, no silent overwrite
 */

import * as SQLite from 'expo-sqlite';
import { Platform } from 'react-native';

let db = null;
async function getDb() {
  if (!db) db = await SQLite.openDatabaseAsync('learncloud_mobile.db');
  return db;
}

export const SyncService = {
  async processQueue() {
    const database = await getDb();
    const pending = await database.getAllAsync("SELECT * FROM offline_queue WHERE status IN ('pending','failed') ORDER BY created_at ASC LIMIT 20");

    for (const item of pending) {
      try {
        await database.runAsync("UPDATE offline_queue SET status='syncing', retry_count=retry_count+1 WHERE id=?", [item.id]);

        // Call existing API endpoint with same body and headers - no new backend patterns
        const response = await fetch(item.endpoint, {
          method: item.method,
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${await getAccessToken()}`,
            'If-Unmodified-Since': item.base_version || '',
            'X-Idempotency-Key': item.idempotency_key
          },
          body: item.body_json
        });

        if (response.status === 409) {
          // Conflict - attendance register already exists or marks locked
          const serverData = await response.json();
          await database.runAsync(
            "INSERT INTO offline_conflicts (queue_id, server_data_json, local_data_json, diff_json, created_at) VALUES (?,?,?,?,?)",
            [item.id, JSON.stringify(serverData), item.body_json, JSON.stringify({server: serverData, local: JSON.parse(item.body_json)}), Date.now()]
          );
          await database.runAsync("UPDATE offline_queue SET status='conflict', last_error=? WHERE id=?", [`Conflict: ${JSON.stringify(serverData).slice(0,200)}`, item.id]);
          // Do not auto-retry conflict, require manual merge UI
          continue;
        }

        if (response.ok) {
          await database.runAsync("UPDATE offline_queue SET status='synced' WHERE id=?", [item.id]);
        } else if (response.status >= 400 && response.status < 500) {
          // Client error 4xx - don't retry infinitely, mark failed
          const errText = await response.text();
          if (response.status === 423) { // locked
            await database.runAsync("UPDATE offline_queue SET status='conflict', last_error=? WHERE id=?", [`Locked: ${errText}`, item.id]);
          } else {
            await database.runAsync("UPDATE offline_queue SET status='failed', last_error=? WHERE id=?", [errText.slice(0,500), item.id]);
          }
        } else {
          // Server error 5xx - retry with backoff
          const retryCount = item.retry_count + 1;
          if (retryCount >= 5) {
            await database.runAsync("UPDATE offline_queue SET status='failed', last_error='Max retries exceeded' WHERE id=?", [item.id]);
          } else {
            const backoff = Math.pow(2, retryCount) * 1000 + Math.random()*1000; // 1s,2s,4s,8s,16s + jitter
            await database.runAsync("UPDATE offline_queue SET status='pending', retry_count=? WHERE id=?", [retryCount, item.id]);
            await new Promise(r => setTimeout(r, backoff));
          }
        }
      } catch (e) {
        console.log("Sync error", e);
        await database.runAsync("UPDATE offline_queue SET status='failed', last_error=? WHERE id=?", [String(e).slice(0,500), item.id]);
      }
    }
  }
};

async function getAccessToken() {
  // In real app, get from SecureStore or memory
  try {
    const SecureStore = require('expo-secure-store');
    return await SecureStore.getItemAsync('access_token') || '';
  } catch {
    return '';
  }
}
