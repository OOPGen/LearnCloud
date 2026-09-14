# LearnCloud Mobile App — Offline Synchronisation Strategy & Conflict Resolution Rules
**Must be approved before writing any code — per your instruction**
**Target: Parents first, teachers second, React Native, consumes existing API, no new backend patterns**
**HQ Bulawayo, slow 3G, mid-range phones, shared/low-end devices**

## 1. Goals & Constraints

- **Small install size and low data usage:** Hermes enabled, RAM bundles, inline requires, no heavy libs (no lodash, moment, no Lottie), optimized images WebP, compressed API JSON (gzip), pagination 25 items, only fetch recently viewed, cache TTL 24h, background sync only on WiFi if user setting, delta sync via If-Modified-Since / ETag
- **Biometric unlock:** expo-local-authentication or react-native-biometrics, fallback to device PIN, app lock after 5 min background, secure storage for refresh token via expo-secure-store / react-native-keychain
- **Clear indicator of what is stale when offline:** Global banner "Offline • Showing cached data • Last updated 2h ago" + per-card badge "Stale" if data older than threshold (attendance summary >6h, balance >12h, timetable >24h), plus greyed-out + icon
- **Push notifications:** notices, absence alerts, published results, fee reminders, with per-category preferences stored locally and server-side in `guardian_contact_preferences` / `notification_preferences`
- **Consumes existing API:** No new backend patterns — uses existing `/api/parent/*`, `/api/teacher/*`, `/api/attendance/*`, `/api/marks/*`, `/api/fees/*`, `/api/messaging/*`, `/api/auth/*` with same tenant_id leading filter and permission checks

## 2. Offline Scope — What is Offline-Capable?

**Parent features (mostly read-only, offline read of recently viewed data):**

- **Home per child:** balance, attendance %, latest result, upcoming assessments, recent notices, homework due — cached after first online fetch, offline read from SQLite cache, stale indicator
- **Fees:** statement, invoice history, receipts — cached, read offline, PDF download requires online (or cached PDF if previously viewed)
- **Attendance detail:** dates and reasons — cached
- **Results:** published report cards only — cached, PDF cached if previously opened
- **Notices and homework:** cached
- **Payment where gateway enabled:** **NOT offline-capable** — initiation requires online (gateway needs live), button disabled offline with message "Payment requires internet — connect to pay". Receipts/history readable offline if cached.
- **Message to teacher:** **NOT offline-capable for sending**, draft saved locally, queued offline, sync when online (see below)

**Teacher features (mutable, offline write):**

- **Today's timetable:** read-only cached, stale indicator
- **Attendance capture:** **Fully offline-capable write** — whole class listed, one tap cycle P/A/L/E/S, mark all present, autosave to local SQLite instantly, queued for sync when connectivity returns, explicit conflict handling
- **Marks entry:** **Offline-capable write with restrictions:** grid keyboard navigable, autosave local SQLite, validation against max, draft save, explicit submit locks pending approval. Submit requires online if assessment already submitted? Draft offline allowed, submit queued.
- **Homework, lesson plans, learners view:** read-only cached

**Not offline:**
- Login (requires online first time, then biometric unlock offline with refresh token cached securely, but refresh requires online)
- Payment initiation
- Report card bulk PDF generation
- Admin actions (extend trial, etc.)

## 3. Cache Strategy — Offline Read of Recently Viewed Data

**Storage:** Expo SQLite via `expo-sqlite` or WatermelonDB (SQLite) for structured, AsyncStorage for small prefs, SecureStore for tokens. No Realm (large size).

**What is cached:**
- Last 10 children home screens per guardian (balance, attendance %, latest result)
- Last 5 fee statements, 20 invoices, 20 receipts per child (if viewed)
- Last 100 attendance records per child
- Last 10 published report cards per child + PDFs if opened
- Last 50 notices, 50 homework per child
- Timetable week per teacher (5 days)
- Last 5 attendance registers per class per teacher (for quick re-mark)
- Last 5 marks grids per assessment (draft)

