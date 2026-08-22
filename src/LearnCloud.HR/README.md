# HR Module — Staff Records, Contracts, Qualifications, Leave, Appraisals, Disciplinary, Reporting + Payroll Analysis (Do Not Build Calculation Yet)

## HR Module Built (Low Risk)

**Entities:**
- **Staff** tenant_id, staff_number STA-2026-00001 unique, first/last, national_id, dob, gender, employment_type permanent/contract/part_time/temporary, employment_status active/on_leave/suspended/terminated/retired, department_id, designation Mathematics Teacher/Bursar/Head, hire_date, confirmation_date, phone, email, address, user_id linked account, photo_url, current_salary decimal currency USD for HR reference not payroll calc, contracts, qualifications, documents
- **Contract** staff_id, contract_number CONT-2026-00001, contract_type permanent/fixed_term/probation, start_date, end_date, probation_end_date, salary, currency, status active/expired/terminated, terms, last_reminder_sent_at, created_by_user_id — expiry reminders via background job: if end_date <= 30 days and last_reminder_sent_at null or >7 days ago, send reminder to HR manager and staff
- **Qualification** staff_id, qualification_name B.Ed Mathematics, institution University of Zimbabwe, year_obtained, grade, certificate_number, is_verified, verified_by
- **StaffDocument** staff_id, document_type national_id/contract/qualification_certificate/police_clearance/medical, file_name, file_url 5MB, file_size, content_type, expiry_date for police clearance, is_verified, uploaded_by
- **Department** name Sciences, code SCI, hod_staff_id, is_active
- **LeaveType** name Annual/Sick/Maternity/Study/Compassionate/Unpaid, code ANNUAL/SICK/MATERNITY, description, default_entitlement_days 22 annual 90 sick 98 maternity per NEC, is_paid, requires_document sick true medical cert, is_carry_forward_allowed, max_carry_forward_days 5, accrual_rule yearly/monthly/none
- **LeaveEntitlement** staff_id, leave_type_id, academic_year 2026, entitled_days 22 may be prorated mid-year joiner, carried_forward_days, used_days, remaining_days = entitled + carried - used, expiry_date carry forward expiry
- **LeaveRequest** staff_id, leave_type_id, start_date, end_date, days_requested calculated excluding weekends (simplified, should use school calendar), reason, status pending/approved/rejected/cancelled/withdrawn, approver_user_id, approved_at, approver_comment, document_url, requested_by_user_id, is_half_day, requested date
- **AppraisalCycle** name 2026 Mid-Year, academic_year_id, term_id, start_date, end_date, status draft/active/closed, description, criteria collection
- **AppraisalCriterion** cycle_id, name Classroom Management/Subject Knowledge/Punctuality, description, weight 1, max_score 5 scale, sort_order
- **Appraisal** cycle_id, staff_id, appraiser_user_id who appraised, status draft/submitted/acknowledged/closed, overall_score, overall_comment, staff_comment self-comment acknowledgment, submitted_at, acknowledged_at, scores collection
- **AppraisalScore** appraisal_id, criterion_id, score 1-5, comment
- **DisciplinaryRecord** staff_id, incident_date, incident_type misconduct/absenteeism/negligence, title, description, severity low/medium/high/critical, action_taken verbal warning/written warning/suspension, status open/under_review/resolved/closed/appealed, reported_by_user_id, assigned_to, visibility hr_only/head_only/admin_only - restricted access based on role, is_confidential true, resolution_date, resolution_notes

**Services:**
- **HRService.CreateStaffAsync** generates staff_number via count, creates staff
- **CreateContractAsync** generates contract_number, calculates daysToExpiry, status active
- **GetExpiringContractsAsync** 30 days ahead for reminders
- **CreateLeaveRequestAsync** calculates working days excluding weekends, checks balance remaining vs requested, throws insufficient balance
- **ApproveLeaveAsync** approver_user_id, status approved/rejected, updates entitlement used_days and remaining_days
- **GetLeaveBalanceAsync** staffId academicYear returns entitlements list entitled/carried/used/remaining total
- **GetLeaveCalendarAsync** from/to date range returns per date leavesOnDate countOnLeave
- **CreateAppraisalCycleAsync** with criteria
- **CreateDisciplinaryAsync** visibility hr_only/head_only/admin_only restricted access: controller filters by visibility based on user role, HR manager sees hr_only, head sees head_only, admin sees all, teacher sees none unless not confidential
- **GetHeadcountReportAsync** totalActive, totalOnLeave, totalTerminated, byDepartment dict, byEmploymentType dict, monthlyTrend last 12 months headcount joined left
- **ExportPayrollReadyAsync** - payroll-ready file, NOT calculated deductions: employeeCode, nationalId, fullName, department, designation, employmentType, hireDate, basicSalary, allowances housing/transport/COLA, overtimeHours/Rate, leaveDaysTaken, unpaidLeaveDays, deductions loans/union, bank name/account/branch, grossTotal basic+allowances+overtime, notes PAYE/NSSA to be calculated by payroll product. Two formats: belina (CSV) and pastel. FileName payroll_ready_2026_2_20260803HHmmss.csv/belina, fileUrl /exports/payroll/..., totalEmployees, totalGross, currency, exportedAt, exportedBy. Audit log.

