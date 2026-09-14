# Supabase Deployment Guide - LearnCloud with RLS for All 39 Tables
**Date:** 2026-08-09
**Migrated From:** MySQL 8.0 → PostgreSQL 15 (Supabase)

## Overview
This guide sets up Supabase with Row Level Security for all 39+ tables, storage buckets with tenant isolation, and deployment.

## Step 1: Create Supabase Project
- Go to supabase.com → New Project → Region af-south-1
- Save DB password
- Wait 2 min

## Step 2: Get Connection Strings
Dashboard → Database → Connection string
- Pooled 6543 for API: `postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true`
- Direct 5432 for migrations: `postgres://postgres:[pass]@db.[ref].supabase.co:5432/postgres`
- API Keys: Dashboard → API → anon key, service_role key

## Step 3: Storage Buckets with Tenant Isolation RLS

### Create Buckets (Dashboard → Storage → New Bucket)

#### Bucket: `logos` (public, 2MB, image/*)
- **Description:** School logos for report cards, invoices - public read, tenant write
- **Public:** True
- **File size limit:** 2MB
- **Allowed MIME:** image/*
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/logos/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for logos"
ON storage.objects FOR ALL
USING (
  bucket_id = 'logos' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'logos' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all logos"
ON storage.objects FOR ALL
USING (
  bucket_id = 'logos' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `payroll` (private, 5MB, .csv, .pdf, .xlsx)
- **Description:** Payroll exports - NationalID, salary, bank account - highly sensitive, private
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .csv, .pdf, .xlsx
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/payroll/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for payroll"
ON storage.objects FOR ALL
USING (
  bucket_id = 'payroll' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'payroll' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all payroll"
ON storage.objects FOR ALL
USING (
  bucket_id = 'payroll' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `assignments` (private, 10MB, .pdf, .docx, .jpg, .png)
- **Description:** Student assignment submissions
- **Public:** False
- **File size limit:** 10MB
- **Allowed MIME:** .pdf, .docx, .jpg, .png
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/assignments/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for assignments"
ON storage.objects FOR ALL
USING (
  bucket_id = 'assignments' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'assignments' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all assignments"
ON storage.objects FOR ALL
USING (
  bucket_id = 'assignments' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `expense-proofs` (private, 5MB, .pdf, .jpg, .png)
- **Description:** Finance expense supporting documents
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .pdf, .jpg, .png
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/expense-proofs/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for expense-proofs"
ON storage.objects FOR ALL
USING (
  bucket_id = 'expense-proofs' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'expense-proofs' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all expense-proofs"
ON storage.objects FOR ALL
USING (
  bucket_id = 'expense-proofs' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `staff-docs` (private, 5MB, .pdf, .jpg, .png)
- **Description:** HR staff documents: national_id, contract, qualification, police clearance
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .pdf, .jpg, .png
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/staff-docs/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for staff-docs"
ON storage.objects FOR ALL
USING (
  bucket_id = 'staff-docs' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'staff-docs' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all staff-docs"
ON storage.objects FOR ALL
USING (
  bucket_id = 'staff-docs' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `student-photos` (private, 2MB, image/*)
- **Description:** Student photos
- **Public:** False
- **File size limit:** 2MB
- **Allowed MIME:** image/*
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/student-photos/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for student-photos"
ON storage.objects FOR ALL
USING (
  bucket_id = 'student-photos' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'student-photos' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all student-photos"
ON storage.objects FOR ALL
USING (
  bucket_id = 'student-photos' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `homework-attachments` (private, 10MB, .pdf, .docx, .zip)
- **Description:** Teacher homework attachments
- **Public:** False
- **File size limit:** 10MB
- **Allowed MIME:** .pdf, .docx, .zip
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/homework-attachments/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for homework-attachments"
ON storage.objects FOR ALL
USING (
  bucket_id = 'homework-attachments' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'homework-attachments' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all homework-attachments"
ON storage.objects FOR ALL
USING (
  bucket_id = 'homework-attachments' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `report-cards` (private, 5MB, .pdf)
- **Description:** Published report card PDFs
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .pdf
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/report-cards/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for report-cards"
ON storage.objects FOR ALL
USING (
  bucket_id = 'report-cards' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'report-cards' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all report-cards"
ON storage.objects FOR ALL
USING (
  bucket_id = 'report-cards' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `invoices` (private, 5MB, .pdf)
- **Description:** Fee invoice PDFs
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .pdf
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/invoices/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for invoices"
ON storage.objects FOR ALL
USING (
  bucket_id = 'invoices' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'invoices' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all invoices"
ON storage.objects FOR ALL
USING (
  bucket_id = 'invoices' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

#### Bucket: `receipts` (private, 5MB, .pdf)
- **Description:** Payment receipt PDFs
- **Public:** False
- **File size limit:** 5MB
- **Allowed MIME:** .pdf
- **RLS Policy:**
```sql
-- Allow tenant to only access own folder: bucket/receipts/{tenantId}/{fileName}
CREATE POLICY "Tenant isolation for receipts"
ON storage.objects FOR ALL
USING (
  bucket_id = 'receipts' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
)
WITH CHECK (
  bucket_id = 'receipts' AND 
  (storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint
);

-- Allow platform superadmin to access all (for support)
CREATE POLICY "Platform admin can access all receipts"
ON storage.objects FOR ALL
USING (
  bucket_id = 'receipts' AND 
  (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN'
);
```

### Storage RLS Summary
- All private buckets use folder structure `{tenantId}/{fileName}` where tenantId is first folder
- RLS checks `(storage.foldername(name))[1]::bigint = (auth.jwt() ->> 'tid')::bigint`
- Platform admin role `PLATFORM_SUPERADMIN` can access all via second policy
- Public bucket `logos` still has RLS for write, but read is public for report cards? Actually logos should be public read for report card PDFs that are shared? For security, make logos private too and serve via signed URL, or public read okay since logo not sensitive.

## Step 4: Enable RLS for All 39 Tables + Policies

### Enable RLS
```sql
-- Enable RLS for all tenant-owned tables
ALTER TABLE tenant_subscriptions ENABLE ROW LEVEL SECURITY;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE academic_years ENABLE ROW LEVEL SECURITY;
ALTER TABLE terms ENABLE ROW LEVEL SECURITY;
ALTER TABLE grades ENABLE ROW LEVEL SECURITY;
ALTER TABLE streams ENABLE ROW LEVEL SECURITY;
ALTER TABLE rooms ENABLE ROW LEVEL SECURITY;
ALTER TABLE subjects ENABLE ROW LEVEL SECURITY;
ALTER TABLE grade_subjects ENABLE ROW LEVEL SECURITY;
ALTER TABLE staff_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE students ENABLE ROW LEVEL SECURITY;
ALTER TABLE guardians ENABLE ROW LEVEL SECURITY;
ALTER TABLE guardian_student_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE student_enrolments ENABLE ROW LEVEL SECURITY;
ALTER TABLE admission_applications ENABLE ROW LEVEL SECURITY;
ALTER TABLE attendance_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE timetable_slots ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_structures ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_structure_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_invoice_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_payment_allocations ENABLE ROW LEVEL SECURITY;
ALTER TABLE assessment_categories ENABLE ROW LEVEL SECURITY;
ALTER TABLE grading_scales ENABLE ROW LEVEL SECURITY;
ALTER TABLE grading_scale_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE assessments ENABLE ROW LEVEL SECURITY;
ALTER TABLE student_marks ENABLE ROW LEVEL SECURITY;
ALTER TABLE report_cards ENABLE ROW LEVEL SECURITY;
ALTER TABLE messages ENABLE ROW LEVEL SECURITY;
ALTER TABLE message_recipients ENABLE ROW LEVEL SECURITY;
ALTER TABLE ai_provider_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE report_comment_drafts ENABLE ROW LEVEL SECURITY;
ALTER TABLE attendance_anomalies ENABLE ROW LEVEL SECURITY;
ALTER TABLE at_risk_flags ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_attendance_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE period_definitions ENABLE ROW LEVEL SECURITY;
ALTER TABLE attendance_registers ENABLE ROW LEVEL SECURITY;
ALTER TABLE timetables ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE discounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoice_sequences ENABLE ROW LEVEL SECURITY;
ALTER TABLE receipt_sequences ENABLE ROW LEVEL SECURITY;
ALTER TABLE learner_credits ENABLE ROW LEVEL SECURITY;
ALTER TABLE credit_notes ENABLE ROW LEVEL SECURITY;
ALTER TABLE fee_invoice_batches ENABLE ROW LEVEL SECURITY;
ALTER TABLE examination_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE examination_session_assessments ENABLE ROW LEVEL SECURITY;
ALTER TABLE examination_slots ENABLE ROW LEVEL SECURITY;
ALTER TABLE composite_weightings ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_expenses ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_approval_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE bank_accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE cash_book_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_staff ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_contracts ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_qualifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_staff_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_departments ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_leave_types ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_leave_entitlements ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_leave_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_appraisal_cycles ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_appraisal_criteria ENABLE ROW LEVEL SECURITY;
ALTER TABLE hr_appraisals ENABLE ROW LEVEL SECURITY;
ALTER TABLE hostel_buildings ENABLE ROW LEVEL SECURITY;
ALTER TABLE hostel_rooms ENABLE ROW LEVEL SECURITY;
ALTER TABLE hostel_allocations ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_books ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_copies ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_members ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_loans ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_reservations ENABLE ROW LEVEL SECURITY;
ALTER TABLE library_fines ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_routes ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_stops ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_vehicles ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_drivers ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE transport_attendance ENABLE ROW LEVEL SECURITY;
ALTER TABLE online_payment_initiations ENABLE ROW LEVEL SECURITY;
ALTER TABLE gateway_transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE payment_gateway_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE subscriptions ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_domains ENABLE ROW LEVEL SECURITY;
ALTER TABLE wizard_progress ENABLE ROW LEVEL SECURITY;

```

### Create Policies - Tenant Isolation (Supabase Auth JWT with tid claim)

For self-hosted Postgres using `current_setting('app.current_tenant_id')`, use:
```sql
USING (tenant_id = current_setting('app.current_tenant_id', true)::bigint)
```

For Supabase using `auth.jwt() ->> 'tid'` (custom claim from our JWT, not Supabase Auth default):

```sql
-- Example for students table
CREATE POLICY tenant_isolation_students ON students
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);
```

Below are policies for all 39 tables:

-- tenant_subscriptions
CREATE POLICY tenant_isolation_tenant_subscriptions ON tenant_subscriptions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_tenant_subscriptions ON tenant_subscriptions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- users
CREATE POLICY tenant_isolation_users ON users
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_users ON users
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- roles
CREATE POLICY tenant_isolation_roles ON roles
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_roles ON roles
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- role_permissions
CREATE POLICY tenant_isolation_role_permissions ON role_permissions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_role_permissions ON role_permissions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- user_roles
CREATE POLICY tenant_isolation_user_roles ON user_roles
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_user_roles ON user_roles
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- academic_years
CREATE POLICY tenant_isolation_academic_years ON academic_years
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_academic_years ON academic_years
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- terms
CREATE POLICY tenant_isolation_terms ON terms
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_terms ON terms
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- grades
CREATE POLICY tenant_isolation_grades ON grades
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_grades ON grades
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- streams
CREATE POLICY tenant_isolation_streams ON streams
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_streams ON streams
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- rooms
CREATE POLICY tenant_isolation_rooms ON rooms
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_rooms ON rooms
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- subjects
CREATE POLICY tenant_isolation_subjects ON subjects
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_subjects ON subjects
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- grade_subjects
CREATE POLICY tenant_isolation_grade_subjects ON grade_subjects
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_grade_subjects ON grade_subjects
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- staff_profiles
CREATE POLICY tenant_isolation_staff_profiles ON staff_profiles
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_staff_profiles ON staff_profiles
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- students
CREATE POLICY tenant_isolation_students ON students
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_students ON students
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- guardians
CREATE POLICY tenant_isolation_guardians ON guardians
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_guardians ON guardians
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- guardian_student_links
CREATE POLICY tenant_isolation_guardian_student_links ON guardian_student_links
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_guardian_student_links ON guardian_student_links
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- student_enrolments
CREATE POLICY tenant_isolation_student_enrolments ON student_enrolments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_student_enrolments ON student_enrolments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- admission_applications
CREATE POLICY tenant_isolation_admission_applications ON admission_applications
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_admission_applications ON admission_applications
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- attendance_records
CREATE POLICY tenant_isolation_attendance_records ON attendance_records
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_attendance_records ON attendance_records
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- timetable_slots
CREATE POLICY tenant_isolation_timetable_slots ON timetable_slots
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_timetable_slots ON timetable_slots
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_structures
CREATE POLICY tenant_isolation_fee_structures ON fee_structures
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_structures ON fee_structures
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_structure_items
CREATE POLICY tenant_isolation_fee_structure_items ON fee_structure_items
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_structure_items ON fee_structure_items
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_invoices
CREATE POLICY tenant_isolation_fee_invoices ON fee_invoices
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_invoices ON fee_invoices
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_invoice_items
CREATE POLICY tenant_isolation_fee_invoice_items ON fee_invoice_items
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_invoice_items ON fee_invoice_items
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_payments
CREATE POLICY tenant_isolation_fee_payments ON fee_payments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_payments ON fee_payments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_payment_allocations
CREATE POLICY tenant_isolation_fee_payment_allocations ON fee_payment_allocations
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_payment_allocations ON fee_payment_allocations
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- assessment_categories
CREATE POLICY tenant_isolation_assessment_categories ON assessment_categories
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_assessment_categories ON assessment_categories
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- grading_scales
CREATE POLICY tenant_isolation_grading_scales ON grading_scales
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_grading_scales ON grading_scales
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- grading_scale_items
CREATE POLICY tenant_isolation_grading_scale_items ON grading_scale_items
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_grading_scale_items ON grading_scale_items
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- assessments
CREATE POLICY tenant_isolation_assessments ON assessments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_assessments ON assessments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- student_marks
CREATE POLICY tenant_isolation_student_marks ON student_marks
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_student_marks ON student_marks
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- report_cards
CREATE POLICY tenant_isolation_report_cards ON report_cards
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_report_cards ON report_cards
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- messages
CREATE POLICY tenant_isolation_messages ON messages
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_messages ON messages
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- message_recipients
CREATE POLICY tenant_isolation_message_recipients ON message_recipients
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_message_recipients ON message_recipients
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- ai_provider_settings
CREATE POLICY tenant_isolation_ai_provider_settings ON ai_provider_settings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_ai_provider_settings ON ai_provider_settings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- report_comment_drafts
CREATE POLICY tenant_isolation_report_comment_drafts ON report_comment_drafts
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_report_comment_drafts ON report_comment_drafts
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- attendance_anomalies
CREATE POLICY tenant_isolation_attendance_anomalies ON attendance_anomalies
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_attendance_anomalies ON attendance_anomalies
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- at_risk_flags
CREATE POLICY tenant_isolation_at_risk_flags ON at_risk_flags
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_at_risk_flags ON at_risk_flags
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- tenant_attendance_settings
CREATE POLICY tenant_isolation_tenant_attendance_settings ON tenant_attendance_settings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_tenant_attendance_settings ON tenant_attendance_settings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- period_definitions
CREATE POLICY tenant_isolation_period_definitions ON period_definitions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_period_definitions ON period_definitions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- attendance_registers
CREATE POLICY tenant_isolation_attendance_registers ON attendance_registers
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_attendance_registers ON attendance_registers
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- timetables
CREATE POLICY tenant_isolation_timetables ON timetables
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_timetables ON timetables
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_items
CREATE POLICY tenant_isolation_fee_items ON fee_items
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_items ON fee_items
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- discounts
CREATE POLICY tenant_isolation_discounts ON discounts
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_discounts ON discounts
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- invoice_sequences
CREATE POLICY tenant_isolation_invoice_sequences ON invoice_sequences
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_invoice_sequences ON invoice_sequences
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- receipt_sequences
CREATE POLICY tenant_isolation_receipt_sequences ON receipt_sequences
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_receipt_sequences ON receipt_sequences
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- learner_credits
CREATE POLICY tenant_isolation_learner_credits ON learner_credits
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_learner_credits ON learner_credits
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- credit_notes
CREATE POLICY tenant_isolation_credit_notes ON credit_notes
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_credit_notes ON credit_notes
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- fee_invoice_batches
CREATE POLICY tenant_isolation_fee_invoice_batches ON fee_invoice_batches
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_fee_invoice_batches ON fee_invoice_batches
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- examination_sessions
CREATE POLICY tenant_isolation_examination_sessions ON examination_sessions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_examination_sessions ON examination_sessions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- examination_session_assessments
CREATE POLICY tenant_isolation_examination_session_assessments ON examination_session_assessments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_examination_session_assessments ON examination_session_assessments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- examination_slots
CREATE POLICY tenant_isolation_examination_slots ON examination_slots
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_examination_slots ON examination_slots
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- composite_weightings
CREATE POLICY tenant_isolation_composite_weightings ON composite_weightings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_composite_weightings ON composite_weightings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- finance_expenses
CREATE POLICY tenant_isolation_finance_expenses ON finance_expenses
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_finance_expenses ON finance_expenses
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- finance_approval_requests
CREATE POLICY tenant_isolation_finance_approval_requests ON finance_approval_requests
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_finance_approval_requests ON finance_approval_requests
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- bank_accounts
CREATE POLICY tenant_isolation_bank_accounts ON bank_accounts
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_bank_accounts ON bank_accounts
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- cash_book_entries
CREATE POLICY tenant_isolation_cash_book_entries ON cash_book_entries
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_cash_book_entries ON cash_book_entries
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_staff
CREATE POLICY tenant_isolation_hr_staff ON hr_staff
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_staff ON hr_staff
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_contracts
CREATE POLICY tenant_isolation_hr_contracts ON hr_contracts
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_contracts ON hr_contracts
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_qualifications
CREATE POLICY tenant_isolation_hr_qualifications ON hr_qualifications
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_qualifications ON hr_qualifications
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_staff_documents
CREATE POLICY tenant_isolation_hr_staff_documents ON hr_staff_documents
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_staff_documents ON hr_staff_documents
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_departments
CREATE POLICY tenant_isolation_hr_departments ON hr_departments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_departments ON hr_departments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_leave_types
CREATE POLICY tenant_isolation_hr_leave_types ON hr_leave_types
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_leave_types ON hr_leave_types
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_leave_entitlements
CREATE POLICY tenant_isolation_hr_leave_entitlements ON hr_leave_entitlements
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_leave_entitlements ON hr_leave_entitlements
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_leave_requests
CREATE POLICY tenant_isolation_hr_leave_requests ON hr_leave_requests
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_leave_requests ON hr_leave_requests
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_appraisal_cycles
CREATE POLICY tenant_isolation_hr_appraisal_cycles ON hr_appraisal_cycles
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_appraisal_cycles ON hr_appraisal_cycles
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_appraisal_criteria
CREATE POLICY tenant_isolation_hr_appraisal_criteria ON hr_appraisal_criteria
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_appraisal_criteria ON hr_appraisal_criteria
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hr_appraisals
CREATE POLICY tenant_isolation_hr_appraisals ON hr_appraisals
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hr_appraisals ON hr_appraisals
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hostel_buildings
CREATE POLICY tenant_isolation_hostel_buildings ON hostel_buildings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hostel_buildings ON hostel_buildings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hostel_rooms
CREATE POLICY tenant_isolation_hostel_rooms ON hostel_rooms
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hostel_rooms ON hostel_rooms
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- hostel_allocations
CREATE POLICY tenant_isolation_hostel_allocations ON hostel_allocations
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_hostel_allocations ON hostel_allocations
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_books
CREATE POLICY tenant_isolation_library_books ON library_books
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_books ON library_books
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_copies
CREATE POLICY tenant_isolation_library_copies ON library_copies
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_copies ON library_copies
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_members
CREATE POLICY tenant_isolation_library_members ON library_members
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_members ON library_members
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_loans
CREATE POLICY tenant_isolation_library_loans ON library_loans
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_loans ON library_loans
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_reservations
CREATE POLICY tenant_isolation_library_reservations ON library_reservations
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_reservations ON library_reservations
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- library_fines
CREATE POLICY tenant_isolation_library_fines ON library_fines
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_library_fines ON library_fines
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_routes
CREATE POLICY tenant_isolation_transport_routes ON transport_routes
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_routes ON transport_routes
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_stops
CREATE POLICY tenant_isolation_transport_stops ON transport_stops
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_stops ON transport_stops
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_vehicles
CREATE POLICY tenant_isolation_transport_vehicles ON transport_vehicles
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_vehicles ON transport_vehicles
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_drivers
CREATE POLICY tenant_isolation_transport_drivers ON transport_drivers
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_drivers ON transport_drivers
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_assignments
CREATE POLICY tenant_isolation_transport_assignments ON transport_assignments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_assignments ON transport_assignments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- transport_attendance
CREATE POLICY tenant_isolation_transport_attendance ON transport_attendance
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_transport_attendance ON transport_attendance
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- online_payment_initiations
CREATE POLICY tenant_isolation_online_payment_initiations ON online_payment_initiations
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_online_payment_initiations ON online_payment_initiations
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- gateway_transactions
CREATE POLICY tenant_isolation_gateway_transactions ON gateway_transactions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_gateway_transactions ON gateway_transactions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- payment_gateway_settings
CREATE POLICY tenant_isolation_payment_gateway_settings ON payment_gateway_settings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_payment_gateway_settings ON payment_gateway_settings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- platform_invoices
CREATE POLICY tenant_isolation_platform_invoices ON platform_invoices
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_platform_invoices ON platform_invoices
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- platform_payments
CREATE POLICY tenant_isolation_platform_payments ON platform_payments
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_platform_payments ON platform_payments
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- subscriptions
CREATE POLICY tenant_isolation_subscriptions ON subscriptions
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_subscriptions ON subscriptions
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- tenant_settings
CREATE POLICY tenant_isolation_tenant_settings ON tenant_settings
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_tenant_settings ON tenant_settings
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- tenant_domains
CREATE POLICY tenant_isolation_tenant_domains ON tenant_domains
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_tenant_domains ON tenant_domains
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- wizard_progress
CREATE POLICY tenant_isolation_wizard_progress ON wizard_progress
FOR ALL
USING (tenant_id = (auth.jwt() ->> 'tid')::bigint)
WITH CHECK (tenant_id = (auth.jwt() ->> 'tid')::bigint);

-- Platform admin can access all for support (with audit)
CREATE POLICY platform_admin_all_wizard_progress ON wizard_progress
FOR ALL
USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN')
WITH CHECK ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

-- For tables with tenant_id NULLABLE (e.g., users where tenant_id NULL = platform admin), need special handling:
-- Allow platform admin to see all, and tenant users to see only own tenant or null if they are platform?
-- For users table:
-- DROP POLICY IF EXISTS tenant_isolation_users ON users;
-- CREATE POLICY tenant_isolation_users ON users
-- FOR ALL
-- USING (
--   tenant_id IS NULL AND (auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN' -- platform admin can see platform users
--   OR tenant_id = (auth.jwt() ->> 'tid')::bigint
-- );

-- For tenants table (not tenant-owned, it IS tenant):
-- Tenants table should be visible to all authenticated? Or only platform admin can list all, tenant admin can only see own?
-- For multi-tenant, tenant should only see own tenant row:
-- ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
-- CREATE POLICY tenant_can_see_own ON tenants FOR SELECT USING (id = (auth.jwt() ->> 'tid')::bigint);
-- CREATE POLICY platform_can_see_all ON tenants FOR ALL USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');

### Global Tables (No RLS or Platform-Only RLS)

These tables are global, no tenant_id or tenant_id NULLABLE for system roles:

- `tenants`: No RLS or platform admin only. For `subscription_plans`, allow all authenticated to read (for pricing page), only platform admin to write.
- `subscription_plans`: No RLS or platform admin only. For `subscription_plans`, allow all authenticated to read (for pricing page), only platform admin to write.
- `permissions`: No RLS or platform admin only. For `subscription_plans`, allow all authenticated to read (for pricing page), only platform admin to write.
- `plans`: No RLS or platform admin only. For `subscription_plans`, allow all authenticated to read (for pricing page), only platform admin to write.

```sql
-- Example for subscription_plans (global catalog)
ALTER TABLE subscription_plans ENABLE ROW LEVEL SECURITY;
CREATE POLICY allow_read_all ON subscription_plans FOR SELECT USING (true);
CREATE POLICY platform_write ON subscription_plans FOR ALL USING ((auth.jwt() ->> 'role') = 'PLATFORM_SUPERADMIN');
```

## Step 5: Apply Migrations

```bash
# Use direct connection for DDL
export DATABASE_URL="postgres://postgres:[PASS]@db.[ref].supabase.co:5432/postgres"
psql $DATABASE_URL -f src/LearnCloud.Auth/Migrations/V19_Postgres_Migration.sql

# Or via dotnet ef
dotnet ef database update --project src/LearnCloud.Auth --startup-project src/LearnCloud.Api --connection "$DATABASE_URL"
```

## Step 6: Deploy API and Web

### Docker Compose Without Postgres (Supabase External DB)

Use `docker-compose.supabase.yml` (see file):

```bash
cd deployment
docker-compose -f docker-compose.supabase.yml up -d --build api web nginx redis
# No postgres service, API points to Supabase pooled URI via .env.production
```

### Fly.io

```bash
fly launch --name learncloud-api --region sin
fly secrets set ConnectionStrings__Default="postgres://postgres.[ref]:[pass]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true" Jwt__Secret="..." Supabase__Url="https://[ref].supabase.co" Supabase__Key="..." 
fly deploy
```

### Vercel/Netlify for Web

```bash
cd src/LearnCloud.Web
npm run build
vercel --prod
```

## Step 7: Test Tenant Isolation

```bash
# Test RLS: As tenant A, try to read tenant B's students
# Set JWT with tid=1, try SELECT * FROM students WHERE tenant_id=2 -> should return 0 rows due to RLS policy

# Test storage RLS:
# As tenant 1, upload to payroll/1/file.csv -> success
# As tenant 1, try to read payroll/2/file.csv -> should fail 403 via RLS

# Run CI guards
./scripts/ci_tenant_isolation_guards.sh
# Should PASS

# Test file storage tenant check via API
curl -H "Authorization: Bearer $TOKEN_TENANT1" https://api.learncloud.co.zw/api/files/payroll/2/<guid>.csv
# Should return 403 Forbid + log security warning
```

## Step 8: Monitoring

- Supabase Dashboard → Database → Roles, RLS policies enabled (green check)
- Supabase Dashboard → Storage → Buckets → Policies enabled
- UptimeRobot for https://learncloud.co.zw/health
- Sentry for error tracking

## Cost Summary

- Free tier: 500MB DB, 1GB storage, 50k MAU - for dev only
- Pro $25/mo: 8GB DB, 100GB storage, 100k MAU - recommended for 50 schools x 2000 learners
- Team $599/mo for large scale

## Files

- `V19_Postgres_Migration.sql` - Initial Postgres schema with RLS-ready tables
- `docker-compose.supabase.yml` - Without postgres service, API points to Supabase
- `SupabaseStorageService.cs` - Storage abstraction local + Supabase
- `POSTGRES_MIGRATION_GUIDE.md` - Full migration guide MySQL→Postgres

## Next

- Apply V19 migration to Supabase via direct connection
- Create storage buckets with RLS policies above
- Enable RLS for all 39 tables with policies above
- Deploy API to Fly.io/Render with pooled connection string
- Test tenant isolation end-to-end
