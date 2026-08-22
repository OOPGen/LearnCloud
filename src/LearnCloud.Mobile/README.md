# LearnCloud Mobile App - React Native (Expo) - Parents First, Teachers Second
**Consumes existing API, no new backend patterns, offline strategy approved: manual merge for attendance duplicate, server-wins for locked marks, stale thresholds 6-12-24h, background sync always**

## Architecture

- **Stack:** React Native Expo SDK 51, React Navigation bottom tabs, Expo SQLite (offline cache + queue), Expo SecureStore (refresh token), Expo Local Authentication (biometric), Expo Notifications (push), NetInfo offline detection, Hermes enabled, RAM bundles, inline requires
- **API:** Existing REST: /api/auth/login (JWT 15min + refresh 14d rotating), /api/parent/* (children, home, fees, attendance, results, notices, homework, messages), /api/teacher/* (dashboard, classes, attendance, marks, homework, lesson plans), /api/attendance/* (register mark), /api/marks/*, /api/fees/*, /api/messaging/*
- **Auth:** JWT access 15min stored memory, refresh token hashed in DB, rotating, stored encrypted SecureStore, biometric unlock after 5 min background, fallback PIN
- **Offline:** SQLite tables: cached_data (key, tenant_id, user_id, data_json, last_updated_at, stale_after_hours), offline_queue (id, tenant_id, user_id, endpoint, method, body_json, created_at, retry_count, status pending/syncing/failed/conflict, base_version, idempotency_key), offline_conflicts (queue_id, server_data_json, local_data_json, diff_json), marks_grids_local, attendance_records_local
- **Sync:** Background job via useEffect NetInfo isConnected true and app foreground, processes queue order created_at ASC, sends If-Unmodified-Since base_version, idempotency header X-Idempotency-Key, exponential backoff 1s,2s,4s,8s,16s + jitter, max 5 retries, then failed status shows Sync failed tap to retry
- **Conflict Handling:** Attendance duplicate register → 409 Conflict with server payload, store in offline_conflicts, show Conflict Dialog Keep Server (default if dismissed) / Keep Mine (force=true + audit) / Merge per student, audit old/new values reason offline_conflict_resolved. Marks locked → server wins, offline rejected, requires unlock via head approval, dialog View Server Version / Discard Mine
- **Stale Indicator:** Global banner Offline • Last synced 2h ago • Showing cached data when isConnected false, per-card badge Stale Updated 5h ago grey opacity 0.7, border, icon 🕒
- **Push Notifications:** Expo Push Notifications (FCM/APNS), categories: notices, absence alerts, published results, fee reminders, per-category preferences stored locally AsyncStorage + server guardian_contact_preferences / notification_preferences notify_notices, notify_absence, notify_results, notify_fees booleans, deep link to relevant screen on tap
- **Small Install Size & Low Data:** Hermes, RAM bundles, inline requires, no lodash/moment/Lottie, images WebP, compressed API gzip, pagination 25, minimal fields ?fields=minimal, delta sync If-Modified-Since, background sync always (approved), cache TTL 24h, auto-play no
- **Biometric Unlock:** expo-local-authentication, fallback device PIN, app lock after 5 min background, secure storage

## Parent Features

- **Login:** Email, password, invitation flow school triggers invitation with token 7 days, accept invitation set password create user linked to guardian via guardian.user_id, account links to one or more children potentially different classes (guardian_student_links), SecureStore refresh token, biometric unlock
- **Child Switcher:** Header select big font 16px rounded-full border-2 primary-200 bg-primary-50 min-w 140px readable at arm's length, plus horizontal chips quick switch, stored selectedChildId AsyncStorage resumable, only own children enforced server-side via ParentAuthorizationService IsGuardianOfStudent
- **Home per Child:** Balance outstanding this term, attendance % this term, latest published results average grade, upcoming assessments next 14 days, recent notices, homework due — cached after first fetch, stale 12h/6h
- **Fees:** Statement lines date/type/number/debit/credit/balance, invoice history, receipts, downloadable PDF (cached PDF if previously opened), payment action where gateway enabled (PayNow card/bank/mobile money) — payment requires online, button disabled offline with message Payment requires internet, uses same allocation FIFO rules as web
- **Attendance Detail:** Dates status reason note period, summary total/present/absent/late/excused percentage isChronic
- **Results:** Published report cards only downloadable PDF, only published visible enforced server-side, draft/approved not visible
- **Notices and Homework**
- **Message to Class Teacher if school enables it, with moderation and rate limiting 5/hour 20/day — offline draft saved locally, queued offline sync when online, status pending moderation badge
- **Profile and Contact Preferences including SMS opt-out** — sms_opt_in/email_opt_in hard opt-out always honoured, filtered before send

## Teacher Features

- **Dashboard:** Today's timetable (timetable_slots teacher_staff_id = me dayOfWeek = today effective dated), registers still to be marked today (attendance_registers existence check per class/date/period), marks deadlines approaching (assessments where teacher teaches subject, dueDate <=7 days, count marked/unmarked), unread notices
- **My Classes:** Only classes teacher assigned to, enforced server-side via timetable_slots teacher_staff_id OR streams class_teacher_staff_id
- **Attendance Capture Reusing Register Screen:** Whole class listed, one tap cycles P/A/L/E/S, mark all present, autosave to SQLite instantly, queued for sync, explicit conflict handling duplicate register Keep Server default/manual merge, usable one-handed 44px targets thumb zone bottom
- **Marks Entry:** Keyboard navigable grid for assessment, autosave local SQLite every 3s, validation against max, draft + submit locks pending approval, submit queued, conflict handling server wins if locked, requires unlock via head approval

## Tests

- Teacher cannot access another teacher's class must fail 401 — TeacherPortalAuthorizationTests
- Parent cannot read another family's child must fail — ParentPortalAuthorizationTests
- Student cannot access another student's record — StudentPortalAuthorizationTests
- Attendance duplicate conflict dialog appears, Keep Server default preserves server
- Marks locked server wins, offline rejected
- Stale indicator shows when offline and data older than threshold
- Push per-category preferences honoured
- Biometric unlock fallback PIN

## How to Run

```bash
cd src/LearnCloud.Mobile
npm install
npx expo start --tunnel
# Scan QR with Expo Go on mid-range phone Tecno Spark
# Login as parent: parent@petra.test / Test123!@#
# Login as teacher: teacher@petra.test / Test123!@#
```

Env: API_URL=https://api.learncloud.co.zw, EXPO_PROJECT_ID, SENTRY_DSN optional

