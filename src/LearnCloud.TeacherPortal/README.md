# Teacher Portal - LearnCloud V1
**For teachers daily use, own phones, between lessons, mid-range phone, slow 3G, one-handed**

## Features Delivered

### Dashboard: today's timetable, registers still to be marked, marks deadlines, unread notices

- `TeacherDashboardService.GetDashboardAsync(tenantId, teacherStaffId, userId)`
  - Today's timetable: query `timetable_slots` where `teacher_staff_id = me` AND `day_of_week = today (1=Mon..7=Sun)` AND effective dated `effective_from <= today <= effective_to`, order by period. Maps to `TodayTimetableItemDto` with grade/stream/subject/room/period times.
  - Registers to mark: for each assigned class (via `GetAssignedClassesAsync`), check if `attendance_registers` exists for today (daily mode) or per period if per_period mode. If not exists, todo list with studentsCount, isOverdue if after 4pm. Flag.
  - Marks deadlines: assessments where teacher teaches subject in class (join timetable_slots distinct), filter dueDate <=7 days, count totalStudents vs markedCount from `student_marks`, daysLeft, status not_started/draft/submitted.
  - Unread notices: `teacher_notices` where target null or targetTeacherStaffId = me and isRead false, top 5.
  - Total classes, total learners.

### My Classes: only classes teacher is assigned to, enforced server-side

- `GetMyClassesAsync` uses `TeacherAuthorizationService.GetAssignedClassesAsync`:
  - Class teacher: `streams.class_teacher_staff_id = teacherStaffId`
  - Timetable: `timetable_slots teacher_staff_id = teacherStaffId`
  - Union distinct gradeId/streamId
  - For each, learnersCount, subjects taught in class (distinct subject names from timetable), isClassTeacher bool, fullName Grade+Stream
- **Authorization critical:** Every endpoint must verify teacher is assigned to class in question, server-side, in addition to tenant filter. Implemented in `TeacherAuthorizationService`:
  - `GetTeacherStaffIdAsync(tenantId, userId)` finds staff_profile where user_id = userId and tenant_id, else throws Unauthorized.
  - `IsAssignedToClassAsync(tenantId, teacherStaffId, gradeId, streamId)` checks class teacher OR timetable slot.
  - `EnsureAssignedToClassAsync` throws Unauthorized if not assigned, logs security.
  - `IsTeachingSubjectInClassAsync` also checks subject assignment via timetable or staff_subjects M2M.
  - All controllers call Ensure before loading data.

### Attendance Capture Reusing Register Screen

- Frontend `TeacherPortal.jsx` AttendanceTab reuses `AttendanceRegisterCapture.jsx` from AttendanceTimetable module.
- Backend: `TeacherPortalController.GetAttendanceRegister` calls EnsureAssignedToClass then delegates to AttendanceService (or returns allowed)
- Optimized for speed: whole class listed, one tap cycle P/A/L/E/S, mark all present, autosave 5s, unsaved indicator, one-handed thumb zone bottom fixed buttons.

### Marks Entry: Keyboard Navigable Grid, Autosave, Validation, Draft + Submit Locks Pending Approval

- `MarksEntryService.GetMarksGridAsync`:
  - Loads assessment, enforces EnsureTeachingSubjectInClass, loads students ordered lastName, existing marks, returns MarksGridDto rows with score, status draft/saved, maxScore.
- `SaveMarksAsync`:
  - Validation against assessment maximum: if score > maxScore throw InvalidOperationException plain language.
  - Upsert student_marks, tenant_id leading index, audit via interceptor.
  - Autosave: frontend calls every 3s if unsaved, `saveAsDraft=true`.
- `SubmitMarksAsync`:
  - Checks all students marked? Allows but logs warning.
  - Writes AuditLog marks_submitted with teacherStaffId, marksCount.
  - Returns grid with OverallStatus submitted, SubmittedAt, locks pending approval (frontend disables edits, shows "Locked pending approval - Head must approve").
  - Unlock requires Head Teacher `marks.approve` + `marks.unlock` permission.

- **Frontend MarksTab**: Excel-like table, input type number min 0 max, validation, keyboard: Tab/Enter down, Arrow navigation, type number overwrites, Space toggle absent? Auto-save 3s debounce, unsaved indicator warning, Save as Draft + Submit Locks button, focus ring.

### Homework and Assignments: Create, Attach File, Due Date, Target Class, Submission Status

