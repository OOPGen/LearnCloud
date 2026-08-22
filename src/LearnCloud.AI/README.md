# AI-Assisted Features — Priority Order: Report Comments, Attendance Anomaly, At-Risk

**Priority 1 is report card comment drafting — saves teacher hours per term, teacher always reviews and edits before saving, nothing written automatically**

## Entities

- **AIProviderSettings** tenant_id, provider_name RuleBased/OpenAI/Claude/Gemini, is_active/default, config_json {apiKey, model, temperature}, enable_comment_drafting, enable_attendance_anomaly, enable_at_risk_detection, seeded RuleBased fallback works offline
- **ReportCommentDraft** tenant_id, student_id, academic_year/term, report_card_id nullable, input_data_json snapshot marks/attendance/subject performance, draft_comment AI generated, tone encouraging/formal/concise/detailed/neutral, length short/medium/long, edited_comment teacher reviewed, is_edited, is_saved teacher always reviews before saving nothing auto, edited_by/at, saved_by/at, provider_name, model, prompt/completion tokens, cost
- **AttendanceAnomaly** tenant_id, student_id, anomaly_type weekday_pattern/sudden_drop/consecutive_absence/low_attendance, description human readable why flagged, explanation detailed why, confidence_score 0-100, detected_at, period_from/to, data_json supporting e.g. weekday Monday missed 4/5 rate 80%, status new/acknowledged/resolved/dismissed
- **AtRiskFlag** tenant_id, student_id, risk_level low/medium/high/critical, risk_score 0-100, flag_reason summary, underlying_reasons_json array type falling_marks/declining_attendance/fee_arrears/low_attendance detail severity high/medium/low, detected_at, period_from/to, status new/in_follow_up/resolved/dismissed, assigned_to_user_id pastoral staff, follow_up_notes, resolved_at

## 1. Report Card Comment Drafting — Priority 1, Saves Hours

**Model:** Given learner's marks, attendance, subject performance, draft teacher comment configurable tone/length

**Flow:**
- Teacher opens report card, clicks Generate Draft, selects tone encouraging/formal/concise/detailed and length short/medium/long and optional custom instructions Focus on effort
- Frontend POST /api/ai/comments/generate {studentId, academicYearId, termId, reportCardId, tone, length, includeAttendance, includeSubjectDetails, customInstructions}
- Service loads student, grade, stream, marks grouped by subject, subjects, attendance records total/present/absent/late, aggregate/average/position, builds CommentGenerationInput studentName gradeName streamName subjectPerformances [{subjectId, subjectName, score, maxScore, grade, classAverage, trend up/down/stable, strengthWeakness}], attendance {total, present, absent, late, percentage, summary}, aggregate, average, position, tone, length, customInstructions
- Provider factory GetProviderAsync tenantId reads AIProviderSettings is_active default providerName switch resolves via DI, RuleBased or OpenAI swappable without touching calling code
- **RuleBasedAIProvider** fallback works offline, no API key, saves hours: strengths = subjects score >=70 top 2, weaknesses score <50 top 2, improving trend up, declining trend down, attendanceSummary excellent >=95 good >=85 fair >=75 needs attention, overall outstanding >=80 very good >=70 good >=60 fair >=50 potential >=40 needs support, tone handling formal replaces Keep working hard with Continued diligence encouraged, concise takes first 2 sentences, encouraging adds Well done and keep going, length short 2 sentences, medium 4-5 sentences, long full paragraph with position and custom instructions
- **OpenAIProvider** optional LLM integration: system prompt experienced Zimbabwean school teacher writing report comments tone length rules specific mention subjects, attendance, encouraging honest, avoid banned words, never write automatically draft for review, user prompt draft for studentName, request body model messages temperature 0.7 max_tokens length short 100 medium 200 long 300, Authorization Bearer apiKey, response choices[0].message.content, usage prompt/completion tokens, cost 0.01, fallback to rule-based if no key or failure, never log full payload with PII truncated
- Save draft: ReportCommentDraft tenant student year term reportCardId input_data_json snapshot, draft_comment AI generated, tone length provider model tokens cost, created_by user
- **Teacher always reviews and edits before saving, nothing written automatically:** Frontend shows draft, editable textarea editedComment, Review button saves as edited draft not yet to report card, Save Final button POST /comments/{draftId}/save {finalComment} updates ReportCommentDraft is_saved true saved_by/at and updates actual ReportCard classTeacherComment = finalComment only upon explicit save, audit log update_comment_via_ai_draft old draftId new finalComment truncated, requires review
- List drafts per student GET /comments/student/{studentId}

## 2. Attendance Anomaly Detection: Flag Unusual Patterns

**Model:** Flag weekday consistently missed, sudden drop, consecutive absence, low attendance with explanation why flagged

- **Weekday pattern:** Group attendance records by DayOfWeek per student, total and missed count, if total >=3 and missed/total*100 >= threshold 75% → anomaly weekday_pattern description Consistently missed Mondays explanation Student missed 4 out of 5 Mondays in last 30 days 80% miss rate on Mondays possible transport issue, confidenceScore rate, dataJson weekday missed total rate
- **Sudden drop:** Compare last 7 days vs previous 21 days, last7Rate = present+late / last7 count *100, prev21Rate same, drop = prev21Rate - last7Rate, if drop >=20% → anomaly sudden_drop description Sudden attendance drop X% explanation Attendance dropped from Y% previous 21 days Z records to X% last 7 days W records drop, possible illness family issue disengagement pastoral follow-up, confidence min(drop*2,95)
- **Consecutive absence:** Count consecutive absent/sick ordered by date, if >=3 → anomaly consecutive_absence description N consecutive days absent explanation Student absent for N consecutive days from start to end, check welfare contact guardian, confidence min(N*10,95)
- **Low attendance:** Overall percentage < threshold 85% with total >10 → flagged low_attendance

