# LearnCloud Roles & Permissions Matrix V1
**HQ:** Bulawayo, ZW | **Version:** 1.1 | **DB:** MySQL 8.0 tenant_id leading indexes | **Style:** module.action

This is the authorization contract that sits on top of the Database Schema V1 and SRS V1. No application code, only spec + constants + seed.

---

## 1. NAMING CONVENTION & GOVERNANCE

- Format: `module.action` or `module.submodule.action` all lowercase dot separated. Example `students.read`, `fees.invoices.create`, `marks.approve`.
- Permission check is always **two layers**:
  1. **Tenant Filter**: `WHERE tenant_id = @CurrentTenantId AND is_deleted=0` (mandatory, enforced at ORM/EF Core Global Query Filter)
  2. **Row-Level Scope**: additional predicate based on role (teacher-owns-class, parent-owns-child). If scope fails, return 403 or empty set — never leak other tenant or other class data.
- All permissions are denied by default. Roles grant allow. No negative deny; absence = deny except where explicitly scoped.

Legend for matrix:
- ✅ ALLOW-ALL = Role can do action on any record within tenant
- 🔶 ALLOW-SCOPED = Role can do action only on scoped rows (explained in Scope column)
  - Scopes: `OWN_CLASS` = only students/enrolments in streams where teacher is assigned (timetable_slots.teacher_staff_id = my staff_id OR streams.class_teacher_staff_id = my staff_id)
  - `OWN_CHILD` = only student_ids linked via `guardian_student_links WHERE guardian_id = myGuardianId`
  - `OWN` = only own user record, own marks, own timetable, own attendance
  - `OWN_SUBJECT` = teacher + subject: marks.enter only for assessments where assessment.subject_id IN my qualified subjects and timetable assignment matches
  - `BREAK_GLASS` = Platform Superadmin must request break-glass with audit, not regular allow
- ❌ DENY = Not granted in V1 (may become SHOULD in V2)

---

## 2. COMPLETE PERMISSION CATALOG - V1 (115 Permissions)

