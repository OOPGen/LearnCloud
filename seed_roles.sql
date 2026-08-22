-- LearnCloud Roles & Permissions Seed - Bulawayo HQ
-- MySQL 8.0, utf8mb4, tenant_id leading indexes, soft delete columns
-- Usage: SET @tenant_id = <new_tenant_id>; SOURCE seed_roles.sql; OR CALL SeedLearnCloudRoles(@tenant_id);

-- 0. Ensure permissions table has all V1 perms (idempotent)
INSERT INTO permissions (code, name, module, created_at, updated_at) VALUES
-- Platform & Tenancy
('tenants.read','View tenants','tenancy', NOW(), NOW()),
('tenants.create','Create tenant','tenancy', NOW(), NOW()),
('tenants.update','Update tenant profile','tenancy', NOW(), NOW()),
('tenants.suspend','Suspend/reactivate tenant','tenancy', NOW(), NOW()),
('platform.plans.read','View plans catalog','platform', NOW(), NOW()),
('platform.plans.manage','Manage plans','platform', NOW(), NOW()),
('platform.subscriptions.read','View tenant subscriptions','platform', NOW(), NOW()),
('platform.subscriptions.manage','Manage subscriptions','platform', NOW(), NOW()),
('platform.audit.read','Read platform audit','platform', NOW(), NOW()),
-- Users Roles Settings Audit
('users.read','View users','users', NOW(), NOW()),
('users.create','Create users','users', NOW(), NOW()),
('users.update','Update users','users', NOW(), NOW()),
('users.disable','Disable users','users', NOW(), NOW()),
('users.invite','Invite users','users', NOW(), NOW()),
('users.import','Import users CSV','users', NOW(), NOW()),
('roles.read','View roles','roles', NOW(), NOW()),
('roles.manage','Manage roles','roles', NOW(), NOW()),
('permissions.read','View permissions','roles', NOW(), NOW()),
('settings.read','View school settings','settings', NOW(), NOW()),
('settings.manage','Manage school settings color #0F153A','settings', NOW(), NOW()),
('settings.academicYear.manage','Manage academic years','settings', NOW(), NOW()),
('audit.read','View audit logs','audit', NOW(), NOW()),
('audit.export','Export audit logs','audit', NOW(), NOW()),
-- Academic
('academicYears.read','View academic years','academic', NOW(), NOW()),
('academicYears.manage','Manage academic years','academic', NOW(), NOW()),
('terms.read','View terms','academic', NOW(), NOW()),
('terms.manage','Manage terms','academic', NOW(), NOW()),
('grades.read','View grades','academic', NOW(), NOW()),
('grades.manage','Manage grades','academic', NOW(), NOW()),
('streams.read','View streams/classes','academic', NOW(), NOW()),
('streams.manage','Manage streams','academic', NOW(), NOW()),
('subjects.read','View subjects','academic', NOW(), NOW()),
('subjects.manage','Manage subjects','academic', NOW(), NOW()),
('gradeSubjects.read','View grade-subject mapping','academic', NOW(), NOW()),
('gradeSubjects.manage','Manage grade-subject mapping','academic', NOW(), NOW()),
('rooms.read','View rooms','academic', NOW(), NOW()),
('rooms.manage','Manage rooms','academic', NOW(), NOW()),
-- Staff
('staff.read','View staff','staff', NOW(), NOW()),
('staff.write','Create/update staff','staff', NOW(), NOW()),
('staff.delete','Delete staff','staff', NOW(), NOW()),
('staff.import','Import staff','staff', NOW(), NOW()),
('staff.export','Export staff','staff', NOW(), NOW()),
-- Students
('students.read','View students','students', NOW(), NOW()),
('students.write','Create/update students','students', NOW(), NOW()),
('students.delete','Delete students','students', NOW(), NOW()),
('students.archive','Archive students','students', NOW(), NOW()),
('students.import','Import students','students', NOW(), NOW()),
('students.export','Export students','students', NOW(), NOW()),
('students.photo.upload','Upload student photo','students', NOW(), NOW()),
('enrolments.read','View enrolments','enrolments', NOW(), NOW()),
('enrolments.create','Create enrolment','enrolments', NOW(), NOW()),
('enrolments.transfer','Transfer stream','enrolments', NOW(), NOW()),
('enrolments.withdraw','Withdraw enrolment','enrolments', NOW(), NOW()),
('enrolments.promote','Promote/repeat decision','enrolments', NOW(), NOW()),
('enrolments.readHistory','View enrolment history','enrolments', NOW(), NOW()),
('guardians.read','View guardians','guardians', NOW(), NOW()),
('guardians.write','Create/update guardians','guardians', NOW(), NOW()),
('guardians.link','Link guardian to student','guardians', NOW(), NOW()),
('guardians.billing.assign','Assign billing contact is_billing_contact','guardians', NOW(), NOW()),
('guardians.delete','Delete guardian','guardians', NOW(), NOW()),
-- Admissions
('admissions.read','View admissions','admissions', NOW(), NOW()),
('admissions.create','Create admission inquiry','admissions', NOW(), NOW()),
('admissions.updateStatus','Update admission status','admissions', NOW(), NOW()),
('admissions.convert','Convert admission to student','admissions', NOW(), NOW()),
('admissions.delete','Delete admission','admissions', NOW(), NOW()),
('admissions.export','Export admissions','admissions', NOW(), NOW()),
-- Attendance
('attendance.read','View attendance','attendance', NOW(), NOW()),
('attendance.mark','Mark attendance','attendance', NOW(), NOW()),
('attendance.edit','Edit attendance after 48h','attendance', NOW(), NOW()),
('attendance.delete','Delete attendance','attendance', NOW(), NOW()),
('attendance.export','Export attendance register','attendance', NOW(), NOW()),
-- Timetable
('timetable.read','View timetable','timetable', NOW(), NOW()),
('timetable.manage','Manage timetable slots','timetable', NOW(), NOW()),
('timetable.clone','Clone timetable','timetable', NOW(), NOW()),
('timetable.export','Export timetable','timetable', NOW(), NOW()),
-- Fees
('fees.structures.read','View fee structures','fees', NOW(), NOW()),
('fees.structures.manage','Manage fee structures amount DECIMAL(18,2)+currency','fees', NOW(), NOW()),
('fees.invoices.read','View invoices','fees', NOW(), NOW()),
('fees.invoices.create','Generate invoices','fees', NOW(), NOW()),
('fees.invoices.void','Void/credit invoice','fees', NOW(), NOW()),
('fees.invoices.export','Export invoices/statements','fees', NOW(), NOW()),
('fees.payments.read','View payments','fees', NOW(), NOW()),
('fees.payments.create','Record payment','fees', NOW(), NOW()),
('fees.payments.reverse','Reverse payment','fees', NOW(), NOW()),
('fees.payments.export','Export payments','fees', NOW(), NOW()),
('fees.receipts.read','View receipts','fees', NOW(), NOW()),
('fees.reports.read','View age analysis/collection reports arrears','fees', NOW(), NOW()),
('fees.discount.approve','Approve discount >15%','fees', NOW(), NOW()),
-- Assessments
('assessmentCategories.read','View assessment categories weight%','assessments', NOW(), NOW()),
('assessmentCategories.manage','Manage categories','assessments', NOW(), NOW()),
('grading.read','View grading scales','assessments', NOW(), NOW()),
('grading.manage','Manage grading scales','assessments', NOW(), NOW()),
('assessments.read','View assessments/tests','assessments', NOW(), NOW()),
('assessments.manage','Manage assessments','assessments', NOW(), NOW()),
('marks.read','View marks','marks', NOW(), NOW()),
('marks.enter','Enter marks for own class/subject','marks', NOW(), NOW()),
('marks.editOwn','Edit own draft marks','marks', NOW(), NOW()),
('marks.submit','Submit marks for approval','marks', NOW(), NOW()),
('marks.approve','Approve marks','marks', NOW(), NOW()),
('marks.unlock','Unlock approved marks','marks', NOW(), NOW()),
('marks.export','Export gradebook','marks', NOW(), NOW()),
('reportCards.read','View report cards','reports', NOW(), NOW()),
('reportCards.generate','Generate draft report cards','reports', NOW(), NOW()),
('reportCards.publish','Publish report cards to portals','reports', NOW(), NOW()),
('reportCards.export','Export report cards PDF bulk','reports', NOW(), NOW()),
-- Messaging
('messages.read','View own inbox','messages', NOW(), NOW()),
('messages.send','Send message to any','messages', NOW(), NOW()),
('messages.sendOwnClass','Send to own class parents','messages', NOW(), NOW()),
('messages.sendBroadcast','Send broadcast announcement','messages', NOW(), NOW()),
('messages.delete','Delete message','messages', NOW(), NOW()),
('messages.readAll','Read all messages audit','messages', NOW(), NOW()),
-- Dashboards
('dashboard.viewAdmin','View School Admin dashboard','dashboard', NOW(), NOW()),
('dashboard.viewHead','View Head Teacher dashboard','dashboard', NOW(), NOW()),
('dashboard.viewBursar','View Bursar dashboard','dashboard', NOW(), NOW()),
('dashboard.viewRegistrar','View Registrar dashboard','dashboard', NOW(), NOW()),
('dashboard.viewTeacher','View Teacher dashboard','dashboard', NOW(), NOW()),
('dashboard.viewParent','View Parent dashboard children cards','dashboard', NOW(), NOW()),
('dashboard.viewStudent','View Student dashboard','dashboard', NOW(), NOW()),
('dashboard.viewPlatform','View Platform dashboard','dashboard', NOW(), NOW())
ON DUPLICATE KEY UPDATE name=VALUES(name), module=VALUES(module), updated_at=NOW();

