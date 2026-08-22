# LearnCloud School Setup Wizard — V1
**Goal:** Newly registered school from empty tenant to usable system in under 30 min unaided, 10 min accepting defaults.
**HQ Bulawayo**

## Steps (9 per spec)

1. **School Profile** — name, type (primary/secondary/combined/early_years), address, city (Bulawayo default), country ZW, contact email/phone, timezone Africa/Harare, base currency USD + ZWG toggle, learner band. **Required, cannot skip.**
2. **Branding** — logo upload 5MB PNG/JPG/SVG (per FR-SR07), primary colour #0F153A for printed docs, secondary #5F3F96. Preview. Skippable.
3. **Academic Year** — name 2026, start 2026-01-10, end 2026-12-05, isCurrent. **Required, cannot skip.**
4. **Terms** — count default 3, date ranges default ZW: Term1 Jan10-Apr15, Term2 May10-Aug10, Term3 Sep10-Dec5. Validation no overlap. Skippable.
5. **Classes and Streams** — bulk entry Form 1: A,B,C. Input: gradeName, gradeCode, streams [{name, capacity}]. Creates Grade and Stream entities with TenantId discriminator. Skippable.
6. **Subjects** — starter list accept/edit. Defaults from `SubjectDefaults`: PrimaryStarter 8 subjects, SecondaryStarter 18 subjects (Math, Eng, Combined Science, History, Geo, Agriculture, Compsci...), Combined = union. Checkbox selected core true. Skippable.
7. **Departments and Staff Roles** — departments default list: Administration, Sciences, Commercials, Arts/Languages, Maths, Practical, Sports, Guidance. Custom roles: HOD, Senior Teacher, Lab Tech... Skippable.
8. **Grading Scale** — bands symbol, description, range. Defaults: ZIMSEC Secondary A 80-100 Excellent #2E7D32, B 70-79 Very Good, C 60-69 Good, D 50-59 Fair #B7791F, E 40-49 Pass, U 0-39 Fail #C62828. Validation no overlap, cover 0-100. Skippable.
9. **Preferences** — week start Monday/Sunday, attendance mode daily/per_period, invoice numbering prefix INV, nextNumber 1, format {prefix}-{year}-{number:5}, enableParentPortal true, enableSms false. Skippable.

## Requirements Met

- **Progress saved after every step:** `WizardProgress` table tenant_id unique, steps_status_json {"1":"completed",...}, data_json FullWizardDataDto, last_saved_at. Backend `SaveStepAsync` updates after each PUT, frontend also localStorage `wizard_progress`, `wizard_formData`, `wizard_currentStep` for offline resume. GET /progress returns resume data.
- **Close browser and resume:** On load, fetch /progress + /data, merge into formData, set currentStep to progress.currentStep. localStorage fallback if offline.
- **Every step skippable except 1 and 3:** `SkipStepAsync` throws if step==1||3. Frontend shows Skip button only if not required.
- **Sensible defaults everywhere:** AcademicDefaults.GetCurrentAcademicYear (Jan10-Dec5), GetThreeTerms (ZW), SubjectDefaults by school type, GradingScaleDefaults ZIMSEC, Branding #0F153A #5F3F96, timezone Africa/Harare, currency USD, weekStart Monday, attendance daily, invoice INV. School can accept through in 10 min (click Save & Continue 9 times).
- **Completion summary with counts:** `GetSummaryAsync` counts Grades, Streams, Subjects, Terms etc from actual tables (tenant-filtered), lists created entities, total completed/skipped.
- **On finish, mark tenant setup complete and route to dashboard with next 3 actions:** POST /complete sets IsCompleted true, CompletedAt, TenantSettings FeaturesJson setupComplete true, returns summary with NextActions: Import learners /students/import, Add staff /staff, Set fee structures /fees/structures. Frontend routes #dashboard.
- **Same screens reachable afterwards from settings:** Form components SchoolProfileForm, BrandingForm, etc. are reusable React components—not one-time code path. Backend Save methods SaveSchoolProfileAsync etc. are also used by SettingsController (same logic). Wizard is not one-time.

## Backend Endpoints

- `GET /api/setup/progress` - resume
- `GET /api/setup/data` - full data JSON
- `PUT /api/setup/step/{step}` body = step DTO, validates via FluentValidation, creates domain entities (Grade, Stream, Subject...), updates WizardProgress, last_saved_at
- `POST /api/setup/skip/{step}` - skip if allowed
- `GET /api/setup/summary` - counts
- `POST /api/setup/complete` - mark complete, return summary with next actions
- `POST /api/setup/branding/logo` - multipart file 5MB, saves to wwwroot/uploads/{tenantId}/logo_{timestamp}.png, returns url, updates Tenant.LogoUrl + TenantSettings
- `GET /api/setup/defaults/subjects?schoolType=secondary` - starter list
- `GET /api/setup/defaults/grading?preference=secondary` - bands
- `GET /api/setup/defaults/departments` - departments + roles
- `GET /api/setup/defaults/academic` - academic year, terms, timezone, currency, colors, weekStart, attendanceMode, invoice defaults (sensible)

All tenant-scoped, require auth, except defaults allow anonymous for preview.

## React Wizard

`Frontend/SetupWizard.jsx` - full wizard with step navigation left sidebar status todo/completed/skipped, top progress bar 9 segments, main form per step reusable.

- Progress saved after every step: fetch PUT /step/{id}, localStorage set wizard_last_saved, wizard_progress
- Resume: useWizardProgress hook fetches /progress + /data on mount, merges into formData
- Skip: POST /skip/{id}
- Completion summary: shows counts grid, created entities list, next 3 actions cards with icons, buttons Go to Dashboard → and Open Settings
- Sensible defaults prefilled, school can accept through.

Same screens: SchoolProfileForm etc. exported and used in Settings pages - e.g. Settings → School Profile uses <SchoolProfileForm>.

Mobile-first: sidebar collapses to top stepper on <1024, touch targets 44px.

## Seed Defaults

`Seed/SubjectDefaults.cs` - PrimaryStarter, SecondaryStarter, CombinedStarter, GetBySchoolType
`GradingScaleDefaults` - ZimsecPrimary, ZimsecSecondary, CompetencyBased, colors #2E7D32 success, #B7791F warning, #C62828 danger - greyscale legible
`DepartmentDefaults` - DefaultDepartments 8, CustomRoles 6
`AcademicDefaults` - current year Jan10-Dec5, 3 terms, 2 terms option, timezone Africa/Harare

## How to Run

Backend: `dotnet add package FluentValidation` etc., migration `WizardProgress.sql`, register `IWizardService` scoped, AddControllers.

Frontend: import SetupWizard.jsx into React Router `/setup` route, requires access_token in localStorage, API_BASE = "/api/setup".

Demo without backend: open `LearnCloud_Setup_Wizard.html` - uses localStorage only, no API, showcases 9 steps, resume, skip, summary, next actions.

## Next Actions After Wizard

Dashboard shows 3 suggested actions (import learners, add staff, set fee structures) - links to /students/import (CSV bulk FR-SR05), /staff, /fees/structures.

Settings reuse: /settings/profile -> SchoolProfileForm, /settings/branding -> BrandingForm, /settings/academic -> AcademicYearForm + TermsForm, /settings/classes -> ClassesStreamsForm, etc. Same validation and save logic, just different route, so wizard is not one-time code path.