| # | Permission Code | Module | Description (What it allows, testable) |
|---|---|---|---|
| **Platform & Tenancy** |
| 1 | `tenants.read` | Platform | List/view tenant records |
| 2 | `tenants.create` | Platform | Provision new tenant |
| 3 | `tenants.update` | Platform | Edit tenant profile/logo/city |
| 4 | `tenants.suspend` | Platform | Suspend/reactivate tenant |
| 5 | `platform.plans.read` | Platform | View subscription plans catalog |
| 6 | `platform.plans.manage` | Platform | Create/edit plans and pricing |
| 7 | `platform.subscriptions.read` | Platform | View tenant subscriptions metering |
| 8 | `platform.subscriptions.manage` | Platform | Change plan, grace, suspend for non-payment |
| 9 | `platform.audit.read` | Platform | Read cross-tenant platform audit logs |
| **Users, Roles, Settings, Audit** |
| 10 | `users.read` | Admin | View users within tenant |
| 11 | `users.create` | Admin | Create user accounts |
| 12 | `users.update` | Admin | Edit user display name, phone |
| 13 | `users.disable` | Admin | Disable/enable login |
| 14 | `users.invite` | Admin | Send invite email/SMS |
| 15 | `users.import` | Admin | Bulk import users CSV |
| 16 | `roles.read` | Admin | List roles |
| 17 | `roles.manage` | Admin | Create/update custom roles & assign perms |
| 18 | `permissions.read` | Admin | View permission catalog |
| 19 | `settings.read` | Admin | View school profile, colors #0F153A |
| 20 | `settings.manage` | Admin | Update school profile, logo, colors, timezone Africa/Harare |
| 21 | `settings.academicYear.manage` | Admin | Create academic years, flag current |
| 22 | `audit.read` | Admin | View tenant audit_logs table |
| 23 | `audit.export` | Admin | Export audit logs CSV |
| **Academic Structure** |
| 24 | `academicYears.read` | Academic | View academic years list |
| 25 | `academicYears.manage` | Academic | Create/edit/delete years |
| 26 | `terms.read` | Academic | View terms |
| 27 | `terms.manage` | Academic | Create/edit terms dates |
| 28 | `grades.read` | Academic | View grades list |
| 29 | `grades.manage` | Academic | Create/edit grades |
| 30 | `streams.read` | Academic | View streams/classes |
| 31 | `streams.manage` | Academic | Create/edit streams, assign class teacher, capacity |
| 32 | `subjects.read` | Academic | View subjects |
| 33 | `subjects.manage` | Academic | Create/edit subjects |
| 34 | `gradeSubjects.read` | Academic | View grade-subject offering |
| 35 | `gradeSubjects.manage` | Academic | Assign subjects to grades per year |
| 36 | `rooms.read` | Academic | View rooms |
| 37 | `rooms.manage` | Academic | Create/edit rooms |
| **Staff** |
| 38 | `staff.read` | Staff | View staff profiles |
| 39 | `staff.write` | Staff | Create/update staff profile |
| 40 | `staff.delete` | Staff | Soft-delete staff |
| 41 | `staff.import` | Staff | Bulk import staff CSV |
| 42 | `staff.export` | Staff | Export staff list |
| **Students & Guardians & Enrolments** |
| 43 | `students.read` | Students | View student records |
| 44 | `students.write` | Students | Create/update student personal data |
| 45 | `students.delete` | Students | Soft-delete student (admin only) |
| 46 | `students.archive` | Students | Archive to alumni/inactive |
| 47 | `students.import` | Students | Bulk import students CSV |
| 48 | `students.export` | Students | Export students list/registers |
| 49 | `students.photo.upload` | Students | Upload student photo |
| 50 | `enrolments.read` | Enrolment | View enrolment history |
| 51 | `enrolments.create` | Enrolment | Create new enrolment (new admission) |
| 52 | `enrolments.transfer` | Enrolment | Transfer stream/mid-term (creates new enrolment row) |
| 53 | `enrolments.withdraw` | Enrolment | Mark withdrawn, set exit_date |
| 54 | `enrolments.promote` | Enrolment | Set promotion decision promoted/repeat |
| 55 | `enrolments.readHistory` | Enrolment | View full enrolment ledger (repeat/transfer history) |
| 56 | `guardians.read` | Guardians | View guardian contacts |
| 57 | `guardians.write` | Guardians | Create/update guardian person |
| 58 | `guardians.link` | Guardians | Link/unlink guardian to student, set relationship_type |
| 59 | `guardians.billing.assign` | Guardians | Set is_billing_contact flag |
| 60 | `guardians.delete` | Guardians | Soft-delete guardian link |
| **Admissions** |
| 61 | `admissions.read` | Admissions | View applications pipeline |
| 62 | `admissions.create` | Admissions | Create inquiry (internal, public form is anonymous) |
| 63 | `admissions.updateStatus` | Admissions | Move status inquiry->applied->offered etc |
| 64 | `admissions.convert` | Admissions | Convert accepted -> active student + enrolment |
| 65 | `admissions.delete` | Admissions | Soft-delete application |
| 66 | `admissions.export` | Admissions | Export applications |
| **Attendance** |
| 67 | `attendance.read` | Attendance | View attendance records |
| 68 | `attendance.mark` | Attendance | Mark daily attendance Present/Absent/Late |
| 69 | `attendance.edit` | Attendance | Override after 48h with reason (School Admin) |
| 70 | `attendance.delete` | Attendance | Soft-delete incorrect mark (admin) |
| 71 | `attendance.export` | Attendance | Export register PDF/CSV |
| **Timetable** |
| 72 | `timetable.read` | Timetable | View any timetable |
| 73 | `timetable.manage` | Timetable | Create/edit/delete slots, conflict detection |
| 74 | `timetable.clone` | Timetable | Clone prior term timetable |
| 75 | `timetable.export` | Timetable | Export timetable PDF |
| **Fees** |
| 76 | `fees.structures.read` | Fees | View fee structures |
| 77 | `fees.structures.manage` | Fees | Create/edit structures & items, amount DECIMAL(18,2)+currency |
| 78 | `fees.invoices.read` | Fees | View invoices |
| 79 | `fees.invoices.create` | Fees | Generate batch/single invoices |
| 80 | `fees.invoices.void` | Fees | Void/credit note (no delete if paid) |
| 81 | `fees.invoices.export` | Fees | Export invoices/statement PDF |
| 82 | `fees.payments.read` | Fees | View payments |
| 83 | `fees.payments.create` | Fees | Record cash/bank/EcoCash payment + proof_url |
| 84 | `fees.payments.reverse` | Fees | Reverse incorrect payment with audit |
| 85 | `fees.payments.export` | Fees | Export payments |
| 86 | `fees.receipts.read` | Fees | View/print receipt PDF |
| 87 | `fees.reports.read` | Fees | View age analysis, collection report (arrears) |
| 88 | `fees.discount.approve` | Fees | Approve discount >15% (bursar needs admin second) |
| **Assessments, Grading, Marks, Reports** |
| 89 | `assessmentCategories.read` | Assessments | View weighting categories |
| 90 | `assessmentCategories.manage` | Assessments | Create/edit categories weight% must sum 100 |
| 91 | `grading.read` | Assessments | View grading scales |
| 92 | `grading.manage` | Assessments | Manage scales & ranges min/max score |
| 93 | `assessments.read` | Assessments | View assessment list |
| 94 | `assessments.manage` | Assessments | Create/edit assessments, max_score, date |
| 95 | `marks.read` | Marks | View marks (scope controls all vs own class vs own child) |
| 96 | `marks.enter` | Marks | Enter score for students in own subject/class |
| 97 | `marks.editOwn` | Marks | Edit own entered marks before submit |
| 98 | `marks.submit` | Marks | Submit marks for approval (locks) |
| 99 | `marks.approve` | Marks | Approve/reject marks (Head/Deputy) |
| 100 | `marks.unlock` | Marks | Unlock after approval (Head only) |
| 101 | `marks.export` | Marks | Export gradebook Excel |
| 102 | `reportCards.read` | Reports | View report cards |
| 103 | `reportCards.generate` | Reports | Generate draft report PDF (aggregate average, rank) |
| 104 | `reportCards.publish` | Reports | Publish to parent/student portal + notify |
| 105 | `reportCards.export` | Reports | Export report card PDF/CSV bulk |
| **Messaging** |
| 106 | `messages.read` | Messages | View own inbox |
| 107 | `messages.send` | Messages | Send direct message to any user |
| 108 | `messages.sendOwnClass` | Messages | Teacher sends to parents of own class only |
| 109 | `messages.sendBroadcast` | Messages | Send school-wide announcement filtered by role/grade/stream |
| 110 | `messages.delete` | Messages | Soft-delete message |
| 111 | `messages.readAll` | Messages | Read all messages audit (School Admin) |
| **Dashboards** |
| 112 | `dashboard.viewAdmin` | Dashboard | School Admin dashboard (admissions, arrears, attendance) |
| 113 | `dashboard.viewHead` | Dashboard | Head dashboard (real-time attendance, collection, academic) |
| 114 | `dashboard.viewBursar` | Dashboard | Bursar dashboard (total due, overdue >60d) |
| 115 | `dashboard.viewRegistrar` | Dashboard | Registrar dashboard (inquiry pipeline) |
| 116 | `dashboard.viewTeacher` | Dashboard | Teacher dashboard (my classes, marks due) |
| 117 | `dashboard.viewParent` | Dashboard | Parent dashboard (children cards fee+attendance+latest report) |
| 118 | `dashboard.viewStudent` | Dashboard | Student dashboard (my timetable, results) |
| 119 | `dashboard.viewPlatform` | Dashboard | Platform admin dashboard (tenants, MRR, health) |

