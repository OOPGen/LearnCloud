# Software Requirements Specification
## LearnCloud - Multi-Tenant SaaS School Management System
**Version:** 1.0 | **Date:** 2026-08-02 | **Brand:** LearnClod / LearnCloud
**Audience:** Independent Schools (150 - 2,000 learners), Investors, Developers
**Status:** DRAFT FOR REVIEW

---

### DOCUMENT CONTROL
Author: LearnCloud Product Team
Location: Bulawayo, ZW - Target Market: Southern Africa independent schools (HQ: Bulawayo)
Branding Colors: #0F153A (Primary Navy), #5F3F96, #844CAD, #BC92CD, #195699, #307EC0, #3C2C59, #171725, #073A69, #5A94C1, #77A6C0, #B4B6B8

---

### 1. PURPOSE, SCOPE AND BUSINESS GOALS

**1.1 Purpose**
LearnCloud is a multi-tenant Software-as-a-Service (SaaS) platform designed to replace manual registers, spreadsheets, and disjointed WhatsApp groups in independent schools. It provides a single source of truth for student lifecycle from inquiry to graduation, with strict financial control and parent transparency. The system is built for schools that cannot afford enterprise ERP systems but need more reliability than generic tools.

**1.2 Scope**
The Version 1 system covers ten core domains: tenancy provisioning, student and staff master data, admissions pipeline, daily attendance, class timetabling, fees and invoicing, continuous assessment and report cards, role-based messaging, self-service portals for parents, students and teachers, and SaaS subscription management that bills the schools themselves. Each school (tenant) operates in a logically isolated data environment accessible via a subdomain (e.g., hillcrest.learncloud.co.zw). Version 1 is web-first (responsive) and does not include native mobile apps, biometric hardware integration, or full Learning Management System content authoring.

**1.3 Business Goals**
LearnCloud aims to be profitable by reducing administrative overhead for schools while creating a recurring, low-churn SaaS revenue stream. For schools, the goal is operational efficiency; for LearnCloud, the goal is scalable acquisition in the 150-2,000 learner segment.

**1.4 Measurable Success Criteria for V1 (12 months post-launch)**
1.  **Time-to-Value:** A new School Admin can complete onboarding wizard, import 500 students via CSV, create current term timetable, and issue first fee invoice in under 45 minutes without vendor support, verified in 95% of onboarding trials.
2.  **Financial Impact:** Schools using LearnCloud for at least one full term shall show a 35% reduction in average fee arrears days (from invoice due date to payment) and a 90% reduction in time taken to generate end-of-term report cards (measured from teacher marks entry deadline to parent portal publish).
3.  **Product Reliability & Adoption:** Achieve 99.5% monthly uptime, <2s average page load on simulated 3G, and 70% monthly active parents per active school by Term 2. Achieve Net Promoter Score >40 among School Admins.

### 2. STAKEHOLDERS AND USER ROLES

**2.1 Platform Admin (LearnCloud Staff)**
Goals: Provision new schools, monitor platform health, manage subscription plans, support School Admins, prevent data leakage between tenants.
Frustrations: Dealing with misconfigured tenant data, manual billing reconciliation, 2 AM downtime calls, schools requesting custom code forks.