-- 1. Platform Superadmin role (tenant_id NULL)
INSERT INTO roles (tenant_id, code, name, is_system, description, created_at, updated_at, is_deleted)
VALUES (NULL, 'PLATFORM_SUPERADMIN', 'Platform Superadmin', 1, 'LearnCloud staff - God platform, break-glass for tenant PII', NOW(), NOW(), 0)
ON DUPLICATE KEY UPDATE name=VALUES(name), updated_at=NOW();

SET @platform_role_id = (SELECT id FROM roles WHERE tenant_id IS NULL AND code='PLATFORM_SUPERADMIN' LIMIT 1);

-- Platform superadmin gets only platform perms + tenants + users.read break-glass
DELETE FROM role_permissions WHERE tenant_id IS NULL AND role_id=@platform_role_id; -- clean for re-seed (platform perms not tenant-scoped, but we store tenant_id NULL)
INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
SELECT NULL, @platform_role_id, p.id, NOW(), NOW(), 0 FROM permissions p
WHERE p.code IN (
  'tenants.read','tenants.create','tenants.update','tenants.suspend',
  'platform.plans.read','platform.plans.manage','platform.subscriptions.read','platform.subscriptions.manage','platform.audit.read',
  'users.read','roles.read','permissions.read','settings.read','audit.read','audit.export','dashboard.viewPlatform'
) ON DUPLICATE KEY UPDATE updated_at=NOW();