Total 119 permissions (expanded from 115 after adding readAll variants needed for scope clarity). V1 MUST = all.

---

## 3. ROLES DEFINITION - V1

| Role Code | Name | Scope | Description |
|---|---|---|---|
| `PLATFORM_SUPERADMIN` | Platform Superadmin | Platform (tenant_id NULL) | LearnCloud staff, provisions tenants, manages plans, break-glass only to tenant PII |
| `SCHOOL_ADMIN` | School Admin | Tenant-All | IT/Admin, god within tenant, not academic approver but can override |
| `HEAD_TEACHER` | Head Teacher | Tenant-All (academic) | Principal, approves marks, publishes reports, board-ready reports |
| `DEPUTY_HEAD` | Deputy Head | Tenant-All (academic, no final publish) | Similar to Head but cannot publish final report or manage settings |
| `BURSAR` | Bursar | Tenant-Financial | Finance officer, fee structures, invoices, payments, receipts, reports |
| `REGISTRAR` | Registrar | Tenant-Admissions & Students | Admissions pipeline, student master, guardians, enrolment transfers |
| `TEACHER` | Teacher | Tenant-Scoped OwnClass | Class/subject teacher, marks own classes, attendance own classes |
| `PARENT` | Parent | Tenant-Scoped OwnChild | Guardian portal, sees own children's data only |
| `STUDENT` | Student | Tenant-Scoped Own | Upper primary/secondary, sees own timetable, own results, own fees |

