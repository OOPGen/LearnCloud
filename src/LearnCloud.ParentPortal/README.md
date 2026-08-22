# Parent Portal — For Least Technical Users, Mostly Phone, Simplicity Outranks Completeness

**Design constraints: small screen, readable at arm's length, minimal JavaScript, understandable without instructions, 44px touch, 16px inputs, bottom nav thumb zone**

## Features Delivered

### Login, Invitation Flow, Account Links to Multiple Children Different Classes

- **Entities:** `GuardianInvitation` tenant_id, guardian_id, email/phone, token_hash SHA256 single-use hashed, expires_at 7 days, used_at, created_by_user_id (registrar/school admin who triggers), invitation_link (raw token in link, never logged hashed only)
- **Flow:** School triggers invitation via `POST /api/parent/invitations` {guardianId, email, phone, message} → generates 32 bytes base64url raw token, hash SHA256 stored, expires 7 days, creates GuardianInvitation, sends email/SMS with link `https://learncloud.co.zw/parent/accept-invitation?token=RAW&email=...` (raw token not logged). Guardian clicks link, POST `/api/parent/accept-invitation` {token, email, newPassword, confirmPassword} → validates token hash exists not used not expired, finds guardian, creates User account linked via guardian.user_id, password hash via Identity hasher, assigns PARENT role, marks email verified, invitation usedAt.
- **Account links to children:** GuardianStudentLink junction tenant_id, guardian_id, student_id, relationship, is_primary, is_billing (fee payer), is_emergency, can_pickup, academic_year_id. One guardian can have multiple children in different classes (e.g., Grade 5 Blue and Form 1 A). Child switcher shows all linked children.

### Child Switcher, Home Screen Per Child

- **Child switcher:** Frontend header select with big font 16px, rounded-full border-2 primary-200 bg-primary-50, min-w 140px, readable at arm's length. Also horizontal chips below header for quick switch when >1 child. Stores selectedChildId in localStorage for resume.
- **Home screen per child `GET /api/parent/children/{studentId}/home`:**
  - Verifies guardian-child link server-side
  - Outstanding balance this term: sum fee_invoices balance_due where studentId, currency
  - Attendance % this term: present+late+excused / total days, isChronic <85% flag
  - Latest published results: report_cards status published order by publishedAt desc, termName, average, overallGrade, pdfUrl
  - Upcoming assessments next 14 days: assessments grade/stream where assessmentDate >= today <= +14 days
  - Recent notices: messaging batches where tenant and is broadcast or targeted to child's grade/stream, top 5
  - Homework due: homework assignments grade/stream where dueDate >= today -7 and <= +7, top 5
  - Returns ChildHomeDto with all above, child switcher uses it.

### Fees: Statement, Invoice History, Receipts, Downloadable PDF, Payment Action Once Gateway Exists

- **Statement:** GET `/api/parent/children/{studentId}/fees/statement?from&to` → loads invoices + payments ordered date, running balance via FeeCalculationService.Round2, returns lines date/type/number/description/debit/credit/balance, totalInvoiced, totalPaid, balanceDue, credit. PDF via `/statement/pdf` returns HTML for print (QuestPDF in real).
- **Invoice history:** GET `/fees/invoices` → invoiceNumber, issueDate, dueDate, total, paid, balance, status, currency. Downloadable PDF via `/api/fees/invoices/{id}/print` existing.
- **Receipts:** GET `/fees/receipts` → receiptNumber, paymentDate, amount, currency, method, reference. PDF via `/api/fees/payments/{id}/receipt/print`.
- **Payment action placeholder:** Home shows outstanding balance big 3xl font, Fees tab shows "PayNow button will appear here once gateway exists, currently manual at school" — gateway later PayNow integration.

### Attendance Detail with Dates and Reasons

- GET `/children/{studentId}/attendance?academicYearId&termId` → list AttendanceDetailDto date, status Present/Absent/Late/Sick/Excused, reason, note, periodNumber, periodName, order desc, take 100
- GET `/attendance/summary?academicYearId&termId` → total, present, absent, late, excused, sick, percentage, isChronic. Frontend shows grid 3 cols total/present/% etc., detail list with color coded present success, absent danger.

### Results: Published Report Cards Only, Downloadable

- GET `/children/{studentId}/results` → only status published, ordered publishedAt desc, only published visible to parents per requirement (draft/approved not visible). Permission scoped guardian-child link.
- GET `/report-cards/{reportCardId}` → detail with subjects, attendance summary, checks status published else 401, checks guardian-child link
- GET `/report-cards/{id}/pdf` → HTML for PDF download, in real QuestPDF

### Notices and Homework

- Notices: GET `/children/{studentId}/notices` → messages where recipient is guardian or broadcast to grade/stream, top 20
- Homework: GET `/children/{studentId}/homework` → homework assignments grade/stream where dueDate >= today-7, subject name, daysLeft

### Message to Class Teacher, School Enables It, Moderation and Rate Limiting

