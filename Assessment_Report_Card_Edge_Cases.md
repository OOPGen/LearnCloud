# Assessment & Report Cards — Edge Cases Contract (Review Before Code)
**HQ Bulawayo | V1 | All grading and aggregation logic will live in one service: AssessmentCalculationService with thorough unit tests**

This is the **policy decision gate** you asked for. I list every edge case I will handle, and mark **[POLICY DECISION NEEDED]** where correct behaviour depends on your school policy, not code.

---

## 1. ASSESSMENT TYPES & WEIGHTING

**Model:** AssessmentType per tenant (test, assignment, practical, examination) with weighting toward term mark, e.g. Test 20%, Assignment 20%, Practical 10%, Examination 50% = 100%

**Edge Cases:**
1. Weights don't sum to 100% (e.g. 90% or 110%) — Should we **enforce sum=100%** and block save, or **allow and normalize** proportionally? Normalizing hides config error but is forgiving.
2. Two assessments same type (e.g. two Tests in term) — Should weight be **split equally within type** (Test type 20% → each test 10%) or **each assessment has its own weight field overriding type weight**?
3. Assessment type deleted after assessments created — What happens to existing assessments? Keep type string snapshot or block deletion if in use?
4. Weight 0% type — Allow? Could be for practice not counting.

**[POLICY DECISION NEEDED] Q1:** Enforce weights sum to 100% exactly, or allow and normalize? Default I propose enforce 100% with validation error.

**[POLICY DECISION NEEDED] Q2:** Within same type, how to distribute type weight? Proposal: Average within type first, then apply type weight. Example: 2 tests 60,80 → average 70 → 70 * 20% weight = 14 points toward term. Alternative: each test individually weighted.

---

## 2. ASSESSMENTS (Subject, Class, Term, Date, Max Mark)

**Edge Cases:**
5. Max mark 0 or negative — Block, max must be >0
6. Max mark 0 should not cause division by zero when computing percentage — guard
7. Assessment date outside term date range — Allow but flag warning, or block?
8. Assessment created for class but subject not offered to that class (grade_subjects) — Block or allow with warning?
9. Assessment duplicate (same subject, class, term, date, name) — Idempotency check
10. Mid-term timetable change — assessment linked to class that no longer exists? Keep historical?

**[POLICY DECISION NEEDED] Q3:** Assessment date outside term: block or allow with warning "Date outside Term 1"? Proposal allow with warning, since exams may be just after term.

---

## 3. MARKS RECORDED PER LEARNER, ABSENT DISTINCT FROM ZERO

**Model:** StudentMark: student_id, assessment_id, score decimal? nullable, is_absent bool, status draft/submitted

**Edge Cases:**
11. **Absent vs zero:** Student absent for assessment — score null, is_absent true. Distinct from score 0 (attempted and scored 0). This is critical.
12. Score > max mark (e.g. 105/100 extra credit) — Should we **reject** or **allow with warning**? Some schools allow bonus.
13. Negative marks — Always reject
14. Decimal marks (e.g. 75.5) — Allow? Precision 0.5? V1 allow 2 decimals.
15. Mark entered for learner not in class (enrolment not current or stream mismatch) — Block, must be enrolled in assessment's class.
16. Mark entered twice (duplicate student_id + assessment_id) — Upsert, last wins with audit.
17. Absent learner later provides medical, should be changed to excused? Allow edit before submit, after submit requires unlock.

**[POLICY DECISION NEEDED] Q4:** For term calculation, how to treat **absent**?
- Option A: **Absent = 0** — counts as 0 toward term average (punitive, common for exams)
- Option B: **Absent = excluded from average** — term average computed only on attempted assessments, absent ignored (forgiving, common for homework)
- Option C: **Absent = excluded but flagged, and if all assessments absent → term mark = Absent not 0**
- Proposal: Make configurable per assessment type: Examination absent=0, Assignment absent=excluded. Need your call.

**My default proposal:** Absent distinct from zero in storage, but for term aggregation: if is_absent true → **exclude from average** unless assessment type is Examination, then treat as 0. This needs policy.

---

## 4. GRADING SCALES — Bands with Symbol, Description, Lower/Upper Bound