---

## 4. MATRIX - ROLES vs PERMISSIONS

### 4.1 Platform & Users

| Permission | Platform Superadmin | School Admin | Head Teacher | Deputy Head | Bursar | Registrar | Teacher | Parent | Student |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| tenants.read | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| tenants.create | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| tenants.update | ✅ | 🔶 OWN_SCHOOL_SETTINGS | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| tenants.suspend | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| platform.plans.read | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| platform.plans.manage | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| platform.subscriptions.read | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| platform.subscriptions.manage | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| platform.audit.read | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| users.read | ✅ BREAK_GLASS | ✅ | ✅ | ✅ | 🔶 BURSAR_TEAM_ONLY | 🔶 REG_TEAM | 🔶 OWN_CLASS_PARENTS | ❌ | ❌ |
| users.create | ❌ | ✅ | ❌ | ❌ | ❌ | 🔶 STUDENT_GUARDIAN_ONLY | ❌ | ❌ | ❌ |
| users.update | ✅ BREAK_GLASS | ✅ | ❌ | ❌ | ❌ | 🔶 OWN_SCOPE | ❌ | 🔶 OWN | 🔶 OWN |
| users.disable | ✅ BREAK_GLASS | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| users.invite | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| users.import | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| roles.read | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| roles.manage | ✅ platform | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| permissions.read | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| settings.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| settings.manage | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| settings.academicYear.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| audit.read | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| audit.export | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

### 4.2 Academic Structure & Staff

| Permission | PSA | SA | HT | DH | BU | RG | TE | PA | ST |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| academicYears.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| academicYears.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| terms.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| terms.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| grades.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| grades.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| streams.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| streams.manage | ❌ | ✅ | ✅ | 🔶 WITH_APPROVAL | ❌ | ✅ | ❌ | ❌ | ❌ |
| subjects.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| subjects.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| gradeSubjects.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| gradeSubjects.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| rooms.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ |
| rooms.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| staff.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | 🔶 COLLEAGUES_IN_GRADE | ❌ | ❌ |
| staff.write | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| staff.delete | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| staff.import | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| staff.export | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |

### 4.3 Students, Enrolments, Guardians, Admissions

| Permission | PSA | SA | HT | DH | BU | RG | TE | PA | ST |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| students.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | 🔶 FEES_ONLY_FIELDS | ✅ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| students.write | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| students.delete | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| students.archive | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| students.import | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| students.export | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS | ❌ | ❌ |
| students.photo.upload | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | 🔶 OWN_CHILD_WITH_APPROVAL | ❌ |
| enrolments.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| enrolments.create | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| enrolments.transfer | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| enrolments.withdraw | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| enrolments.promote | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| enrolments.readHistory | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| guardians.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS_PARENTS | 🔶 OWN + OWN_CHILD_LINK | ❌ |
| guardians.write | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | 🔶 OWN | ❌ |
| guardians.link | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| guardians.billing.assign | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| guardians.delete | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.create | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.updateStatus | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.convert | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.delete | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| admissions.export | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |

### 4.4 Attendance, Timetable, Fees, Assessments, Messaging, Dashboards

