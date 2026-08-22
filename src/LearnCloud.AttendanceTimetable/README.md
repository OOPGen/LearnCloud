# Attendance & Timetable Module - LearnCloud V1
**HQ Bulawayo, Mixed Hardware, Slow 3G, One-Handed Phone Use**

## Attendance

### Two Modes Configured Per Tenant

- `TenantAttendanceSettings` table tenant_id unique, mode daily/per_period, backdating_window_days default 7, allow_backdating_beyond_window true but flagged, chronic_absence_threshold 85%, count_late_as_present true, count_excused_as_present true, count_sick_as_present false, auto_save_interval_seconds 5
- GET/PUT /api/attendance/settings
- Daily mode: period_number null, one record per student per date per class
- Per-period mode: period_number required, one record per student per date per period

### Register Capture Screen Optimized for Speed

**Frontend:** `Frontend/AttendanceRegisterCapture.jsx`

- Whole class listed, ordered by lastName, photo initials circle 32px
- One tap per learner to cycle present → absent → late → excused → sick → present (STATUS_CYCLE)
- 44px min-h-touch targets, 48px status button w-12 h-12, thumb zone bottom sticky Mark All Present + Save
- Mark all present action: sets all to present in one tap
- Autosave every 5 seconds if unsaved (useRef timeout), clear indicator unsaved changes (amber pulse) vs saved green check with timestamp
- Usable one-handed on phone: bottom fixed buttons in thumb reach, swipe? One tap cycle, large tap targets, no small dropdowns
- Absence reason and optional note: appears when status != present, inputs for reason (sick, family...) and note 255 chars
- State: records map studentId -> {status, absenceReason, note}, unsaved bool, lastSaved Date, isBackdated flag
- Load: GET /api/attendance/register?gradeId&streamId&date&periodNumber&academicYearId&termId returns RegisterResponseDto header + students + records
- Save: POST /api/attendance/mark bulk items, returns MarkRegisterResponse with isBackdated flag

### Guard Against Duplicate Registers

- `attendance_registers` table unique key `(tenant_id, grade_id, stream_id, attendance_date, period_number, academic_year_id, term_id)` 
- Service checks existingRegister, if exists updates instead of creating new (upsert), deletes old records then inserts new - prevents duplicate
- Frontend handles as update, not error, but audit logs old vs new

### Backdating Allowed Within Configurable Window, Flagged in Audit Log

- TenantAttendanceSettings.BackdatingWindowDays = 7 default
- Service calculates daysDiff = today - attendanceDate, if > window => isBackdated=true
- If AllowBackdatingBeyondWindow false and beyond window -> InvalidOperationException
- If allowed but beyond window, sets register.is_backdated true, record.is_backdated true, writes AuditLog Action=backdated_attendance with oldValues date/daysDiff, newValues backdateReason/windowDays, ip, reason
- UI shows badge "Backdated — flagged in audit"

### Summaries: Per Learner, Per Class, Per Term, Percentage + Chronic Threshold

- `AttendanceService.CalculatePercentage(present,late,excused,sick,absent,total,settings)` => presentCount = present + (CountLateAsPresent?late:0) + (CountExcusedAsPresent?excused:0) + (CountSickAsPresent?sick:0) ; % = presentCount/total*100 rounded 2 decimals
- Unit tests cover all combos
- `GetSummaryAsync`: query AttendanceRecords tenant_id leading index, filter grade/stream/year/term/from/to, group by student, calculate counts, percentage, isChronicAbsence = percentage < threshold, chronicMessage plain language
- Summary DTOs: PerLearner (total, present, absent, late, excused, sick, %, chronic flag), PerClass (totalStudents, avg %, chronicCount, learners sorted by %)
- Frontend `AttendanceSummary.jsx` shows total students, avg %, chronic count, table per learner with flag

### Printable A4 Class Register for Month

- `PrintableMonthRegisterDto`: gradeName, streamName, year, month, monthName, dates list days in month, rows: studentId, studentName, studentNumber, attendanceByDate dict date string -> letter P/A/L/E/S
- Endpoint GET /api/attendance/printable/month?gradeId&streamId&year&month&academicYearId&termId
- Endpoint GET /api/attendance/printable/month/html returns HTML with table border black, greyscale legible letters, print CSS @media print
- Frontend `PrintableMonthRegister` component renders table with color classes but letters ensure greyscale, print button calls window.print()

## Timetable

### Periods Defined Per Tenant with Times and Break Slots

- `PeriodDefinition` tenant_id, period_number, name (Period 1, Break), start_time TIME, end_time TIME, is_break bool, sort_order, academic_year_id nullable (global or per year)
- Endpoints GET /api/timetable/periods (returns 8 periods + breaks sensible defaults if none: Period1 08:00-08:45, Period2 08:45-09:30, Break 09:30-10:00, Period3 10:00-10:45, Period4 10:45-11:30, Lunch 11:30-12:30, Period5 12:30-13:15, Period6 13:15-14:00), POST /periods, POST /periods/bulk
- `BreakSlot` separate optional