**Model:** GradingScale per tenant: bands [{symbol A, description Excellent, lower 80, upper 100, color #2E7D32}, ...]. Support letter grades (A-F), symbol grades (1-9), percentage bands.

**Edge Cases:**
18. Overlapping bands: A 80-100, B 70-90 overlap 80-90 — Which grade wins? Should block overlapping on save.
19. Gaps: A 80-100, B 60-69, gap 70-79 no grade — What to assign if mark 75? Should block gaps, require coverage 0-100.
20. Lower bound > upper bound misconfig — Block
21. Upper bound 100 inclusive, lower 0 inclusive — Clarify inclusive both ends, but avoid overlap by using lower inclusive, upper inclusive with no overlap rule (or upper exclusive except top band).
22. Multiple grading scales per tenant? V1 single default per tenant, but some schools want different scale per grade (primary vs secondary). Should we allow per grade override?
23. Rounding before grading: Mark 79.5 with band B 70-79, A 80-100 — Does 79.5 round to 80 → A, or stays B? Need rounding rule.

**[POLICY DECISION NEEDED] Q5:** Grading rounding before band lookup: **Round percentage to nearest integer** then grade, or **use exact decimal** with band bounds decimal? Example 79.6% → B or A? Proposal Round to 2 decimals then grade, no integer rounding, so 79.6 stays B if B max 79.99.

**[POLICY DECISION NEEDED] Q6:** Single grading scale per tenant or per grade? V1 proposal single per tenant to keep simple, but allow per grade in V2. OK?

---

## 5. TERM RESULTS — Weighted Assessments, Then Term Aggregate

**Model:** Per subject per learner: term_subject_mark = Σ(assessment_percentage * type_weight * assessment_weight_within_type). Then term aggregate = average or aggregate of subject marks.

**Edge Cases:**
24. Subject with no assessments in term — Term subject mark blank or 0? Should subject be hidden on report or show "No assessment"?
25. Subject with only some assessments done, others not yet entered — Compute partial average or wait until all entered?
26. Learner does not take subject (optional subjects): e.g. 8 subjects offered, learner takes 6. Should aggregate be average of taken subjects only, or include missing as 0? Must be average of taken only.
27. Learners who joined mid-term: has only 2 of 5 assessments (joined late). How to compute term result? Options:
    - **Pro-rate:** Average of attempted assessments only (forgiving)
    - **Count missing as 0** (punitive)
    - **Exclude from position until next term**
28. Different max marks normalized: Assessment max 20, another max 100 — Must normalize to percentage before weighting: `percentage = score/max *100`, then apply weight. If not normalized, 20/20 =20 vs 80/100=80 unfair. Always normalize.
29. Weighting within type: if type weight 20% and 2 tests, each test weight? See Q2.

**[POLICY DECISION NEEDED] Q7:** For learner **does not take subject** (optional), should subject be **excluded from aggregate** (average of taken only) or **shown but not counted**? Proposal exclude from aggregate, show only taken subjects on report. Prevents penalizing learners taking fewer subjects.

**[POLICY DECISION NEEDED] Q8:** For **mid-term joiner** missing assessments, compute term mark as **average of attempted only** (pro-rate) or **count missing as 0**? Proposal average of attempted only, but flag "Joined mid-term" on report and exclude from class position if joined after 50% term.

**[POLICY DECISION NEEDED] Q9:** Term aggregate: Is aggregate **average of subject percentages** (sum % / count) or **sum of weighted subject marks**? Most ZW schools use average. Also, should core subjects have more weight than optional? Or all equal? Proposal all equal average.

---

## 6. CLASS POSITION WITH TIES, CONFIGURABLE BY AGGREGATE OR AVERAGE

**Model:** Class position computed per class per term, ties handled correctly, configurable choice whether position is by aggregate or average.

**Edge Cases:**
30. Tie: Two learners both 85% aggregate — What ranking method?
    - **Standard Competition Ranking ("1224"):** 1,2,2,4 — next rank skips (common in ZW schools)
    - **Dense Ranking ("1223"):** 1,2,2,3 — next rank does not skip
    - **Ordinal Ranking:** 1,2,3,4 — ties broken arbitrarily by name (unfair)
31. Position by aggregate vs average: If learner A takes 8 subjects aggregate 600, average 75; learner B takes 6 subjects aggregate 480, average 80. By aggregate A wins, by average B wins. Which is fairer?
32. Learners with different subject sets comparing aggregate is unfair — should position be by average always, even if config says aggregate?
33. Learners who joined mid-term: include in position or exclude? If included with prorated average, may rank high with fewer assessments.
34. Learners who were absent for all assessments: aggregate 0, should they be last or excluded from position?
35. Multiple streams same grade: position per stream or per grade? Spec says per class, so stream only? Or grade-wide? Need clarity.

**[POLICY DECISION NEEDED] Q10:** Tie handling: **1224 (standard competition)** is most common in Zim. Confirm?

**[POLICY DECISION NEEDED] Q11:** Position by **aggregate or average** — default? Proposal **average**, because aggregate penalizes learners taking fewer optional subjects. But make configurable per tenant: setting `position_by = aggregate|average`. Which default you want? Proposal default average.

**[POLICY DECISION NEEDED] Q12:** Include **mid-term joiners** in position? Proposal exclude if enrolment_date > 50% term elapsed, show position as "-" with note "Joined mid-term".

---

## 7. REPORT CARD TEMPLATE, PDF, STATES

**Model:** Template configurable: school logo/details, learner details, subjects with marks, grades, position, teacher comment, attendance summary, overall comment class teacher and head, next term start date.

**Edge Cases:**
36. School logo missing — show placeholder initials or blank?
37. Learner photo missing — initials circle
38. Subjects with no marks (learner doesn't take) — hide row or show blank? Proposal hide if learner doesn't take.
39. Teacher comment missing — show blank or "No comment"?
40. Attendance summary: daily vs per_period mode, if attendance data missing for term, show blank or 0%? Show "N/A"
41. Overall comment class teacher and head: required before publish or optional?
42. Next term start date: from term config, if not set show TBD
43. Draft/approved/published states: Only published visible to parents. Who can approve? Head Teacher only, or Deputy too? Can teacher edit after approved? Should auto-unpublish to draft if marks unlocked after publish?
44. Bulk PDF generation for class as background job: If one learner fails (e.g. missing subject), continue others and report failed list, not fail whole batch
45. PDF prints correctly A4 greyscale: Must not rely on color alone, grades have symbol + description, status chips have border + icon
46. Bulk generation idempotent: If report already generated for learner+term, skip or regenerate?
47. Published card visible to parents: What about students? Upper primary/secondary should see own? Proposal yes, student sees own published.

**[POLICY DECISION NEEDED] Q13:** Approval workflow: **Teacher submits → Head approves → Published** or **Teacher submits → Head approves → Head publishes** separate steps? Proposal two steps: submit locks marks, approve unlocks for report generation, publish makes visible to parents. Who can publish? Head only? Or School Admin too?

**[POLICY DECISION NEEDED] Q14:** If report card published then marks changed (unlock), should it **auto-unpublish to draft** to prevent parents seeing outdated? Proposal yes, auto-unpublish and require re-approve.

**[POLICY DECISION NEEDED] Q15:** Class position: per **stream** or per **grade**? Example Grade 5 has Blue 40 learners and Green 40 learners, position top 40 or top 80? Spec says class, so stream. Confirm position per stream (class) not per grade?

---

## 8. PERMISSION SCOPING

**Marks entry:** Permission `marks.enter` scoped to teaching teacher — only teacher assigned to class AND teaching subject in class via timetable. Already enforced in Teacher Portal via `TeacherAuthorizationService.EnsureTeachingSubjectInClass`

**Approval:** Permission `marks.approve` + `marks.unlock` separate — Head Teacher, Deputy Head. Teacher cannot approve own marks.

**Report generation:** `reportCards.generate` — Head, Deputy, School Admin. Publish: `reportCards.publish` — Head only.

**Parent view:** Only published cards visible via `reportCards.read` with OWN_CHILD scope (guardian_student_links). Student view OWN.

**Edge:** What if HOD wants to approve for department subjects, not whole class? V1 allow any Head/Deputy to approve any subject, V2 per department.

---

## 9. SUMMARY OF POLICY QUESTIONS NEEDING YOUR CALL

Please answer these 15, even short yes/no, so I don't build wrong fairness logic:

**Q1:** Assessment type weights must sum to 100%? Enforce or normalize?

**Q2:** Two tests same type: split type weight equally within type, or each assessment has individual weight?

**Q3:** Assessment date outside term: block or allow with warning?

**Q4:** Absent handling for term result: Absent = 0, or Absent = excluded from average (except exam=0), or configurable per type?

**Q5:** Grading rounding: Round percentage to 2 decimals then lookup band, or round to integer?

**Q6:** Single grading scale per tenant or allow per grade (primary vs secondary different)?

**Q7:** Learner does not take optional subject: Exclude from aggregate average (show only taken) or show blank and count as 0?

**Q8:** Mid-term joiner missing assessments: Average of attempted only (pro-rate) + flag "Joined mid-term" and exclude from position if joined after 50% term? Or count missing as 0?

**Q9:** Term aggregate: Average of subject percentages (equal weight) or sum aggregate? Core vs optional weight different?

**Q10:** Tie handling: 1224 standard competition ranking (1,2,2,4) confirm?

**Q11:** Position by aggregate or average default? Proposal average. Configurable?

**Q12:** Include mid-term joiners in position? Proposal exclude if >50% term elapsed.

**Q13:** Approval workflow: Teacher submit → Head approve + Head publish two steps or submit → approve → publish three steps? Who can publish?

**Q14:** If published report then marks unlocked/changed, auto-unpublish to draft?

**Q15:** Class position per stream (40) or per grade (80)? Per class = stream.

Once you answer these, I will build:

- Entities: AssessmentType, Assessment, StudentMark (absent distinct from zero), GradingScale, GradingBand
- TermResult, SubjectResult
- ReportCardTemplate, ReportCard (draft/approved/published), ReportCardSubjectLine
- Migration
- AssessmentCalculationService (single arithmetic source) with unit tests: ties (1224), absent, subjects not taken, mid-term joiner, aggregate vs average, rounding
- Services: AssessmentTypeService, AssessmentService, MarksService (permission-scoped), GradingService, TermResultService, PositionService, ReportCardService (PDF individually + bulk background job idempotent)
- Controllers: /api/assessment-types, /api/assessments, /api/marks, /api/grading-scales, /api/term-results, /api/report-cards (generate, approve, publish, bulk)
- Frontend: Marks entry grid reused (keyboard navigable, autosave, validation max, draft + submit locks), Report card preview A4 greyscale, bulk generate progress
- Permissions: marks.enter OWN_CLASS+OWN_SUBJECT, marks.approve, reportCards.generate/publish

Ready for your policy calls.

