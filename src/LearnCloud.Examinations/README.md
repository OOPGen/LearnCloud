# Examinations Full Module — Extend Assessment into Full Examination Module

## Model

**Examination sessions grouping assessments across subjects for a term, with timetable, venues and invigilator assignment:**
- `ExaminationSession` id tenant_id name Term 2 2026 Final Exams academic_year_id term_id session_type mid_term/final/mock/supplementary status draft/scheduled/ongoing/completed/archived start_date end_date
- `ExaminationSessionAssessment` session_id assessment_id subject_id grade_id stream_id (null = all streams)
- `ExaminationSlot` session_id subject_id grade_id stream_id assessment_id exam_date start_time end_time venue_id venue_name snapshot invigilator_staff_id invigilator_name status scheduled/completed/cancelled, unique (tenant, session, exam_date, start_time, grade, stream) prevents class double-booked exam, index tenant date teacher

**Weighted composite results: continuous assessment against examination, with weights configurable per subject and per level:**
- `CompositeWeighting` tenant academic_year term grade_id null=all grades subject_id null=all subjects continuous_assessment_weight 30 examination_weight 70 is_active, unique (tenant, year, term, grade, subject)
- Calculation: CA avg = average of CA assessments percentages, Exam avg = average of exam assessments, composite = CA avg * CA_weight/100 + Exam avg * Exam_weight/100, handles joined mid-year missing CA pro-rated returns exam only

**Merit lists and rankings by class, stream, year group and subject, with configurable tie rule:**
- `MeritListConfig` academic_year term scope class/stream/year_group/subject rankingMethod 1224 standard competition (1,2,2,4) or 1223 dense (1,2,2,3) positionBy average or aggregate topN 10 includeTies
- Service `CalculatePositions` sorted by chosen metric descending, rank = position for 1224 else rank+1 for dense, same score same rank isTie true, mid-year joiners excluded from position with reason Joined mid-year if enrolment >50% term

**Promotion: rules based on aggregate, subject minimums and attendance; decision screen showing recommendations with reason; manual override with required justification; bulk promotion job creates next-year enrolments:**
- `PromotionRule` academic_year from_grade to_grade minimum_aggregate 50 minimum_average 50 minimum_attendance 75 max_failed_subjects 2 required_subjects_json list subject_ids must pass minimum_subject_score 40 description is_active
- `PromotionDecision` student_id from_grade to_grade from_academic_year to_academic_year from_term recommended_action promoted/repeat/conditional/graduated final_action after manual override is_manual_override bool override_justification required if override decided_by/at reason aggregate 45% < minimum 50%, failed Math 30% <40%, attendance 80% >=75% aggregate average failedCount attendance status pending/approved/completed next_enrolment_id created by bulk job
- `PromotionBatch` batch_number PROMO-2026-00001 from_year to_year from_grade to_grade status pending/running/completed/failed total promoted/repeat/conditional/failed result_json started/completed
- Bulk promotion job: for each student in from_grade from_year, evaluate rule via `EvaluatePromotion`, create decision, if promoted/conditional create next-year enrolment with grade to_grade academic_year to_year term 1 enrolment_type promoted, is_current false? Actually next year is_current true for new year, previous is_current false? Transaction

**Transcripts covering learner's full academic history across years:**
- `Transcript` student_id transcript_number TR-2026-00001 generated_at by user data_json full history pdf_url
- Service builds transcript from term results across years: for each academic year term aggregate average rank subject results, includes prior school results if transferred with prior results (priorResults list not included in current aggregate but shown in transcript separate section)

**Historical analysis: subject performance trends, class comparisons, learners whose performance dropped between terms:**
- `HistoricalAnalysisCache` year term grade subject analysis_type subject_trend/class_comparison/performance_drop data_json calculated_at
- Service `DetectPerformanceDrops` histories grouped by student ordered year term, diff current - previous, if diff <= -threshold (10%) flagged drop reason Dropped X% from prev to curr
- Subject trends: average per subject over terms, class comparisons: average per class per term