### Weekly Grid Editor Assigning Subject and Teacher to Class Period

- `Timetable` effective dated: id, tenant_id, name, academic_year_id, term_id, effective_from Date inclusive, effective_to nullable inclusive null=ongoing, version, status draft/active/archived, created_from_timetable_id for clone
- `TimetableSlot`: tenant_id, timetable_id FK, academic_year_id, term_id, grade_id, stream_id, subject_id, teacher_staff_id, room_id nullable, day_of_week 1=Mon..7=Sun, period_number, start_time, end_time
- Unique constraints: (tenant_id, timetable_id, day, period, stream_id) class double-booked, (tenant_id, timetable_id, day, period, teacher_staff_id) teacher in two places, (tenant_id, timetable_id, day, period, room_id) room conflict if rooms used
- Frontend `TimetableGridEditor.jsx`: fetches periods, grid by class, subjects, teachers, rooms, renders table days Monday-Friday rows, periods columns, cells clickable + Add, selected cell modal with subject/teacher/room selects, Check Clash button, Save Slot, clash dialog plain language, clone info effective dated
- Effective dated: mid-term change creates new timetable version with new effective_from, previous effective_to = new effective_from -1 day, history preserved, FindEffectiveTimetable query effective_from <= date && (effective_to null || >= date) order by version desc

### Clash Detection on Save: Teacher in Two Places, Class Double-Booked, Room Conflict - Explain Plain Language, Do Not Silently Refuse

- `TimetableService.CheckClashAsync(tenantId, timetableId, newSlot, excludeSlotId)`:
  - Loads existing slots same timetable, same day, same period
  - Teacher clash: existing.TeacherStaffId == new.TeacherStaffId -> clash message "Teacher Mrs Moyo is already teaching Form 2B Mathematics with Mrs Moyo in Room 5 at Monday Period 2. A teacher cannot be in two places at once."
  - Class double-booked: existing.GradeId==new.GradeId && StreamId==new.StreamId -> "Class Form 1A is already booked for Math with Mrs Moyo at Monday Period 2. A class cannot have two lessons at same time."
  - Room conflict if rooms used: existing.RoomId==new.RoomId && new.RoomId.HasValue -> "Room Room 5 is already booked by Form 1A at Monday Period 2. Room conflict — two classes cannot use same room."
  - Returns ClashResponseDto hasClash bool + list ClashDetailDto with clashType, message plain language, existingInfo, newInfo, day, period, teacher, grade, stream, room
  - CreateSlot calls CheckClash, if hasClash throws InvalidOperationException with join of messages, controller returns 409 Conflict {message, code CLASH_DETECTED} not silently refuse
  - BulkCreateSlots collects clashes, saves non-clashing, returns partial clash result
- Unit tests for clash detection: teacher clash, class double-booked, room conflict, effective dated does not rewrite history, duplicate register guard, backdating flagged, percentage calculation

### Views by Class and by Teacher, Both Printable

- Endpoints: GET /api/timetable/view/class?gradeId&streamId&academicYearId&termId&effectiveDate, GET /view/teacher?teacherStaffId&..., GET /view? filters
- Service GetGridAsync finds effective timetable for date via FindEffectiveTimetable, loads periods, slots filtered by grade/stream/teacher, maps to TimetableSlotDto with names, groups by day
- Printable HTML: GET /view/class/html and /view/teacher/html returns HTML table border black, print CSS, title
- Frontend ClassTimetableView, TeacherTimetableView with print button

### Backend Endpoints Summary

- Attendance: GET/PUT /settings, GET /register, POST /mark (bulk, autosave), GET /summary, GET /printable/month, GET /printable/month/html
- Timetable: GET/POST /periods, POST /periods/bulk, POST / (create timetable effective dated), GET / (list), GET /{id}, POST /{id}/slots, POST /{id}/slots/check-clash, POST /{id}/slots/bulk, DELETE /{id}/slots/{slotId}, GET /view, GET /view/class, GET /view/teacher, GET /view/class/html, GET /view/teacher/html

### Frontend

- AttendanceRegisterCapture.jsx optimized speed, one-handed, autosave, mark all present, reason/note, backdated flag
- AttendanceSummary.jsx per learner/class/term %, chronic threshold flag
- PrintableMonthRegister.jsx A4 monthly
- TimetableGridEditor.jsx weekly grid, subject+teacher+room assignment, clash dialog plain language, effective dated info, printable
- ClassTimetableView, TeacherTimetableView

### Unit Tests

- Tests/AttendanceTimetableTests.cs
  - AttendancePercentageTests: daily mode count late/excused as present, all absent 0%, all present 100%, countSick flag, zero total 0, chronic flagged when < threshold, theory cases
  - TimetableClashDetectionTests: TeacherClash, ClassDoubleBooked, RoomConflict, Clash message plain language, EffectiveDated does not rewrite history, Duplicate register guard, Backdating flagged

All tenant-owned tables tenant_id leading indexes per frozen multi-tenancy, soft-delete, audit interceptor.