-- 2. Tenant default roles seed procedure
DROP PROCEDURE IF EXISTS SeedLearnCloudRoles;
DELIMITER $$
CREATE PROCEDURE SeedLearnCloudRoles(IN p_tenant_id BIGINT UNSIGNED)
BEGIN
  DECLARE v_school_admin BIGINT UNSIGNED;
  DECLARE v_head BIGINT UNSIGNED;
  DECLARE v_deputy BIGINT UNSIGNED;
  DECLARE v_bursar BIGINT UNSIGNED;
  DECLARE v_registrar BIGINT UNSIGNED;
  DECLARE v_teacher BIGINT UNSIGNED;
  DECLARE v_parent BIGINT UNSIGNED;
  DECLARE v_student BIGINT UNSIGNED;

  -- Insert 8 tenant roles
  INSERT INTO roles (tenant_id, code, name, is_system, description, created_at, updated_at, is_deleted) VALUES
  (p_tenant_id, 'SCHOOL_ADMIN', 'School Admin', 1, 'IT/Admin - all tenant permissions', NOW(), NOW(), 0),
  (p_tenant_id, 'HEAD_TEACHER', 'Head Teacher', 1, 'Principal - academic approve, report publish', NOW(), NOW(), 0),
  (p_tenant_id, 'DEPUTY_HEAD', 'Deputy Head', 1, 'Deputy - academic manage without final publish', NOW(), NOW(), 0),
  (p_tenant_id, 'BURSAR', 'Bursar', 1, 'Finance - fees, invoices, payments', NOW(), NOW(), 0),
  (p_tenant_id, 'REGISTRAR', 'Registrar', 1, 'Admissions & student master', NOW(), NOW(), 0),
  (p_tenant_id, 'TEACHER', 'Teacher', 1, 'Scoped OWN_CLASS - attendance mark, marks enter own subject', NOW(), NOW(), 0),
  (p_tenant_id, 'PARENT', 'Parent', 1, 'Scoped OWN_CHILD - sees children cards fee/attendance/report', NOW(), NOW(), 0),
  (p_tenant_id, 'STUDENT', 'Student', 1, 'Scoped OWN - sees own timetable/results', NOW(), NOW(), 0)
  ON DUPLICATE KEY UPDATE name=VALUES(name), updated_at=NOW();

  SET v_school_admin = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='SCHOOL_ADMIN' LIMIT 1);
  SET v_head = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='HEAD_TEACHER' LIMIT 1);
  SET v_deputy = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='DEPUTY_HEAD' LIMIT 1);
  SET v_bursar = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='BURSAR' LIMIT 1);
  SET v_registrar = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='REGISTRAR' LIMIT 1);
  SET v_teacher = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='TEACHER' LIMIT 1);
  SET v_parent = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='PARENT' LIMIT 1);
  SET v_student = (SELECT id FROM roles WHERE tenant_id=p_tenant_id AND code='STUDENT' LIMIT 1);

  -- Clean existing mappings for re-seed idempotency
  DELETE FROM role_permissions WHERE tenant_id=p_tenant_id AND role_id IN (v_school_admin, v_head, v_deputy, v_bursar, v_registrar, v_teacher, v_parent, v_student);

  -- SCHOOL_ADMIN = ALL tenant perms (exclude platform.* and tenants.* which are platform only)
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_school_admin, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code NOT LIKE 'platform.%' AND p.code NOT LIKE 'tenants.%';

  -- HEAD_TEACHER = academic god + reports publish + fees read + attendance read/edit
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_head, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'users.read','users.invite','roles.read','settings.read','settings.academicYear.manage','audit.read',
    'academicYears.read','academicYears.manage','terms.read','terms.manage','grades.read','grades.manage','streams.read','streams.manage','subjects.read','subjects.manage','gradeSubjects.read','gradeSubjects.manage','rooms.read','rooms.manage',
    'staff.read','staff.export',
    'students.read','students.archive','students.export','enrolments.read','enrolments.transfer','enrolments.withdraw','enrolments.promote','enrolments.readHistory',
    'guardians.read','admissions.read','admissions.updateStatus','admissions.export',
    'attendance.read','attendance.edit','attendance.export',
    'timetable.read','timetable.manage','timetable.clone','timetable.export',
    'fees.structures.read','fees.invoices.read','fees.invoices.export','fees.payments.read','fees.payments.export','fees.receipts.read','fees.reports.read','fees.discount.approve',
    'assessmentCategories.read','assessmentCategories.manage','grading.read','grading.manage','assessments.read','assessments.manage',
    'marks.read','marks.approve','marks.unlock','marks.export',
    'reportCards.read','reportCards.generate','reportCards.publish','reportCards.export',
    'messages.read','messages.send','messages.sendBroadcast','messages.delete','messages.readAll',
    'dashboard.viewAdmin','dashboard.viewHead','dashboard.viewBursar','dashboard.viewRegistrar'
  );

  -- DEPUTY_HEAD = same as HEAD but no publish, no settings.manage, no discount.approve, no unlock
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_deputy, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'users.read','roles.read','settings.read','audit.read',
    'academicYears.read','terms.read','grades.read','streams.read','streams.manage','subjects.read','gradeSubjects.read','rooms.read',
    'staff.read','staff.export',
    'students.read','students.export','enrolments.read','enrolments.transfer','enrolments.withdraw','enrolments.promote','enrolments.readHistory','guardians.read','admissions.read','admissions.updateStatus','admissions.export',
    'attendance.read','attendance.export',
    'timetable.read','timetable.manage','timetable.export',
    'fees.structures.read','fees.invoices.read','fees.invoices.export','fees.payments.read','fees.receipts.read','fees.reports.read',
    'assessmentCategories.read','grading.read','assessments.read','assessments.manage','marks.read','marks.approve','marks.export',
    'reportCards.read','reportCards.generate','reportCards.export',
    'messages.read','messages.send','messages.sendBroadcast',
    'dashboard.viewHead','dashboard.viewRegistrar'
  );

  -- BURSAR = fees god + students.read limited + guardians billing + reports
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_bursar, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'users.read','roles.read','settings.read','audit.read',
    'academicYears.read','terms.read','grades.read','streams.read',
    'students.read','students.export','enrolments.read','enrolments.readHistory','guardians.read','guardians.billing.assign',
    'attendance.read',
    'fees.structures.read','fees.structures.manage','fees.invoices.read','fees.invoices.create','fees.invoices.void','fees.invoices.export','fees.payments.read','fees.payments.create','fees.payments.reverse','fees.payments.export','fees.receipts.read','fees.reports.read',
    'messages.read','messages.send','dashboard.viewBursar'
  );

  -- REGISTRAR = admissions + students + guardians + enrolments
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_registrar, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'users.read','users.create','users.update','users.invite','users.import','roles.read','settings.read',
    'academicYears.read','terms.read','grades.read','streams.read','streams.manage','subjects.read','gradeSubjects.read','rooms.read',
    'staff.read','staff.export',
    'students.read','students.write','students.archive','students.import','students.export','students.photo.upload','enrolments.read','enrolments.create','enrolments.transfer','enrolments.withdraw','enrolments.promote','enrolments.readHistory','guardians.read','guardians.write','guardians.link','guardians.billing.assign','guardians.delete',
    'admissions.read','admissions.create','admissions.updateStatus','admissions.convert','admissions.delete','admissions.export',
    'attendance.read','attendance.export','timetable.read','timetable.export',
    'messages.read','messages.send','dashboard.viewRegistrar'
  );

  -- TEACHER = scoped OWN_CLASS + OWN_SUBJECT (enforced at app layer on top of permission)
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_teacher, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'users.read','roles.read','settings.read',
    'academicYears.read','terms.read','grades.read','streams.read','subjects.read','gradeSubjects.read','rooms.read','academicYears.read',
    'staff.read',
    'students.read','students.export','enrolments.read','enrolments.readHistory','guardians.read',
    'attendance.read','attendance.mark','attendance.export',
    'timetable.read','timetable.export',
    'assessmentCategories.read','grading.read','assessments.read','assessments.manage','marks.read','marks.enter','marks.editOwn','marks.submit','marks.export','reportCards.read','reportCards.export',
    'messages.read','messages.sendOwnClass','dashboard.viewTeacher'
  );

  -- PARENT = scoped OWN_CHILD
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_parent, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'roles.read','settings.read','academicYears.read','terms.read','grades.read','streams.read','subjects.read','gradeSubjects.read',
    'students.read','enrolments.read','enrolments.readHistory','guardians.read',
    'attendance.read','timetable.read','timetable.export',
    'fees.invoices.read','fees.invoices.export','fees.payments.read','fees.receipts.read',
    'assessmentCategories.read','grading.read','assessments.read','marks.read','reportCards.read','reportCards.export',
    'messages.read','dashboard.viewParent'
  );

  -- STUDENT = scoped OWN
  INSERT INTO role_permissions (tenant_id, role_id, permission_id, created_at, updated_at, is_deleted)
  SELECT p_tenant_id, v_student, p.id, NOW(), NOW(), 0 FROM permissions p
  WHERE p.code IN (
    'roles.read','settings.read','academicYears.read','terms.read','grades.read','streams.read','subjects.read','gradeSubjects.read',
    'students.read','enrolments.read','enrolments.readHistory','attendance.read','timetable.read','timetable.export',
    'fees.invoices.read','fees.invoices.export','fees.payments.read','fees.receipts.read',
    'assessmentCategories.read','grading.read','assessments.read','marks.read','reportCards.read','reportCards.export',
    'messages.read','dashboard.viewStudent'
  );

END$$
DELIMITER ;

-- Example usage for new tenant id 1 (Petra High Bulawayo):
-- SET @tenant_id = 1;
-- CALL SeedLearnCloudRoles(@tenant_id);