**Mark moderation: approval chain teacher to HOD to head, locked state after approval and audited reason for any change afterwards:**
- `MarkModeration` assessment_id student_id subject_id current_stage teacher/hod/head status draft/submitted_by_teacher/approved_by_hod/approved_by_head/locked is_locked locked_at/by teacher_user_id submitted_at hod_user_id approved_at head_user_id approved_at change_reason audited reason for any change after locked previous_score_json new_score_json
- Flow: Teacher enters marks draft, submits → submitted_by_teacher, HOD approves → approved_by_hod, Head approves → approved_by_head locked true locked_at, any change after locked requires unlock with change_reason required justification, audited, previous/new score JSON

**Certificates and printable award lists:**
- `Certificate` student_id academic_year term certificate_type merit/distinction/attendance/improvement title e.g. Top 3 in Form 1A description data_json pdf_url issued_at by user
- Printable award lists: merit list top N per scope, certificates bulk PDF background job

**All calculations in tested services:**
- `ExaminationCalculationService` single source: GetGradeFromScale, CalculateCompositeResult with absent handling, CalculateTermResult with exclude subjects not taken, CalculatePositions with ties 1224/1223 and average vs aggregate and mid-year joiner exclusion, EvaluatePromotion aggregate/subject minimums/attendance, DetectPerformanceDrops, CalculateWithTransferResults
- Unit tests `Tests/ExaminationCalculationTests.cs` covering: grading bands, composite CA 30 Exam 70, joined mid-year missing CA pro-rated, absent for exam counts as zero vs excluded, term results excludes subjects not taken, ties 1224 and dense, aggregate vs average different subject set, promotion aggregate subject minimums attendance, historical drop detection, edge cases different subject set, joined mid-year average of attempted only, absent for exam treated as zero, transferred with prior results

**Edge cases covered:**

- Learner who takes different subject set: exclude not taken from aggregate average, not count as 0, position by average fairer than aggregate
- Joined mid-year: average of attempted only, flag isMidYearJoiner, enrolmentDate vs termStart, exclude from position if >50% term elapsed, transcript notes Joined mid-year
- Absent for examination: absent distinct from zero, is_absent true, score null, for term result absent handling configurable per type: ExcludeExceptExamAsZero => CA absent excluded, exam absent 0, else ExcludeAll => absent excluded
- Transferred from another school with prior results: priorResults list stored separately, transcript shows prior school results, current term aggregate excludes prior unless includePriorInAggregate true, promotion may consider prior? V1 not, but transcript includes

**Permissions:**

- Marks entry permission-scoped to teaching teacher: `marks.enter` OWN_CLASS+OWN_SUBJECT via TeacherAuthorizationService.IsTeachingSubjectInClass, approval separate permissions `marks.approve` HOD, `marks.approve` head for final lock, after locked requires `marks.unlock` with change reason
- Promotion: `promotion.rules.manage`, `promotion.decision.view`, `promotion.decision.override` requires justification, `promotion.batch.create`
- Transcript: `transcripts.read` OWN_CHILD for parent, OWN for student, `transcripts.generate` for admin
- Certificates: `certificates.generate`, `certificates.print`

**Endpoints (to be implemented):**

- POST /api/examination-sessions, GET list, POST slots, GET slots, POST composite-weightings, GET merit-list?scope&positionBy&tieRule, POST promotion-rules, POST promotion-evaluate, POST promotion-override, POST promotion-batch bulk, GET transcripts/{studentId}, POST transcripts/generate, GET historical-analysis/performance-drops, POST mark-moderation/submit/approve/unlock with change reason, POST certificates/generate bulk PDF job

**Frontend (to be implemented):**

- Examination session list, timetable grid with venues invigilators, composite weighting config per subject/level, merit lists ranking with tie badges, promotion decision screen recommendations with reason and manual override justification input required, bulk promotion job progress, transcripts view full history PDF, historical analysis charts subject trends class comparisons drops flagged, moderation chain UI teacher submit HOD approve Head approve locked badge, certificates printable award lists A4 greyscale

**Migrations:** V8_Examinations.sql with all tables, tenant_id leading indexes, unique constraints, seed default composite weighting CA 30 Exam 70