| Permission | PSA | SA | HT | DH | BU | RG | TE | PA | ST |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| attendance.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| attendance.mark | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🔶 OWN_CLASS | ❌ | ❌ |
| attendance.edit | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| attendance.delete | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| attendance.export | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | 🔶 OWN_CLASS | ❌ | ❌ |
| timetable.read | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | 🔶 OWN + OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| timetable.manage | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| timetable.clone | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| timetable.export | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ |
| fees.structures.read | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.structures.manage | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.invoices.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CHILD | 🔶 OWN |
| fees.invoices.create | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.invoices.void | ❌ | ✅ | ❌ | ❌ | ✅ WITH_SA_APPROVAL | ❌ | ❌ | ❌ | ❌ |
| fees.invoices.export | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CHILD | 🔶 OWN |
| fees.payments.read | ❌ BREAK_GLASS | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | 🔶 OWN_CHILD | 🔶 OWN |
| fees.payments.create | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.payments.reverse | ❌ | ✅ | ❌ | ❌ | ✅ WITH_SA_APPROVAL | ❌ | ❌ | ❌ | ❌ |
| fees.payments.export | ❌ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.receipts.read | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CHILD | 🔶 OWN |
| fees.reports.read | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| fees.discount.approve | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| assessmentCategories.read | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ |
| assessmentCategories.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| grading.read | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ |
| grading.manage | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| assessments.read | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ |
| assessments.manage | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_SUBJECT | ❌ | ❌ |
| marks.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CLASS + OWN_SUBJECT | 🔶 OWN_CHILD | 🔶 OWN |
| marks.enter | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🔶 OWN_CLASS + OWN_SUBJECT | ❌ | ❌ |
| marks.editOwn | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🔶 OWN + DRAFT_ONLY | ❌ | ❌ |
| marks.submit | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🔶 OWN_CLASS | ❌ | ❌ |
| marks.approve | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| marks.unlock | ❌ | 🔶 BREAK_GLASS_AUDIT | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| marks.export | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CLASS | ❌ | ❌ |
| reportCards.read | ❌ BREAK_GLASS | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| reportCards.generate | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| reportCards.publish | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| reportCards.export | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | 🔶 OWN_CLASS | 🔶 OWN_CHILD | 🔶 OWN |
| messages.read | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | 🔶 OWN | 🔶 OWN | 🔶 OWN |
| messages.send | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| messages.sendOwnClass | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🔶 OWN_CLASS | 🔶 OWN_TEACHER | ❌ |
| messages.sendBroadcast | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| messages.delete | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| messages.readAll | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| dashboard.viewAdmin | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| dashboard.viewHead | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| dashboard.viewBursar | ❌ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| dashboard.viewRegistrar | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| dashboard.viewTeacher | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| dashboard.viewParent | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| dashboard.viewStudent | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| dashboard.viewPlatform | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 5. ROW-LEVEL SCOPING ENFORCEMENT (ON TOP OF TENANT FILTER)

All queries MUST include `tenant_id = @currentTenantId AND is_deleted=0`. Scoping adds extra predicate. Implementation in C# / MySQL EF Core / Dapper:

### 5.1 Teacher-Owns-Class

**Definition:** A teacher (user → staff_profile → staff_id) owns a class if:
- `streams.class_teacher_staff_id = myStaffId` OR
- Exists `timetable_slots WHERE teacher_staff_id = myStaffId AND stream_id = targetStreamId AND academic_year_id = currentYear AND term_id = currentTerm AND is_deleted=0`

**Enforcement Layers:**
1. **ORM Global Filter:** `modelBuilder.Entity<StudentEnrolment>().HasQueryFilter(e => e.TenantId == currentTenant && activeEnrolmentsForMyStreams.Contains(e.StreamId))` for Teacher role.
2. **SQL Predicate Example for students.read OWN_CLASS:**
```sql
SELECT s.* FROM students s
JOIN student_enrolments e ON e.student_id=s.id AND e.is_current=1 AND e.is_deleted=0
WHERE e.tenant_id=@tenantId AND s.is_deleted=0
AND e.stream_id IN (
  SELECT stream_id FROM timetable_slots 
  WHERE tenant_id=@tenantId AND teacher_staff_id=@myStaffId 
    AND academic_year_id=@currentYear AND is_deleted=0
  UNION
  SELECT id FROM streams 
  WHERE tenant_id=@tenantId AND class_teacher_staff_id=@myStaffId AND is_deleted=0
)
```
3. **Marks.enter:** Add `assessments.subject_id IN (SELECT subject_id FROM staff_subjects OR grade_subjects where teacher qualified)` + stream check. Prevents teacher A entering marks for teacher B class even if they guess assessment_id.
4. **Attendance.mark:** Only allowed if `attendance_records.stream_id` is in own class set, and `attendance_date` within term. Override `attendance.edit` (after 48h) denied for teacher, allowed for School Admin/Head with audit log `old_values/new_values` + reason.
5. **App Check:** API middleware loads `MyStaffId, MyOwnedStreamIds` into HttpContext.Items after JWT validated. Service layer calls `IAuthorizationService.EnsureOwnClass(streamId)`.

### 5.2 Parent-Owns-Child

**Definition:** Parent user → guardians.user_id = myUserId → guardians.id = myGuardianId. Linked via `guardian_student_links` -> child student_ids.