**TTL and stale indicator:**
- Home: stale after 12h
- Balance/fees: stale after 12h (money sensitive, show warning)
- Attendance: stale after 6h
- Results: stale after 24h (report cards don't change often)
- Timetable: stale after 24h
- Notices/homework: stale after 6h

**UI indicator:**
- Global banner top: `Offline • Last synced 2h ago • Showing cached data` when NetInfo isConnected false
- Per-card: small badge `Stale • Updated 5h ago` grey, plus icon 🕒, and greyed-out opacity 0.7
- Pull-to-refresh forces online fetch when connected, updates timestamp

**Cache invalidation:**
- On online fetch success, overwrite SQLite row and update `last_updated_at`
- On push notification received (e.g., new result published), invalidate that child's result cache and refetch when next online or show "New result available — pull to refresh"

**Low data usage:**
- API returns minimal fields for mobile: `?fields=minimal` query param, compressed, pagination 25, no images in list, images lazy
- Delta sync: Send `If-Modified-Since` header with last_updated_at, server returns 304 Not Modified if unchanged
- Background sync only on WiFi if user setting "Sync on WiFi only" true

## 4. Offline Write — Attendance Capture & Marks Entry That Syncs When Connectivity Returns

**General offline queue pattern (no new backend):**

- All offline writes go to local queue table `offline_queue` SQLite: id, tenant_id, user_id, endpoint (e.g. `POST /api/attendance/mark`), method, body JSON, created_at, retry_count, status pending/syncing/failed/conflict, last_error
- When NetInfo isConnected true and app foreground, background sync job processes queue in order created_at ASC
- Each queue item: try to call existing API endpoint with same body and auth header (JWT refresh if needed)
- On success 2xx: mark status synced, remove from queue (or keep as synced for audit 24h)
- On failure 4xx/5xx: retry with exponential backoff 2^retry * 1s + jitter, max 5 retries, then mark failed and show in UI "Sync failed — tap to retry" with error reason
- For attendance, autosave every 5 seconds to both local SQLite register table and queue (debounced)

**Attendance capture offline:**

- Teacher opens register for class/date/period. If online, fetch existing register from API, cache to SQLite. If offline, load from SQLite cache if exists, else empty new register.
- Teacher taps to cycle status P/A/L/E/S, enters reason/note. Each tap updates SQLite `attendance_records_local` table instantly (optimistic), and enqueues or updates queue item for that class/date/period bulk
- Queue item body: `MarkRegisterRequest` gradeId streamId attendanceDate periodNumber academicYearId termId items [{studentId, status, absenceReason, note}]
- When online returns, sync job POSTs to `/api/attendance/mark`
- **Explicit conflict handling for attendance (see Section 5)**

**Marks entry offline:**

- Teacher opens marks grid for assessmentId. If online, fetch grid, cache to SQLite `marks_grids_local` + `marks_rows_local`
- Offline edits: update local SQLite row, enqueue queue item `SaveMarksRequest` items [{studentId, score, isAbsent, comment}] draft
- Submit action (locks pending approval) is queued as separate request `POST /marks/{id}/submit` with flag requires online? For V1, allow submit offline queued, but server will validate if assessment already submitted/approved — may reject as conflict, then show conflict UI

**Message to teacher offline:**

- Parent composes message to class teacher, if offline, save draft to SQLite `messages_draft` and enqueue `POST /api/parent/children/{id}/messages`, sync when online

**Payment offline:** Not allowed, button disabled, message "Payment requires internet"

## 5. Conflict Resolution Rules — Explicit

Conflict occurs when teacher A and teacher B (or same teacher on two devices) mark same class/date/period or same assessment marks while offline, then both sync, or when offline edit conflicts with server version that changed after offline cache was fetched.

**We use combination of Last-Write-Wins with Audit + Manual Merge UI for attendance, and Server-Wins for locked marks — never silent overwrite without user visible warning.**

### 5.1 Attendance Register Conflicts

**Conflict detection:** When syncing attendance register for (tenant, grade, stream, date, period), server checks if register already exists with different updated_at than the base version client fetched (optimistic concurrency via `If-Unmodified-Since` or version number). If exists, it's a conflict.

**Types:**

- **Type A: Duplicate register — another teacher already marked same class/date/period while you were offline**
  - Server has register with records, client tries to create new register same key
  - **Resolution:** Not overwrite. Server returns 409 Conflict with existing register payload. Mobile app shows **Conflict Dialog**: "This register was already marked by Mrs Moyo at 08:15 today. Your offline marks: 30 present, 5 absent. Server marks: 32 present, 3 absent. What to do? [Keep Server] [Keep Mine] [Merge per student — show diff]" 
  - Options:
    - Keep Server: discard local, load server version
    - Keep Mine: overwrite server with local (requires `?force=true` header and logs audit with reason "Offline conflict overwrite by teacher X, 5 differences")
    - Merge: show per-student diff list, teacher taps per student which status to keep, then save merged
  - Audit: all conflict resolutions logged to audit_logs with old/new values, reason "offline_conflict_resolved"
  - Default if teacher dismisses dialog without choosing: Keep Server, local queued item marked as conflict, requires manual resolution before can sync again (prevents silent overwrite)

- **Type B: Same register edited by two teachers offline (both modified same student status different)**
  - Example: Student Thabo marked Present by Teacher A offline, Absent by Teacher B offline, both sync
  - Server detection: last-write-wins based on marked_at timestamp, but with warning: first sync wins, second sync gets conflict response with diff, same dialog as above
  - **Rule:** Last write wins only after explicit user choice in merge UI, not automatic. If teacher chooses Keep Mine, it overwrites. This is explicit conflict handling.

- **Type C: Backdating conflict — offline mark for date beyond backdating window**
  - Client allows backdating offline (e.g., teacher marks yesterday attendance while offline, but backdating window is 7 days, yesterday is within, okay). If offline mark for 10 days ago and window is 7 days, server will flag is_backdated true and audit log backdated_attendance. Mobile app shows warning "Backdated — flagged in audit" after sync, but does not block.

**Implementation details:**

- Each attendance register local row stores `base_version` = server `updated_at` timestamp when fetched (for optimistic concurrency)
- On sync, send header `If-Unmodified-Since: base_version` or body `baseVersion`
- Server compares base_version vs current server updated_at, if mismatch → 409 Conflict with server payload
- Mobile app stores conflict in `offline_conflicts` table and shows UI

### 5.2 Marks Entry Conflicts

**Conflict detection:**

- Assessment marks have states: draft, submitted, approved_by_hod, approved_by_head, locked
- Offline edits allowed only when status draft. If teacher edits offline while server status is already submitted/approved/locked, conflict

**Types:**

- **Type D: Marks already submitted/approved/locked on server while teacher edited offline draft**
  - Example: Teacher A edits marks offline draft, Teacher B (HOD) approves on web while A offline, then A syncs
  - **Resolution:** Server wins, offline changes rejected. Mobile app shows dialog: "Marks for Math Test 1 were already submitted by Mrs Moyo at 09:00 and approved by Head. Your offline changes (3 scores) cannot be saved because marks are locked pending approval. [View Server Version] [Discard Mine]"
  - No automatic overwrite of locked marks. Reason: academic integrity, approval chain must not be bypassed by offline edit.
  - If teacher really needs to change locked marks, must request unlock via `marks.unlock` permission which requires head approval and audited reason, then re-edit online

- **Type E: Two teachers edit same assessment marks offline (same subject but different streams? Should not happen because teacher scoped to class). If same class subject, last-write-wins per student with merge UI similar to attendance, but server checks if assessment already submitted — if submitted, reject as above**

- **Type F: Score exceeds max after max changed server-side**
  - Assessment max changed from 100 to 50 while offline, offline score 80 now > max 50
  - Resolution: On sync, server validation fails score > max, returns 400 with message, mobile shows error per student and allows edit to fix

**Implementation:**

- Marks grid local row stores `base_version` = assessment `updated_at` + existing mark `updated_at`
- On sync save draft, server checks assessment status, if not draft returns 409 with current server grid
- Submit locks: `POST /marks/{id}/submit` offline queued, on sync if assessment already submitted returns conflict

### 5.3 Read-Only Data Conflicts (Fees, Reports, Notices) — No Conflict, Stale Wins

- Parent fees, attendance, results, notices, homework are read-only for parent/student. No write conflict, only stale read.
- If server data changed while offline (e.g., new invoice issued), next online fetch overwrites cache, no conflict. Stale indicator shows until refreshed.

### 5.4 Two-Way Sync Principles

- **Client wins for draft attendance/marks not yet submitted on server** — after explicit merge choice for duplicate register case
- **Server wins for locked/submitted/approved state** — offline draft cannot overwrite locked
- **Last-write-wins only with explicit user confirmation** for attendance duplicate register
- **Never silent overwrite** — always show conflict dialog with diff, require user action, audit log reason
- **Audit all conflict resolutions** to `audit_logs` with oldValues (server) newValues (chosen) and reason "offline_conflict_resolved by teacher X chose keep mine 5 differences"

### 5.5 Out-of-Order and Retry Safety

- Queue items processed in order created_at ASC per user to preserve causality (e.g., create register then add note)
- Each queue item has idempotency key `clientId` = `tenantId:userId:timestamp:random` sent as header `X-Idempotency-Key`, server stores in `idempotency_keys` table to detect duplicate due to retry (network failure after server processed but client didn't get response). If idempotency key already processed, server returns previous result 200, not duplicate.
- Retry with exponential backoff 1s, 2s, 4s, 8s, 16s + jitter, max 5 retries, then mark failed and show "Sync failed — tap to retry" with error reason, keeps local data

### 5.6 Offline Queue Persistence and App Kill

- Queue stored in SQLite, survives app kill, device restart
- On app start, if NetInfo connected, auto-start sync job
- If app killed mid-sync, on next start resume from last pending item, no data loss

## 6. Push Notifications

- Categories: notices (general), absence alerts (attendance marked absent), published results (report card published), fee reminders (invoice due 7 days, arrears over threshold)
- Per-category preferences: stored locally in AsyncStorage + server-side in `guardian_contact_preferences` / `notification_preferences` table with columns notify_notices, notify_absence, notify_results, notify_fees, each bool, plus channel sms/email/push
- Push provider: Expo Push Notifications (FCM/APNS) for V1, swappable same as SMS/email via abstraction
- On notification tap, deep link to relevant screen: absence alert → attendance detail, result published → results tab, fee reminder → fees statement, notice → notices tab
- Opt-out: per-category toggle, always honoured, filtered before send

## 7. Small Install Size and Low Data Usage

- **Install size:** Hermes enabled, RAM bundles true, inline requires true, no heavy libs: no lodash (use native), no moment (use date-fns light or Intl), no Lottie, no SVG lib heavy, images WebP, startup <2s on mid-range Android (Tecno Spark, 2GB RAM)
- **Low data:** API compressed gzip, pagination 25, minimal fields `?fields=minimal`, delta sync If-Modified-Since, background sync only on WiFi if setting "Sync on WiFi only" true, cache TTL 24h, no auto-play videos, no large fonts
- **Measured:** Install size target <30MB APK, <40MB AAB, JS bundle <1MB gz

## 8. Clear Indicator of What is Stale When Offline

- Global banner top: "Offline • Last synced 2h ago • Showing cached data" when NetInfo isConnected false, with retry button
- Per-card badge: "Stale • Updated 5h ago" grey with clock icon, opacity 0.7, plus border
- Fees balance: if stale >12h show warning "Balance may be outdated — pull to refresh when online"
- Timetable: if stale >24h show "Timetable may have changed — check when online"
- Attendance summary: if stale >6h show "Attendance may be outdated"
- Pull-to-refresh on every list when online forces fetch and updates last_updated_at

## 9. Security

- Refresh token stored in SecureStore encrypted, access token in memory only
- Biometric unlock: expo-local-authentication, fallback device PIN, app lock after 5 min background, secure storage
- Offline data encrypted: SQLite with expo-sqlite encrypted? For V1, AsyncStorage not encrypted but SQLite file in app sandbox, plus sensitive fields (balance) not cached beyond TTL 24h
- Tenant isolation via JWT tid claim + guardian-child link enforced server-side, even offline cache filtered by tenant_id

## 10. Testing Strategy for Offline

- Unit tests: attendance percentage calc, allocation, conflict detection logic (detect duplicate register, detect locked marks)
- Integration tests: simulate offline queue 5 items, go online, sync in order, verify server state matches expected after conflicts resolved
- Manual tests: teacher A marks offline, teacher B marks same register online, A goes online → conflict dialog appears, choose merge, verify audit log
- Network link conditioner: test on 3G slow, offline, airplane mode, app kill mid-sync

---

## 11. Open Policy Questions for You (Before Code)

Before I code, confirm these policy choices built into conflict resolution:

1. **Attendance duplicate register:** Default keep server or require manual merge? Proposal require manual merge dialog, default keep server if dismissed.

2. **Marks locked:** Server wins, offline changes rejected, requires unlock via head approval. Confirm?

3. **Attendance backdating beyond window:** Allow offline but flag in audit on sync warning, or block offline backdating beyond window? Proposal allow but flag.

4. **Stale thresholds:** Home 12h, balance 12h, attendance 6h, results 24h, timetable 24h, notices 6h — OK?

5. **Background sync:** Only on WiFi if user setting enabled, or always on any data? Proposal always, but setting to restrict to WiFi only available.

6. **Biometric lock timeout:** 5 min background → lock, require biometric/PIN to unlock. OK?

Once approved, I will build:

- React Native app (Expo) with navigation bottom tabs, auth with JWT + refresh + biometric, SQLite WatermelonDB, NetInfo offline detection, queue table, sync service with conflict dialog, stale banner, push notifications per category, small install size.