- **Endpoint:** POST /attendance-anomalies/detect {gradeId, streamId, academicYearId, termId, daysBack 30, threshold 75} → detects for filtered students, saves anomalies avoiding duplicates where same student type period status new exists, returns list
- **List:** GET /attendance-anomalies?studentId → includes studentName number anomalyType description explanation confidence detectedAt periodFrom/To status new/acknowledged

## 3. At-Risk Learner Identification: Combine Falling Marks, Declining Attendance and Fee Arrears into Flag for Pastoral Follow-Up, Always Shown with Underlying Reasons

- **Falling marks:** Current term average vs previous term average (last 20 marks vs current), drop >= marksDropThreshold 15% → reason falling_marks detail Marks dropped X% from Y% to Z%, severity high if drop >=20 else medium, riskScore +40 if >=20 else +25
- **Declining attendance:** Last 10 vs previous 10 attendance rate, drop >= attendanceDropThreshold 15% → reason declining_attendance, riskScore +35 if >=20 else +20; overall low attendance <85% → low_attendance reason
- **Fee arrears:** Invoices balanceDue sum, if >= arrearsThreshold 100 → reason fee_arrears detail Arrears $X over threshold $Y - N unpaid invoices, severity high if >=2*threshold else medium, riskScore +30 if >=2*threshold else +20
- **Risk level:** riskScore >=70 critical, >=50 high, >=30 medium, else low; flagReason = join reasons detail; underlyingReasonsJson array type detail severity score trend; avoid duplicate active flag per student
- **Endpoints:** POST /at-risk/detect {gradeId, streamId, academicYearId, termId, marksDropThreshold 15, attendanceDropThreshold 15, arrearsThreshold 100} → detects for filtered students, saves flags; GET /at-risk?studentId returns flags with underlyingReasons list always shown
- **Pastoral follow-up:** status new/in_follow_up/resolved/dismissed, assignedToUserId pastoral staff, followUpNotes, resolvedAt

## Provider Abstraction

- **IAIProvider** ProviderName, GenerateReportCommentAsync(CommentGenerationInput) → CommentGenerationResult draftComment providerName model prompt/completion tokens cost
- **RuleBasedAIProvider** fallback works offline, no API key, saves hours
- **OpenAIProvider** optional, apiKey model http client, system prompt experienced Zimbabwean teacher tone length rules specific mention subjects attendance encouraging honest, avoid banned words, never write automatically draft for review, fallback to rule-based if no key or failure
- **AIProviderFactory** GetProviderAsync(tenantId) reads AIProviderSettings is_active default providerName switch resolves via DI, swappable without touching calling code

## Permissions

- **Report comment drafting:** marks.enter? Actually reportCards.generate? For V1 TEACHER, HEAD_TEACHER, DEPUTY_HEAD can generate draft, review, save - teacher always reviews
- **Attendance anomaly:** HEAD_TEACHER, DEPUTY_HEAD, SCHOOL_ADMIN, TEACHER can detect and list
- **At-risk:** HEAD_TEACHER, DEPUTY_HEAD, SCHOOL_ADMIN, TEACHER, COUNSELOR can detect and list

## Frontend

- AIFeatures.jsx 3 tabs:
  - **Comments Priority:** form studentId year term tone encouraging/formal/concise/detailed length short/medium/long customInstructions, Generate Draft button saves hours, shows draft by provider model tone length, subject performances table score grade trend strengthWeakness, attendance total present absent percentage, draft comment AI generated teacher must review badge Requires Review Nothing Written Automatically, edited textarea teacher reviews and edits, Review button saves as edited draft not yet to report card, Save Final to Report Card button explicit teacher action with message Teacher reviewed and edited before saving nothing written automatically
  - **Attendance Anomaly:** filters grade/stream/daysBack/threshold, Detect button, list anomaly type weekday_pattern/sudden_drop/consecutive_absence, description, explanation why flagged, confidence, period, dataJson
  - **At-Risk:** filters grade marksDrop attendanceDrop arrearsThreshold, Detect button, list riskLevel critical/high/medium/low student name grade stream riskScore flagReason underlyingReasons type detail severity score trend always shown with underlying reasons for pastoral follow-up

## Tests

- Report comment drafting: tone encouraging adds Well done, formal replaces Keep working hard, short 2 sentences, medium 4-5, long includes position custom instructions, strengths/weaknesses detection
- Attendance anomaly: weekday pattern 4/5 Mondays 80% flagged, sudden drop >20% flagged, consecutive 3+ flagged
- At-risk: falling marks 15% drop flagged, declining attendance 15% flagged, arrears over threshold flagged, riskLevel critical >=70 high >=50 medium >=30 low

## Migration V15_AI.sql

- ai_provider_settings tenant provider_name RuleBased is_active default enable_comment_drafting/attendance_anomaly/at_risk
- report_comment_drafts tenant student year term report_card_id input_data_json draft_comment tone length edited_comment is_edited is_saved edited_by/at saved_by/at provider model tokens cost
- attendance_anomalies tenant student anomaly_type description explanation confidence detected period from/to data_json status acknowledged_by/at
- at_risk_flags tenant student risk_level risk_score flag_reason underlying_reasons_json detected period status assigned_to follow_up resolved_at
- Seed RuleBased default per tenant