**Enforcement:**
```sql
-- All parent queries add:
WHERE fee_invoices.student_id IN (
  SELECT student_id FROM guardian_student_links
  WHERE tenant_id=@tenantId AND guardian_id=@myGuardianId AND is_deleted=0
)
-- plus tenant_id=@tenantId already
```
- **Isolation:** Guardian cannot see children of other guardians even in same tenant. List is per year: include `academic_year_id=@currentYear` to allow historical view but default current.
- **Billing Contact:** For fee reads, no need to be billing contact, but for correctness we allow any linked guardian to read, while `is_billing_contact` flag only affects who receives push/email notifications and who is shown as primary debtor.
- **Student Own:** `students.id = @myStudentId` where `users.id = @myUserId AND users.tenant_id=@tenantId AND students.id = (SELECT student_id FROM guardian? Actually student has no user_id directly, but we have students linked via users? In our schema students.current_enrolment_id + maybe students have user_id? In V1 student portal login is separate user with tenant_id and student link via `users`? We implement `student_user_links` or reuse `users` where `users` email matches student? Better: students have optional user_id? For simplicity: student portal user has `student_id` claim in JWT. Then filter `student_id = @myStudentId`.

- **Global Fallback:** If any scoped query returns zero rows due to scope mismatch, return 403 Forbidden with audit log `view_sensitive` attempt, not 404 to avoid enumeration, plus increment failed auth counter.

### 5.3 Implementation Pattern (C#)

```csharp
// In each repository base class
IQueryable<T> ApplyTenantFilter(IQueryable<T> q) => q.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted);