**Endpoints:**
- POST/GET /api/hr/staff, POST /api/hr/contracts, GET /api/hr/contracts/expiring?daysAhead=30, POST /api/hr/leave-requests, POST /api/hr/leave-requests/{id}/approve, GET /api/hr/leave-balances/{staffId}?academicYear, GET /api/hr/leave-calendar?from&to, POST /api/hr/appraisal-cycles, POST /api/hr/disciplinary, GET /api/hr/reports/headcount, POST /api/hr/payroll-export

**Frontend (to be built):** Staff list, staff detail with tabs contracts/qualifications/documents/leave balance, leave request form with balance check, leave calendar, appraisal cycles configurable criteria weight maxScore.

**Migration V15_HR.sql:** departments, staff, contracts, qualifications, staff_documents, leave_types seeded Annual 22, Sick 90, Maternity 98, leave_entitlements, leave_requests, appraisal_cycles, criteria, appraisals, scores, disciplinary_records, indexes tenant_id leading.

## Payroll Analysis — For Acceptance Before Code

**File:** `Docs/payroll-regulatory-analysis.md` - 6 sections:

1. **Statutory obligations varying by jurisdiction:** ZW PAYE progressive bands change every budget + AIDS levy 3% of PAYE, taxable includes basic+allowances+bonus+benefits, non-taxable some, filing monthly 10th. NSSA pension 4.5%+4.5% ceiling $700-1000 changes via SI, Workman's Comp 1-2% employer, ZIMDEF 1% employer. NEC Education minimum wages per grade, leave 22 days 5-day week 30 days 6-day week 90 days sick 98 days maternity 13th cheque some NEC, overtime 1.5x normal 2x Sunday. Medical aid 50/50. Union dues. SA PAYE different bands, UIF 1%+1% ceiling R17712, SDL 1%, medical tax credits, etc. ZM NAPSA 5%+5% ceiling K1149.

2. **What must be configurable if we build:** Per jurisdiction per effective date: PAYE bands array from/to/rate, AIDS levy rate, pension rate employee/employer ceiling, Workman's Comp rate, tax credit formulas, allowance types taxability, minimum wage per NEC grade overtime multipliers, 13th cheque mandatory bool, medical aid split, currency, pay frequency, per employee tax status resident/non-resident disabled over 55 pension opt-in medical aid dependents union, per payroll run gross to net formula version proration.

3. **What liability we take on by calculating deductions:** Tax liability under-remit PAYE school penalty 10%+interest may seek damages from us, labour law leave entitlement wrong employee claim underpayment, NSSA under-remittance affects pension, directors personally liable for PAYE not remitted, professional indemnity needed chartered accountant and labour lawyer retainer, versioning audit 7 years, jurisdiction creep SA ZM.

4. **Recommendation:** **DO NOT build in-house payroll calculation for V1.** Build HR records + payroll-ready export (CSV+PDF) with gross inputs no PAYE/NSSA/ZIMDEF, two templates Belina/Pastel, audit log. Terms "Payroll export is payroll-ready file for import into your payroll product or for your accountant. LearnCloud does not calculate statutory deductions."

   - Option A Build full payroll: pros control no dependency can charge higher, cons 50+ configurable fields, monitor SI weekly, need accountant/lawyer retainer insurance, liability high, build 3-6 months ZW alone 6-12 months ZW+SA+ZM plus 20% dev time forever maintenance, one operator highest regulatory risk.

   - Option B Integrate with existing payroll product: Belina/Paymaster/Pastel/Sage, LearnCloud exports gross inputs CSV/API, payroll product calculates statutory deductions using its audited insured engine, exports back net pay payslip PDFs. Pros liability stays with established vendor, focus on HR low risk, schools keep existing product. Cons dependency, school pays two products, mapping complexity, need multiple integrations.

   - Option C Export payroll-ready file (Recommended V1): Pros zero statutory liability gross addition low risk, fast build HR 4-6 weeks export 1 week, schools use existing product/accountant, high value single source truth for staff records, can charge for HR without payroll risk, leaves door open to integrate later. Cons not full payroll, some schools want all-in-one may choose competitor.

   - Hybrid roadmap: V1 HR + export, V2 integration Belina via CSV auto-import/API, V3 consider in-house payroll calculation for ZW only with jurisdiction flag effective-dated bands and explicit disclaimers accountant sign-off per tenant but still high risk.

5. **What must be configurable if we ever build:** Jurisdiction ZW/SA/ZM, NEC Education/General, PAYE bands array with effective_from/to, AIDS levy rate, pension rate employee/employer ceiling, Workman's Comp rate, tax credits, allowance types taxability, min wage per grade, overtime multipliers, 13th cheque mandatory, medical aid split, currency, pay frequency, employee tax status, pension opt-in, medical aid dependents, union, proration rules.

6. **Acceptance required before code:** Checkbox accept recommendation build HR + export for V1 not payroll calculation, or accept risk instruct to build payroll calculation for ZW only with disclaimers, or instruct integrate with specific payroll product for V2.

**Status:** Awaiting your acceptance of analysis before building payroll calculation code. HR module built, payroll export built, payroll calculation NOT built per analysis recommendation.

