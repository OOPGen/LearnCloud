# Student Portal - Secure Login, Own Record Scoping, Simple & Fast for Low-End Shared Devices

**Design constraints: works on small screen, readable at arm's length, minimal JavaScript, understandable without instructions, shared or low-end devices, 44px touch, 16px inputs**

## Features Delivered

### Secure Login + Own Record Scoping Verified Server-Side

- Student has `user_id` nullable FK to users table (migration adds column). Login via `/api/auth/login` with role STUDENT, tenant_id, user_id linked to student.
- `StudentAuthorizationService`: `GetStudentIdAsync(tenantId, userId)` finds student where user_id=userId and tenant_id, else Unauthorized. `IsOwnRecordAsync` checks studentId == authenticatedStudentId, `EnsureOwnRecordAsync` throws Unauthorized if not own, plus tenant filter check student exists in tenant (prevents ID guessing). Every endpoint calls Ensure.
- School-level setting `StudentPortalSettings` controlling whether students may see fee info at all: `AllowStudentsViewFees` bool default false (school must opt-in), also AllowGuardianContacts, AllowSubmitAssignments, AllowViewAttendance/Results/Timetable/MessageTeacher.

### Dashboard, Timetable, Attendance, Results, Assignments, Notices, Fees (if permitted), Profile

**Endpoints (all tenant_id leading index, soft-delete, audit, scoped to own record):**

- `GET /api/student/dashboard` - timetable week, attendance summary, published results latest, assignments due next 14 days, recent notices, fee summary if allowed
- `GET /api/student/timetable` - week Mon-Fri, effective dated timetable for student's grade/stream,today, slots with period name times subject teacher room isBreak
- `GET /api/student/attendance?academicYearId&termId` - detail dates status reason note period, summary total/present/absent/late/excused percentage isChronic
- `GET /api/student/results` - published report cards only, only published visible enforced server-side (draft/approved not visible), ordered publishedAt desc
- `GET /api/student/results/{id}` - detail subjects, checks status published else 401
- `GET /api/student/results/{id}/pdf` - HTML for PDF download
- `GET /api/student/assignments` - homework for student's grade/stream due >= today-7, subject name, daysLeft, status pending/submitted/late, canSubmit from settings
- `POST /api/student/assignments/{id}/submit` {note, fileUrl, fileName} - checks AllowStudentsSubmitAssignments setting, checks assignment grade/stream matches student's class, creates StudentAssignmentSubmission status submitted/late
- `GET /api/student/notices` - message batches broadcast for student's class, top 20
- `GET /api/student/fees/summary` - fee summary if school permits, else 403 FEES_NOT_ENABLED_FOR_STUDENTS with message "Fee information not enabled for students by school. Contact bursar." School-level setting controls.
- `GET /api/student/fees/statement?from&to` - statement lines if allowed
- `GET /api/student/profile` - studentNumber, first/last, fullName, email, phone, grade/stream, photo, dob, status
- `PUT /api/student/profile` - update phone/email own record only
- `POST /api/student/change-password` - delegates to /api/auth/change-password revokes refresh tokens

### Fee Statement If School Permits

- `StudentPortalSettings.AllowStudentsViewFees` bool default false, school must opt-in via settings UI. If false, `GetFeeSummaryAsync` returns null, controller returns 403 with code FEES_NOT_ENABLED_FOR_STUDENTS and message "Fee information not enabled for students by school. Contact bursar." This prevents students seeing fees unless school explicitly allows.

### Simple and Fast, Low-End Shared Devices

- Frontend `StudentPortal.jsx` - max-w-xl centered, bottom nav 7 items Home/Table/Attend/Results/HW/Fees/Me min touch 44px 10px labels thumb zone, top header primary-800 white 3px padding, readable at arm's length 3xl balance 2xl attendance.
- Dashboard: student name, grade/stream/number, timetable today filtered flatMap, attendance percentage, latest result average, fee summary if allowed balanceDue, homework due, recent notices - all in one screen minimal scroll.
- TimetableTab: week Mon-Fri, day columns, slots with periodName start-end subject teacher room, isBreak badge.
- AttendanceTab: summary grid total/present/%, detail list color coded success/danger/warning with date status reason note.
- ResultsTab: published only downloadable PDF, only published visible message, position display.
- AssignmentsTab: title subject, due date daysLeft status, canSubmit flag from school setting, file input + submit button min touch.
- NoticesTab: title body date priority.
- FeesTab: if disabled shows locked icon 🔒 Fee Information Disabled message school-level setting, else balance 3xl, statement downloadable PDF, payment action placeholder PayNow button will appear.
- ProfileTab: fullName studentNumber grade/stream, phone email, change password form current/new/confirm, revokes all refresh tokens.

- Minimal JS: React only, no heavy libraries, Tailwind CDN, system fonts, semantic headings, focus rings, 16px inputs prevents iOS zoom, bottom nav thumb zone, offline cache via service worker? No, just small weight <150KB.

### Tests That Attempt to Read Another Student's Record and Must Fail

- `Tests/StudentPortalAuthorizationTests.cs`:
  - `Student_Cannot_Access_Another_Students_Record_Must_Fail`: studentA user 100 studentNumber 2026-001, studentB user 200 2026-002, studentA GetStudentId returns A, canAccessOwn true, canAccessOther false, Ensure throws Unauthorized, GetStudentIds returns only own.
  - `Student_Tenant_Isolation_Cannot_Access_Other_Tenant_Record_By_Guessing_ID`: two tenants same logical class Blue same studentNumber 2026-001, tenant 1 context global filter hides tenant 2, own true, other tenant false, Ensure throws.
  - `School_Setting_AllowStudentsViewFees_Controls_Access`: default AllowStudentsViewFees false returns null 403, after enabling true returns summary not null.

### Backend Services

- `StudentAuthorizationService` own record scoping
- `StudentPortalService` dashboard, timetable week effective dated, attendance, published results only, assignments with submission where enabled, notices, fee summary gated by school setting, profile
- `StudentPortalSettings` entity tenant_id unique, bools

All tenant-owned indexes lead with tenant_id, soft-delete, audit interceptor old/new values.