IQueryable<StudentEnrolment> ApplyTeacherScope(IQueryable<StudentEnrolment> q, ClaimsPrincipal user) {
  if (user.IsInRole("TEACHER")) {
    var ownedStreams = _cache.GetOwnedStreamsForTeacher(user.GetStaffId());
    return q.Where(e => ownedStreams.Contains(e.StreamId));
  }
  return q;
}
```

All services call `ApplyTenantFilter().ApplyScope()`.

---

## 6. OUTPUT ARTIFACTS

### 6.1 C# Static Permissions Class

See file `Permissions.cs` (also inline below). This is the single source of truth for string constants to avoid magic strings.

```csharp
// File: LearnCloud.Authorization/Permissions.cs
namespace LearnCloud.Authorization
{
  public static class Permissions
  {
    public static class Tenants
    {
      public const string Read = "tenants.read";
      public const string Create = "tenants.create";
      public const string Update = "tenants.update";
      public const string Suspend = "tenants.suspend";
    }
    public static class Platform
    {
      public static class Plans
      {
        public const string Read = "platform.plans.read";
        public const string Manage = "platform.plans.manage";
      }
      public static class Subscriptions
      {
        public const string Read = "platform.subscriptions.read";
        public const string Manage = "platform.subscriptions.manage";
      }
      public static class Audit
      {
        public const string Read = "platform.audit.read";
      }
    }
    public static class Users
    {
      public const string Read = "users.read";
      public const string Create = "users.create";
      public const string Update = "users.update";
      public const string Disable = "users.disable";
      public const string Invite = "users.invite";
      public const string Import = "users.import";
    }
    public static class Roles
    {
      public const string Read = "roles.read";
      public const string Manage = "roles.manage";
    }
    public static class Settings
    {
      public const string Read = "settings.read";
      public const string Manage = "settings.manage";
      public const string AcademicYearManage = "settings.academicYear.manage";
    }
    public static class Audit
    {
      public const string Read = "audit.read";
      public const string Export = "audit.export";
    }
    public static class Academic
    {
      public const string AcademicYearsRead = "academicYears.read";
      public const string AcademicYearsManage = "academicYears.manage";
      public const string TermsRead = "terms.read";
      public const string TermsManage = "terms.manage";
      public const string GradesRead = "grades.read";
      public const string GradesManage = "grades.manage";
      public const string StreamsRead = "streams.read";
      public const string StreamsManage = "streams.manage";
      public const string SubjectsRead = "subjects.read";
      public const string SubjectsManage = "subjects.manage";
      public const string GradeSubjectsRead = "gradeSubjects.read";
      public const string GradeSubjectsManage = "gradeSubjects.manage";
      public const string RoomsRead = "rooms.read";
      public const string RoomsManage = "rooms.manage";
    }
    public static class Staff
    {
      public const string Read = "staff.read";
      public const string Write = "staff.write";
      public const string Delete = "staff.delete";
      public const string Import = "staff.import";
      public const string Export = "staff.export";
    }
    public static class Students
    {
      public const string Read = "students.read";
      public const string Write = "students.write";
      public const string Delete = "students.delete";
      public const string Archive = "students.archive";
      public const string Import = "students.import";
      public const string Export = "students.export";
      public const string PhotoUpload = "students.photo.upload";
    }
    public static class Enrolments
    {
      public const string Read = "enrolments.read";
      public const string Create = "enrolments.create";
      public const string Transfer = "enrolments.transfer";
      public const string Withdraw = "enrolments.withdraw";
      public const string Promote = "enrolments.promote";
      public const string ReadHistory = "enrolments.readHistory";
    }
    public static class Guardians
    {
      public const string Read = "guardians.read";
      public const string Write = "guardians.write";
      public const string Link = "guardians.link";
      public const string BillingAssign = "guardians.billing.assign";
      public const string Delete = "guardians.delete";
    }
    public static class Admissions
    {
      public const string Read = "admissions.read";
      public const string Create = "admissions.create";
      public const string UpdateStatus = "admissions.updateStatus";
      public const string Convert = "admissions.convert";
      public const string Delete = "admissions.delete";
      public const string Export = "admissions.export";
    }
    public static class Attendance
    {
      public const string Read = "attendance.read";
      public const string Mark = "attendance.mark";
      public const string Edit = "attendance.edit";
      public const string Delete = "attendance.delete";
      public const string Export = "attendance.export";
    }
    public static class Timetable
    {
      public const string Read = "timetable.read";
      public const string Manage = "timetable.manage";
      public const string Clone = "timetable.clone";
      public const string Export = "timetable.export";
    }
    public static class Fees
    {
      public const string StructuresRead = "fees.structures.read";
      public const string StructuresManage = "fees.structures.manage";
      public const string InvoicesRead = "fees.invoices.read";
      public const string InvoicesCreate = "fees.invoices.create";
      public const string InvoicesVoid = "fees.invoices.void";
      public const string InvoicesExport = "fees.invoices.export";
      public const string PaymentsRead = "fees.payments.read";
      public const string PaymentsCreate = "fees.payments.create";
      public const string PaymentsReverse = "fees.payments.reverse";
      public const string PaymentsExport = "fees.payments.export";
      public const string ReceiptsRead = "fees.receipts.read";
      public const string ReportsRead = "fees.reports.read";
      public const string DiscountApprove = "fees.discount.approve";
    }
    public static class Assessments
    {
      public const string CategoriesRead = "assessmentCategories.read";
      public const string CategoriesManage = "assessmentCategories.manage";
      public const string GradingRead = "grading.read";
      public const string GradingManage = "grading.manage";
      public const string Read = "assessments.read";
      public const string Manage = "assessments.manage";
    }
    public static class Marks
    {
      public const string Read = "marks.read";
      public const string Enter = "marks.enter";
      public const string EditOwn = "marks.editOwn";
      public const string Submit = "marks.submit";
      public const string Approve = "marks.approve";
      public const string Unlock = "marks.unlock";
      public const string Export = "marks.export";
    }
    public static class ReportCards
    {
      public const string Read = "reportCards.read";
      public const string Generate = "reportCards.generate";
      public const string Publish = "reportCards.publish";
      public const string Export = "reportCards.export";
    }
    public static class Messages
    {
      public const string Read = "messages.read";
      public const string Send = "messages.send";
      public const string SendOwnClass = "messages.sendOwnClass";
      public const string SendBroadcast = "messages.sendBroadcast";
      public const string Delete = "messages.delete";
      public const string ReadAll = "messages.readAll";
    }
    public static class Dashboard
    {
      public const string ViewAdmin = "dashboard.viewAdmin";
      public const string ViewHead = "dashboard.viewHead";
      public const string ViewBursar = "dashboard.viewBursar";
      public const string ViewRegistrar = "dashboard.viewRegistrar";
      public const string ViewTeacher = "dashboard.viewTeacher";
      public const string ViewParent = "dashboard.viewParent";
      public const string ViewStudent = "dashboard.viewStudent";
      public const string ViewPlatform = "dashboard.viewPlatform";
    }