**2.2 School Admin (Typically Principal's PA or IT Admin)**
Goals: Configure the school once and have it work; control who sees what; onboard staff quickly; get a single dashboard of admissions, arrears, attendance.
Frustrations: Locked out of critical settings by vendor; staff forgetting passwords; duplicating data entry across systems; poor bulk import tools.

**2.3 Head Teacher / Principal**
Goals: Real-time view of attendance, enrollment, fee collection, and academic performance; board-ready reports; compliance with Ministry inspection.
Frustrations: Data not trustworthy or out-of-date; needing to ask bursar for reports; lack of audit trail when marks are changed.

**2.4 Bursar / Finance Officer**
Goals: Accurate invoicing per fee structure, track payments (cash, bank, USD/ZWG), manage discounts, arrears and payment plans, produce receipts and debtors age analysis.
Frustrations: Parents claiming they paid but no record; spreadsheets with formula errors; inability to handle part-payments, sibling discounts, or multi-currency.

**2.5 Registrar / Admissions Officer**
Goals: Capture all inquiries, convert to enrolled, avoid over-enrollment in a stream, ensure required documents are collected.
Frustrations: Paper forms lost; same student applied twice with different names; parents repeatedly asking about application status.

**2.6 Teacher**
Goals: Mark attendance in <2 minutes per lesson; enter marks quickly; see only their classes and students; message parents of their class without sharing personal number.
Frustrations: System slow during morning register rush; having to re-enter same marks for different reports; being blamed for late report cards due to tool friction.

**2.7 Parent / Guardian**
Goals: See fees owed and paid, receipt, attendance of child, report cards, school announcements; communicate with teacher; pay or confirm payment.
Frustrations: Having to visit school for basic info; no notification when child absent; confusing fee statements; having multiple children requires multiple logins.

**2.8 Student (Upper Primary & Secondary)**
Goals: See timetable, homework/assessment dates, own results, announcements.
Frustrations: Interface too complex or patronizing; information outdated; no access when parent holds account.

### 3. FUNCTIONAL REQUIREMENTS

All requirements use the form "The system shall..." and include priority. MUST = V1, SHOULD = V2, COULD = Later. Each is testable with acceptance criteria.

#### 3.1 Tenancy and Onboarding
FR-T01 [MUST] The system shall logically isolate all school data by tenant_id such that no query from one tenant can return data from another tenant, verified by automated tenant isolation tests on every release.
FR-T02 [MUST] The system shall allow a Platform Admin to provision a new tenant by entering school name, primary contact, learner count band, and desired subdomain, and shall create the tenant environment in under 2 minutes.
FR-T03 [MUST] The system shall provide a 7-step onboarding wizard for School Admin: school profile, academic year and terms, grades and streams, fee structures, subjects, import users, invite staff.
FR-T04 [MUST] The system shall assign each tenant a subdomain (e.g., {slug}.learncloud.co.zw) and shall route login and portal sessions to that tenant.
FR-T05 [SHOULD] The system shall allow a School Admin to upload school logo, colors from the LearnCloud palette (#0F153A, #5F3F96 etc.) and generate branded portal headers.
FR-T06 [COULD] The system shall support custom domain mapping (e.g., portal.hillcrest.ac.zw) with managed SSL.
FR-T07 [MUST] The system shall seed default roles and permissions from a template and allow School Admin to modify permissions without code changes.

#### 3.2 Student and Staff Records
FR-SR01 [MUST] The system shall allow Registrar to create, view, update, and archive a student record with fields: unique student number (auto-generated per tenant), first name, last name, DOB, gender, grade, stream, enrolment status, guardian links (min 1), photo, and status (active/inactive/alumni).
FR-SR02 [MUST] The system shall enforce unique student number per tenant and prevent duplicate national ID per tenant with a warning.
FR-SR03 [MUST] The system shall allow linking up to 3 guardians per student with relationship and contact, and a primary contact flag.
FR-SR04 [MUST] The system shall allow School Admin to create staff records with employment type, subjects qualified, and role assignment, and shall link staff to a system user account.
FR-SR05 [MUST] The system shall support bulk import of students and staff via CSV with row-level error reporting and rollback if >10% rows fail.
FR-SR06 [MUST] The system shall maintain an audit log of all changes to student and staff master data showing who, when, old and new values.
FR-SR07 [SHOULD] The system shall allow document attachments to student records (birth certificate, ID) with file type and size validation (PDF/JPG/PNG, max 5MB).
FR-SR08 [COULD] The system shall generate a student QR ID card with name, class, and photo.

#### 3.3 Admissions
FR-AD01 [MUST] The system shall provide a public, tenant-branded online application form that captures applicant details, desired grade, prior school, and guardian details without requiring login.
FR-AD02 [MUST] The system shall assign each application a unique number and status: Inquiry, Applied, Documents Pending, Interview, Offered, Accepted, Rejected, Enrolled.
FR-AD03 [MUST] The system shall allow Registrar to move application through status workflow with timestamp and optional notes, and shall prevent enrollment beyond stream capacity set by School Admin.
FR-AD04 [MUST] The system shall allow one-click conversion of an Accepted application into an Active Student record, preserving data and attaching the original application.
FR-AD05 [SHOULD] The system shall automatically send email/SMS status updates to applicant guardian when status changes to Offered, Rejected, or Enrolled.
FR-AD06 [COULD] The system shall allow payment of application fee and attach proof to application.

#### 3.4 Attendance
FR-AT01 [MUST] The system shall allow Teachers to mark daily class attendance for their assigned class with codes: Present, Absent, Late, Sick, Excused, and an optional comment, and save for up to 60 learners in under 10 seconds of server time.
FR-AT02 [MUST] The system shall prevent editing of attendance after 48 hours unless School Admin overrides with a logged reason.
FR-AT03 [MUST] The system shall compute attendance percentage per student per term and flag students below 85% threshold in the dashboard.
FR-AT04 [SHOULD] The system shall automatically notify primary guardian via portal notification and email if student marked Absent without Excused flag by 10:00 AM school time.
FR-AT05 [SHOULD] The system shall provide bulk attendance entry per grade for assembly or event.
FR-AT06 [MUST] The system shall generate attendance register PDFs per class per term that match Ministry format.
FR-AT07 [COULD] The system shall support period-wise attendance for secondary schools.

#### 3.5 Timetable
FR-TT01 [MUST] The system shall allow School Admin to define academic year, terms (Term 1-3), periods per day (e.g., 8), break times, and rooms/labs.
FR-TT02 [MUST] The system shall allow School Admin to create timetable entries assigning Subject + Teacher + Grade/Stream + Room + Period + Day.
FR-TT03 [MUST] The system shall detect and block conflicts where same teacher or same room is double-booked in the same period, showing conflicting entry details.
FR-TT04 [MUST] The system shall provide personal timetable views for Teacher and Student filtered to current term.
FR-TT05 [SHOULD] The system shall allow timetable cloning from prior term with conflict re-validation.
FR-TT06 [COULD] The system shall auto-generate timetable using heuristic solver with manual adjustment, given teacher load and room constraints.

#### 3.6 Fees and Invoicing
FR-FE01 [MUST] The system shall allow Bursar to define fee structures per grade and term with line items (tuition, levy, boarding, transport), currency (USD/ZWG), and amount.
FR-FE02 [MUST] The system shall allow Bursar to generate fee invoices for a single student, a stream, a grade, or entire school in one batch job, and each invoice shall have a unique, sequential invoice number per tenant per year.
FR-FE03 [MUST] The system shall support discount types: sibling percentage, staff child, bursary fixed amount, and early payment, with approval by School Admin if discount >15%.
FR-FE04 [MUST] The system shall allow recording of payments against an invoice supporting full, partial, and overpayment, capturing method (cash, bank transfer, USD, EcoCash), reference, date, and proof upload.
FR-FE05 [MUST] The system shall automatically calculate balance due, arrears aging (30/60/90+ days), and shall update student fee status to Paid, Partial, Overdue, or Arrears.
FR-FE06 [MUST] The system shall generate a PDF receipt for each payment and a PDF statement for each student showing invoices, payments, and balances.
FR-FE07 [MUST] The system shall prevent deletion of an invoice that has a payment allocated; instead it shall require credit note with audit.
FR-FE08 [SHOULD] The system shall provide debtor's age analysis and collection report exportable to Excel/CSV.
FR-FE09 [COULD] The system shall integrate with Paynow / Stripe for online payment and automatically reconcile.

#### 3.7 Assessment and Report Cards
FR-AS01 [MUST] The system shall allow Head Teacher to define assessment categories per grade: e.g., Coursework 30%, Mid-Term 30%, Final Exam 40%, with weights summing to 100%.
FR-AS02 [MUST] The system shall allow Teacher to enter marks out of a configurable total for their subjects and classes, with validation (0 <= mark <= total) and bulk entry via spreadsheet-like grid.
FR-AS03 [MUST] The system shall compute weighted term average per subject per student using the category weights and shall compute overall average and class rank.
FR-AS04 [MUST] The system shall lock marks after Teacher submits and require Head Teacher approval to unlock, with audit of change.
FR-AS05 [MUST] The system shall generate a tenant-branded report card PDF per student per term showing subject marks, grade/letter, class average, attendance, teacher comment, and promotion recommendation.
FR-AS06 [MUST] The system shall allow Head Teacher to set promotion status per student: Promoted, Repeat, Conditional, Graduated.
FR-AS07 [SHOULD] The system shall allow configurable report card templates (logo from brand palette, signatures) without code changes.
FR-AS08 [SHOULD] The system shall publish report cards to Parent/Student portal at a scheduled time and send notification when published.
FR-AS09 [COULD] The system shall support competency-based assessment comments bank.

#### 3.8 Messaging and Announcements
FR-MG01 [MUST] The system shall allow School Admin to send announcements to all users or filtered by role, grade, or stream, via in-portal inbox and email fallback.
FR-MG02 [MUST] The system shall log all messages sent with sender, recipients (count), timestamp, and content, and provide read receipts for portal messages.
FR-MG03 [MUST] The system shall allow Teacher to message guardians of students in their class without exposing personal phone numbers.
FR-MG04 [SHOULD] The system shall allow parents to reply to a Teacher message and maintain a threaded conversation per student.
FR-MG05 [SHOULD] The system shall provide SMS gateway integration for critical alerts (fee overdue >60 days, absence notification) using a shared or dedicated sender ID.
FR-MG06 [COULD] The system shall support scheduled announcements and WhatsApp Cloud API integration.

#### 3.9 Portals (Parent, Student, Teacher)
FR-PO01 [MUST] The system shall provide role-based dashboards: Teacher sees My Classes, Attendance Today, Marks Due; Parent sees Children Cards with Fee Balance, Attendance %, Latest Report; Student sees My Timetable and Results.
FR-PO02 [MUST] The system shall allow Parent with multiple children in same tenant to switch child context without separate login.
FR-PO03 [MUST] The system shall enforce that Parent can only view data for their linked children and Student can only view own data.
FR-PO04 [MUST] The system shall allow password self-reset via email and enforce lockout after 5 failed attempts for 15 minutes.
FR-PO05 [SHOULD] The system shall provide a mobile-responsive portal that scores >90 on Lighthouse mobile audit.
FR-PO06 [SHOULD] The system shall allow Guardian to update own contact details with approval by Registrar.
FR-PO07 [COULD] The system shall provide offline-viewable report card PDF history for 3 years.

#### 3.10 Subscriptions and Billing (LearnCloud SaaS Revenue)
FR-SB01 [MUST] The system shall provide Platform Admin with subscription plans: Starter (up to 300 learners), Growth (301-800), Scale (801-2000) with monthly and annual pricing.
FR-SB02 [MUST] The system shall meter active students (enrollment status = Active) per tenant nightly and shall flag over-limit tenants when exceeding plan limit by >10%.
FR-SB03 [MUST] The system shall generate Platform-to-School invoices monthly/annually, track payment status, and apply 7-day grace period then restrict tenant to read-only mode if overdue >14 days.
FR-SB04 [MUST] The system shall allow Platform Admin to suspend or reactivate a tenant and record reason.
FR-SB05 [SHOULD] The system shall integrate with a payment gateway for automatic subscription collection and send payment failure notifications.
FR-SB06 [COULD] The system shall provide usage analytics per tenant (DAU, modules used) for churn prediction.

### 4. NON-FUNCTIONAL REQUIREMENTS

NFR-01 Performance [MUST] Under simulated 3G network (1.6 Mbps down / 768 Kbps up, 300ms RTT) on a mid-range Android device (4x CPU slowdown), the login page, dashboard, and attendance marking page shall achieve Largest Contentful Paint <2.0 seconds and Time to Interactive <3.0 seconds with cache disabled, measured via Lighthouse.
NFR-02 Scalability [MUST] The system shall support 500 concurrent active users per tenant cluster and 50 tenants (approx. 5,000 total concurrent) with average API response <400ms at 95th percentile, and database CPU <70% at peak.
NFR-03 Availability [MUST] The platform shall achieve 99.5% monthly uptime excluding scheduled maintenance. Monthly downtime budget = 3 hours 39 minutes. Downtime shall be measured via external uptime probe every 60 seconds on /health endpoint.
NFR-04 Backup and Recovery [MUST] The system shall take encrypted nightly full backups at 02:00 CAT and incremental WAL backups hourly. Recovery Point Objective (RPO) = 4 hours. Recovery Time Objective (RTO) = 8 hours. Backup restore shall be tested quarterly with documented test log.
NFR-05 Security [MUST] All traffic over TLS 1.2+. Passwords hashed with Argon2id. All data encrypted at rest (AES-256). API rate-limited: 100 requests/minute/user, 1000/minute/tenant. Tenant isolation enforced at ORM layer.
NFR-06 Browser Support [MUST] Support latest 2 versions of Chrome, Edge, Firefox, Safari, and Chrome Android. No support required for IE11.
NFR-07 Data Retention [MUST] Transaction logs retained 2 years, backups retained 90 days, audit logs retained 5 years, student records retained 7 years post-graduation per Ministry requirement.
NFR-08 Internationalization [SHOULD] Support English for V1; date format DD/MM/YYYY, currency USD base with ZWG optional display; timezone Africa/Harare as default tenant timezone (covers Bulawayo HQ, Africa/Harare is IANA for Zimbabwe).

### 5. DATA PROTECTION REQUIREMENTS FOR RECORDS CONCERNING MINORS

As LearnCloud processes personal data of children under 18, it is subject to Zimbabwe Data Protection Act [Chapter 12:07], Ministry of Primary and Secondary Education circulars, and by design aligned to GDPR/KVKK principles for minors.

DP-01 Lawful Basis and Consent [MUST] Enrollment shall require explicit consent checkbox from parent/guardian for processing of learner personal data, with consent text versioned and stored. Student aged 13+ shall also provide assent where applicable.
DP-02 Data Minimization [MUST] System shall collect only fields defined in FR-SR01 and required for school operation. No collection of biometric, health data beyond disability/ allergy relevant flag without additional explicit consent.
DP-03 Access Control [MUST] Role-based access: Parent sees only own child, Teacher sees only assigned classes, Bursar sees financial but not sensitive health notes, Platform Admin has no access to tenant personal data without documented break-glass and audit.
DP-04 Age-Appropriate Portal [MUST] Student portal for users <13 shall not allow direct messaging to non-teachers and shall hide contact details of other students.
DP-05 Encryption and Pseudonymization [MUST] All learner PII encrypted at rest, TLS in transit. Exported reports for analysis shall support pseudonymized learner IDs.
DP-06 Retention and Right to be Forgotten [MUST] Upon written request from parent/guardian and approval by School Admin, the system shall anonymize student record within 30 days after legal retention period, while retaining financial audit records for 7 years as anonymized ledger.
DP-07 Breach Notification [MUST] Any suspected personal data breach involving minor records shall trigger alert to Platform Admin within 1 hour and notification to affected School Admin within 24 hours, with incident log and remediation plan.
DP-08 Audit and Logging [MUST] Every access to learner sensitive fields (DOB, address, guardian contact, report card) shall be logged with user, timestamp, and purpose (view/edit/export).
DP-09 Third-Party Processors [MUST] No learner data shall be sent to third-party AI, analytics, or marketing services without explicit school opt-in. Email/SMS providers shall be listed as processors in Data Processing Agreement.
DP-10 Data Residency [SHOULD] Primary data shall be hosted in Africa region (South Africa or Zimbabwe region where available) with backup copy in different availability zone, to satisfy regulator guidance.

### 6. ASSUMPTIONS, CONSTRAINTS AND OUT-OF-SCOPE FOR V1

**Assumptions:**
A1 Schools have reliable internet for admin staff, though parents may have intermittent 3G.
A2 All schools operate on Zimbabwean academic calendar: Term 1 Jan-Apr, Term 2 May-Aug, Term 3 Sep-Dec, but system allows custom terms.
A3 Teaching language is English; report cards in English.
A4 One school = one tenant; group of schools requires separate tenants in V1.
A5 School has at least one designated School Admin with basic computer literacy.
A6 E-Signatures on report cards are acceptable if portal published; printed version may still require wet signature outside system.

**Constraints:**
C1 Team size small (2-4 developers) so V1 must use managed services (Postgres, object storage) not self-managed infra.
C2 Budget constraints require 3G-optimized frontend, no native apps for V1.
C3 Must comply with Zimbabwe Data Protection Act before launch.
C4 Logo and brand colors are predetermined from provided palettes: Primary Navy #0F153A and Purple #5F3F96 must meet WCAG AA contrast against white for text.
C5 Invoicing must support multi-currency display but accounting base currency is USD.

**Explicit Out-of-Scope for V1:**
O1 Full Learning Management System (video lessons, quizzes, assignments grading workflow) - COULD later.
O2 Library management, hostel/dormitory allocation, transport/GPS bus tracking.
O3 Biometric fingerprint/face attendance hardware integration.
O4 Complex payroll, staff leave management beyond master record.
O5 E-commerce school shop, inventory.
O6 Native iOS/Android apps; V1 is responsive web only.
O7 Custom report card designer with drag-and-drop; V1 uses fixed templates.
O8 AI predictive analytics (dropout risk, fee default prediction).
O9 Third-party accounting package full sync (Pastel, QuickBooks) - only CSV export in V1.
O10 Online exam proctoring.

### 7. RISKS

Risk Register with Likelihood (Low/Medium/High), Impact (Low/Med/High), Mitigation.

R1 - Tenant Data Leakage (Likelihood: Low, Impact: High). Mitigation: ORM tenant filter mandatory code review, automated isolation tests, row-level security in Postgres, principle of least privilege, quarterly penetration test.

R2 - Scope Creep - Schools demand custom features that delay V1 (Likelihood: High, Impact: High). Mitigation: Ruthless MUST definition in this SRS, public roadmap, say no to custom code forks; offer SHOULD as paid V2 modules.

R3 - Low Parent Adoption due to Data Costs (Likelihood: Medium, Impact: High). Mitigation: Lite portal <500KB initial load, 2G/3G optimization, SMS fallback for critical alerts, USSD inquiry COULD later.

R4 - Fee Data Accuracy Disputes Leading to Legal Claims (Likelihood: Medium, Impact: Medium). Mitigation: Immutable financial ledger, no delete of invoices/payments, full audit trail, receipt PDFs, dual approval for discounts >15%.

R5 - Key Personnel Dependency - Only one dev knows billing module (Likelihood: Medium, Impact: High). Mitigation: Documentation in this SRS plus code docs, pair programming, bus factor review monthly.

R6 - Regulatory Change - New Ministry report format (Likelihood: Medium, Impact: Medium). Mitigation: Configurable report templates in V2, but V1 provides PDF export plus CSV raw data so school can adapt.

R7 - Backup Restore Failure (Likelihood: Low, Impact: High). Mitigation: Automated nightly backup + quarterly restore drill, 4h RPO target monitored, offsite encrypted copy.

R8 - Churn if Onboarding Takes >1 Hour (Likelihood: High, Impact: High). Mitigation: Onboarding wizard, CSV templates, in-app help tours, video guides, success criteria #1 enforced in UAT.

### 8. GLOSSARY

**Academic Year:** 12-month cycle defined by school, typically Jan-Dec in Zimbabwe, containing 3 Terms.

**Term:** A subdivision of Academic Year (Term 1, 2, 3). Each term has start/end, invoicing, attendance, assessments.

**Grade / Form:** Level of learning (e.g., Grade 1-7, Form 1-4). Also called Year.

**Stream / Class:** Subdivision of Grade (e.g., Grade 5 Blue, Form 3A). Has capacity limit, class teacher, timetable.

**Enrolment:** Status of a student officially admitted and active in a grade/stream for a term. Distinct from applicant.

**Applicant / Inquiry:** Person who has applied but not yet enrolled; tracked in admissions pipeline.

**Promotion:** Process at end of academic year determining if student moves to next Grade (Promoted), repeats (Repeat), or conditional.

**Arrears:** Outstanding fee balances overdue beyond due date; shown in age buckets.

**Fee Structure:** Set of fee line items applicable to a Grade or specific student per Term.

**Invoice:** Formal demand for payment issued to student/guardian for a Term.

**Receipt:** Proof of payment against invoice.

**Guardian:** Parent or legal guardian linked to student; primary guardian receives notifications.

**Bursar:** Finance officer responsible for fees, invoices, payments, receipts.

**Registrar:** Staff responsible for admissions and student master records.

**Tenant:** One independent school's isolated data environment within multi-tenant platform.

**Portal:** Web interface for specific role (Parent Portal, Student Portal, Teacher Portal).

**RPO (Recovery Point Objective):** Maximum data loss period acceptable, here 4 hours.

**RTO (Recovery Time Objective):** Maximum time to restore service after disaster, here 8 hours.

**MUST / SHOULD / COULD:** Priority as per MoSCoW; MUST required for V1 launch.

---

**Appendix A - Branding Reference**
Login background must use AI-generated images of school related scenes: library with students reading, modern classroom, school science lab, school trip to national park, assembly. Images to be generated in LearnCloud palette tones: Deep navy #0F153A, Purple #5F3F96, Blue #307EC0 overlay gradient for readability. Structure similar to attached login reference (left branded image, right login form).

**Appendix B - Acceptance Criteria For V1 Launch**
All MUST requirements implemented, tenant isolation tests passing, NFR-01 to NFR-04 measured and meeting targets, DP-01 to DP-09 audited, 2 pilot schools onboarded with <45 min time-to-first-invoice, security review completed.

---
End of Document