- Entities: `HomeworkAssignment` tenant_id, teacher_staff_id, subject, grade, stream, academic_year, term, title, description, file_url/file_name/file_size, due_date, status; `HomeworkSubmission` assignment_id, student_id, status pending/submitted/late, submitted_at, file_url, note, teacher_feedback, score.
- Service `HomeworkService.CreateAsync` ensures assigned to class + teaching subject, creates assignment, auto-creates pending submissions for each student in class (for tracking).
- List, Get, GetSubmissions (per assignment, enforces teacher owns assignment), UpdateFeedback.
- Frontend HomeworkTab: form grade/stream/subject/due date/title/desc, create, list with submitted/pending counts.

### Lesson Plans: Create Against Subject and Class, Simple Template

- Entity `LessonPlan` teacher_staff_id, subject, grade, stream, year/term, date, objective, activities (Intro/Main/Conclusion), resources, assessment, reflection, status draft/planned/taught/archived.
- Service `LessonPlanService` ensures teaching subject in class, CRUD.
- Frontend LessonPlansTab: form with template fields, list.

### Read-Only View of Learners in Class with Guardian Contact Details, Subject to Permission

- `TeacherDashboardService.GetLearnersInClassAsync` enforces EnsureAssignedToClass, loads students ordered lastName, for each loads guardian_student_links + guardians, returns LearnerInClassDto with studentNumber, photo, dob, gender, status, attendance % (from attendance_records), guardians list with name, relationship, phone, email, is_primary, is_billing (fee payer flagged ★), is_emergency, can_pickup, sms/email opt-in.
- Permission: guardians.read - for teacher only own class parents allowed (via assignment check). If includeGuardians=false, skip.
- Frontend LearnersTab: inputs grade/stream, load button (server verifies), list learners cards with avatar initials, guardians contacts.

### Profile and Password Management

- `TeacherPortalController.GetProfile` returns staffId, userId, classes, message to use /api/auth/me and /api/auth/change-password
- UpdateProfile (phone, qualification) placeholder
- Change password via existing Auth module: POST /api/auth/change-password revokes all refresh tokens.

### Design for Mid-Range Phone on Slow Connection

- Bottom nav fixed, thumb zone, 44px min touch, 48px primary actions
- Page weight <150KB, Tailwind only, no heavy animation, system fonts
- Offline: IndexedDB for marks draft, attendance draft (reuses existing)
- Semantic HTML, 16px inputs prevents iOS zoom
- Server-side enforcement, not just UI hide - tests prove

### Tests That Attempt to Access Another Teacher's Class and Must Fail

- `Tests/TeacherPortalAuthorizationTests.cs`:
  - `Teacher_Cannot_Access_Another_Teachers_Class_Must_Fail`: Seeds two teachers Alice (Blue) and Bob (Green), same grade, different streams, timetable slots. Teacher A can access Blue but not Green -> EnsureAssignedToClass throws Unauthorized, IsAssigned returns false, teaching subject check fails.
  - `Teacher_GetAssignedClasses_Only_Own`: Teacher A has Blue and Red, not Green, count 2.
  - `Tenant_Isolation_Teacher_Cannot_Access_Other_Tenant_Class`: Two tenants with same class names, tenant isolation via global filter + assignment check, teacher from tenant1 cannot access class from tenant2 even if IDs guessed.

### Backend Endpoints

- `GET /api/teacher/dashboard` - today's timetable, registers to mark, marks deadlines, notices
- `GET /api/teacher/classes` - my classes only assigned
- `GET /api/teacher/attendance/register?gradeId&streamId&date&period&year&term` - verifies assignment
- `GET /api/teacher/marks/{assessmentId}` - grid
- `POST /api/teacher/marks/{assessmentId}` - save draft autosave
- `POST /api/teacher/marks/{assessmentId}/submit` - locks pending approval
- `POST /api/teacher/homework` - create
- `GET /api/teacher/homework` - list
- `GET /api/teacher/homework/{id}` + `/submissions` + `PUT /submissions/{id}/feedback`
- `POST /api/teacher/lesson-plans`, `GET /api/teacher/lesson-plans`, `GET /api/teacher/lesson-plans/{id}`
- `GET /api/teacher/classes/{gradeId}/{streamId}/learners?includeGuardians=true` - read-only learners with guardian contacts
- `GET /api/teacher/profile`, `PUT /api/teacher/profile`

All with tenant_id leading indexes, soft-delete, audit.

### Frontend

- `Frontend/TeacherPortal.jsx` - single file bottom nav phone design, DashboardTab, MyClassesTab, AttendanceTab (reuses AttendanceRegisterCapture), MarksTab keyboard navigable, HomeworkTab, LessonPlansTab, LearnersTab, ProfileTab

### Security

- Every endpoint calls `GetTeacherStaffIdAsync` + `EnsureAssignedToClassAsync` server-side, tenant filter via DbContext global filter, not just JWT perm `teacher` role.
- Tests prove cross-teacher and cross-tenant access fails with Unauthorized.