    // All permissions flat list for seeding
    public static readonly IReadOnlyList<string> All = new List<string>
    {
      Tenants.Read, Tenants.Create, Tenants.Update, Tenants.Suspend,
      Platform.Plans.Read, Platform.Plans.Manage, Platform.Subscriptions.Read, Platform.Subscriptions.Manage, Platform.Audit.Read,
      Users.Read, Users.Create, Users.Update, Users.Disable, Users.Invite, Users.Import,
      Roles.Read, Roles.Manage,
      Settings.Read, Settings.Manage, Settings.AcademicYearManage,
      Audit.Read, Audit.Export,
      Academic.AcademicYearsRead, Academic.AcademicYearsManage, Academic.TermsRead, Academic.TermsManage,
      Academic.GradesRead, Academic.GradesManage, Academic.StreamsRead, Academic.StreamsManage,
      Academic.SubjectsRead, Academic.SubjectsManage, Academic.GradeSubjectsRead, Academic.GradeSubjectsManage,
      Academic.RoomsRead, Academic.RoomsManage,
      Staff.Read, Staff.Write, Staff.Delete, Staff.Import, Staff.Export,
      Students.Read, Students.Write, Students.Delete, Students.Archive, Students.Import, Students.Export, Students.PhotoUpload,
      Enrolments.Read, Enrolments.Create, Enrolments.Transfer, Enrolments.Withdraw, Enrolments.Promote, Enrolments.ReadHistory,
      Guardians.Read, Guardians.Write, Guardians.Link, Guardians.BillingAssign, Guardians.Delete,
      Admissions.Read, Admissions.Create, Admissions.UpdateStatus, Admissions.Convert, Admissions.Delete, Admissions.Export,
      Attendance.Read, Attendance.Mark, Attendance.Edit, Attendance.Delete, Attendance.Export,
      Timetable.Read, Timetable.Manage, Timetable.Clone, Timetable.Export,
      Fees.StructuresRead, Fees.StructuresManage, Fees.InvoicesRead, Fees.InvoicesCreate, Fees.InvoicesVoid, Fees.InvoicesExport,
      Fees.PaymentsRead, Fees.PaymentsCreate, Fees.PaymentsReverse, Fees.PaymentsExport, Fees.ReceiptsRead, Fees.ReportsRead, Fees.DiscountApprove,
      Assessments.CategoriesRead, Assessments.CategoriesManage, Assessments.GradingRead, Assessments.GradingManage,
      Assessments.Read, Assessments.Manage,
      Marks.Read, Marks.Enter, Marks.EditOwn, Marks.Submit, Marks.Approve, Marks.Unlock, Marks.Export,
      ReportCards.Read, ReportCards.Generate, ReportCards.Publish, ReportCards.Export,
      Messages.Read, Messages.Send, Messages.SendOwnClass, Messages.SendBroadcast, Messages.Delete, Messages.ReadAll,
      Dashboard.ViewAdmin, Dashboard.ViewHead, Dashboard.ViewBursar, Dashboard.ViewRegistrar,
      Dashboard.ViewTeacher, Dashboard.ViewParent, Dashboard.ViewStudent, Dashboard.ViewPlatform
    };
  }

  public static class RoleCodes
  {
    public const string PlatformSuperadmin = "PLATFORM_SUPERADMIN";
    public const string SchoolAdmin = "SCHOOL_ADMIN";
    public const string HeadTeacher = "HEAD_TEACHER";
    public const string DeputyHead = "DEPUTY_HEAD";
    public const string Bursar = "BURSAR";
    public const string Registrar = "REGISTRAR";
    public const string Teacher = "TEACHER";
    public const string Parent = "PARENT";
    public const string Student = "STUDENT";
  }
}
```

### 6.2 Seed Data for New Tenant

See separate files: `seed_roles.sql` and `SeedDefaultRoles.cs`

**Key principle:** When Platform Admin provisions tenant (`tenants.create`), backend service runs `SeedDefaultRoles(tenant_id)` inside same transaction as academic year seed. It:

1. Inserts permissions if not exists (global)
2. Inserts 8 tenant roles with `is_system=1`
3. Inserts `role_permissions` mapping per matrix above
4. Creates default School Admin user invite (optional)

Example SQL call:
```sql
SET @tenant_id = 1; -- new tenant id from tenants insert
CALL SeedLearnCloudRoles(@tenant_id);
```

Full script in `seed_roles.sql`.

---

## 7. AUDIT & COMPLIANCE NOTES

- All `marks.approve`, `fees.invoices.void`, `fees.payments.reverse`, `attendance.edit`, `marks.unlock` must write to `audit_logs` with `old_values/new_values` + `created_by`.
- Platform Superadmin access to tenant student data must go via `platform.audit.read` + break-glass table `break_glass_requests` (shadow, not in V1 spec but recommended) and fire alert email to School Admin within 1h per DP-07.
- Parent scoping must be tested with two guardians sharing same phone but different children — ensure no leakage.

---

**End of Roles Matrix V1**