- **School setting:** ParentMessagingSettings tenant_id, enable_parent_teacher_messaging bool default true, require_moderation bool default false, rate_limit_per_hour 5, per_day 20, allowedRoles JSON.
- **Entities:** ParentTeacherMessage guardian_id, studentId (context which child), gradeId, streamId, sender_user_id guardian user, recipient teacher staff id/user id, subject, body, status pending/approved/rejected/sent, requires_moderation bool, moderated_by, moderated_at, moderation_note, rate_limit_key guardianId:gradeId:streamId
- **Rate limiting:** Before send, count messages where tenant, guardian, grade, stream, created_at >= now-1h, if >= rateLimitPerHour (5) throw "Rate limit: max 5 per hour per class". Same for per day 20.
- **Moderation:** If require_moderation true, status pending, needs head/teacher to approve via moderation endpoint (not in V1 scope, but status pending). If false, status sent.
- **Endpoints:** POST `/children/{studentId}/messages` {studentId, gradeId, streamId, subject, body, recipientTeacherStaffId} → validates guardian-child link, checks school enables messaging, checks rate limit, creates message. GET `/children/{studentId}/messages` → thread for that child, guardian only own.
- **Frontend MessagesScreen:** Subject, body textarea, Send to Teacher button min-h-touch, list thread with status pending moderation badge.

### Profile and Contact Preferences Including SMS Opt-Out

- GET `/profile` → guardianId, userId, first/last, email, phone, address, children list, contactPreferences smsOptIn, emailOptIn, smsOptOut hard, emailOptOut, preferredLanguage, canReceiveFeesSms etc.
- PUT `/profile/contact-preferences` → update sms_opt_in, email_opt_in, sms_opt_out hard stops all SMS, email_opt_out, preferredLanguage, canReceiveFeesSms etc. Opt-out always honoured: filtered before send in messaging module, delivery log is_opted_out true, counted as filteredOptOut, not sent. Cost estimate excludes opted out.

### Authorization: Every Endpoint Verifies Guardian-Child Link Server-Side, In Addition to Tenant Filter

- `ParentAuthorizationService`: GetGuardianIdAsync(tenantId, userId) finds guardian where user_id=userId and tenant_id, else Unauthorized. IsGuardianOfStudentAsync checks guardian_student_links tenant+guardian+student not deleted. EnsureGuardianOfStudentAsync throws Unauthorized if not linked. GetStudentIdsForGuardian returns only own children. All parent portal services call Ensure before loading data.
- Tenant filter via LearnCloudDbContext global reflection filter `tenant_id == CurrentTenantId && !IsDeleted` plus guard throws if saved with different TenantId.
- Tests attempt to read another family's child and must fail — `Tests/ParentPortalAuthorizationTests.cs`:
  - Parent_Cannot_Read_Another_Familys_Child_Must_Fail: Seed guardianA with studentA, guardianB with studentB, guardianA is guardian of A true, not of B false, Ensure throws Unauthorized, children list only own.
  - Parent_With_Multiple_Children_Can_Read_Both_But_Not_Other: Guardian with 2 children Blue and Green, can read both, not other child 3.
  - Tenant_Isolation_Parent_Cannot_Access_Other_Tenant_Child: Two tenants same logical class Blue, guardian from tenant1 cannot access student from tenant2 even if IDs guessed, tenant filter + link check.

### Design Constraints: Small Screen, Readable at Arm's Length, Minimal JS, Understandable Without Instructions

- Phone-first: max-w-xl centered, bottom nav fixed 6 items Home/Fees/Attend/Results/HW/Me, 44px min touch, 10px labels, thumb zone bottom, sticky header child switcher big 16px font rounded-full border-2 primary-200 bg-primary-50 140px min-w readable at arm's length.
- Minimal JS: React only, no heavy libraries, no animation library, Tailwind CDN, CSS mock, semantic HTML, focus rings, keyboard nav Tab, labels for inputs.
- Understandable without instructions: Big cards with icons, outcome-focused labels Outstanding Balance 3xl font, Attendance This Term 2xl, Latest Result average 72% etc., no jargon, child switcher shows fullName — Grade Stream, home per child shows balance, attendance %, latest result, upcoming, notices, homework due — all in one screen.
- Invitation flow simple: school triggers, guardian receives link, sets password, account links to children, child switcher appears. No manual linking needed.

### Backend Endpoints

- Invitations: POST /api/parent/invitations (school admin registrar head), POST /api/parent/accept-invitation anonymous
- Children: GET /api/parent/children, GET /children/{studentId}/home (balance, attendance %, latest result, upcoming assessments, notices, homework)
- Fees: GET /children/{id}/fees/statement, /fees/invoices, /fees/receipts, /fees/statement/pdf
- Attendance: GET /children/{id}/attendance, /attendance/summary
- Results: GET /children/{id}/results (published only), GET /report-cards/{id}, GET /report-cards/{id}/pdf
- Notices: GET /children/{id}/notices
- Homework: GET /children/{id}/homework
- Messages: POST /children/{id}/messages (rate limit 5/hour, moderation), GET /children/{id}/messages thread
- Profile: GET /profile, PUT /profile/contact-preferences (SMS opt-out always honoured)

All with tenant_id leading indexes, soft-delete, audit.

### Frontend

- `Frontend/ParentPortal.jsx` — single file, child switcher header big select + chips, bottom nav 6 items, HomeScreen, FeesScreen (statement/invoices/receipts tabs with PDF download), AttendanceScreen, ResultsScreen (published only downloadable), NoticesScreen, HomeworkScreen, MessagesScreen (subject/body send to teacher with moderation badge), ProfileScreen (contact preferences SMS opt-out hard).

