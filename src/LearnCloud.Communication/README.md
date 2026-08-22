# Communication Full Module — Extend Messaging into Full Communication

**Extends LearnCloud.Messaging, preserves provider abstraction ISmsProvider/IEmailProvider swappable via settings, usage caps, opt-out handling**

## What Was Added Beyond Messaging

### Announcements with Audience and Expiry Date Shown Across Portals

- `Announcement` tenant_id, title, body, audience_type all/class/role/stream/year_group, audience_filter_json gradeId/streamId/role, expiry_date nullable, status active/expired/draft/archived, priority low/normal/high/urgent, show_in_teacher_portal/parent_portal/student_portal/admin_dashboard bool, created_by_user_id, published_at
- Endpoints: POST /announcements create, GET /announcements?portal=teacher/parent/student/admin list active where expiryDate null or > now and show_in_* true, PUT update title/body/expiry/status
- Shown across portals: Teacher portal dashboard queries active teacher announcements, Parent portal home queries parent, Student portal home queries student, Admin dashboard queries admin. Expiry date respected — after expiry not shown.

### Scheduled Sending

- `ScheduledMessage` tenant_id, batch_id nullable, title, template_id, channel sms/email, audience_type, audience_filter_json, body, subject, scheduled_send_at future date, status scheduled/queued/sent/cancelled, created_by_user_id
- Endpoints: POST /scheduled create with future scheduledSendAt, GET /scheduled list order scheduledSendAt
- Background job picks scheduled where scheduledSendAt <= now and status scheduled, moves to queued and creates MessageBatch and delivery logs, same batching retry backoff rate limit path as messaging

### Saved Audience Segments

- `AudienceSegment` tenant_id, name e.g. "Grade 5 Blue Parents", description, audience_type class/stream/year_group/all_guardians/arrears_over_x/absent_today/manual/dynamic, filter_json gradeId/streamId/arrearsThreshold/date etc, is_dynamic bool true means re-evaluated at send time (arrears, absent), created_by_user_id, is_system
- Endpoints: POST /segments create, GET /segments list, POST /segments/preview {segmentId or audienceType/filterJson} returns totalGuardians totalStudents sampleRecipients
- Frontend: Save segment form name description audienceType filterJson isDynamic checkbox, list with preview button
- Dynamic: arrears_over_x dynamic re-evaluates at send time via AudienceResolver joining fee_invoices balance_due > threshold

### Template Library with Categories

- `TemplateCategory` tenant_id, name Fees/Attendance/Academic/General/Discipline, code fees/attendance/etc, description, color
- `CategorizedTemplate` tenant_id, template_id, category_id link
- Service `TemplateCategoryService`: ListCategories seeds defaults if none (Fees #B7791F, Attendance #C62828, Academic #5A94C1, General #0F153A, Discipline #3C2C59), CreateCategory, ListTemplatesWithCategory filter by categoryCode via join
- Endpoints: GET /template-categories, POST /template-categories, GET /templates/with-category?categoryCode=fees
- Frontend: Category tabs All/Fees/Attendance/Academic/General, templates grid name [channel] category badge color, merge fields list

### Two-Way SMS Handling If Provider Supports It

- `InboundSms` tenant_id, from_number sender phone, to_number provider number/shortcode, body, provider EcoCashSms/BulkSmsZw, provider_reference, received_at, matched_guardian_id via phone lookup, matched_student_id, matched_school_slug, status received/matched/unmatched/replied/archived, reply_body, replied_at
- Webhook endpoint POST /inbound-sms/webhook {From, To, Text, ProviderReference, Provider} AllowAnonymous but checks X-Tenant-Id header or domain mapping, matches guardian by phone, creates inbound_sms status matched/unmatched, creates communication_log per linked student for searchable by learner
- List inbound: GET /inbound-sms 100 recent
- Reply: POST /inbound-sms/{id}/reply {replyBody} calls ISmsProvider.SendAsync to from_number, updates replyBody repliedAt status replied
- Preserves provider abstraction: inbound handling uses same provider factory, if provider supports two-way (e.g. Twilio), webhook configured

### Event-Triggered Rule Engine (Absence N Days, Arrears Over Threshold, Report Card Published, Invoice Due in 7 Days) with Per-Tenant Config and Opt-Out

- `CommunicationRule` tenant_id, name, code absence_3_days etc, event_type absence_n_days/arrears_over_threshold/report_card_published/invoice_due_7_days, description, is_active bool, config_json {"n":3} or {"threshold":100,"currency":"USD"} or {"daysBeforeDue":7}, template_id, channel sms/email, audience_type dynamic, respect_opt_out true, respect_contact_preferences true, created_by_user_id, last_triggered_at, trigger_count
- Endpoints: GET /rules list, POST /rules create, POST /rules/{id}/trigger manual for testing {studentId, gradeId, streamId, amount, date, extraJson}
- Service `CommunicationRuleEngine`: CheckAndTriggerAsync(tenantId, eventType, TriggerRuleRequest) loads active rules where eventType match, parses config, evaluates shouldTrigger:
  - absence_n_days: count consecutive absence for student via attendance_records status Absent/Sick ordered desc, if >= n trigger
  - arrears_over_threshold: if trigger amount >= threshold
  - report_card_published: always trigger when report card published event
  - invoice_due_7_days: if trigger date daysUntilDue == daysBeforeDue
  - If shouldTrigger: log, create MessageBatch auto from rule template, audience filter studentId, respect opt-out check GuardianContactPreference optOut, create delivery logs for billing guardian, update last_triggered_at trigger_count
- Background job ProcessScheduledRulesAsync runs hourly, scans active rules, for absence_n_days scans students take 100 per tenant for demo, counts consecutive absence, triggers if >=n; similar for other types
- Per-tenant config: config_json per rule, is_active toggles, respect_opt_out always honoured (checks contact preferences before creating delivery log, skips opted out)

### Delivery Analytics by Campaign

- `CampaignAnalytics` batchId batchNumber title channel totalRecipients sent delivered failed read replied totalCost currency deliveryRate delivered/sent*100 readRate replyRate dailyStats
- Endpoint GET /analytics/campaigns?from&to: loads MessageBatch 50 recent, for each loads delivery logs counts sent delivered failed read replied totalCost sum, deliveryRate readRate, dailyStats grouped by date
- Frontend AnalyticsTab table batchNumber title channel total sent delivered failed cost delivery% 

### Per-Tenant Communication Log Searchable by Learner

- `CommunicationLog` tenant_id batchId announcementId ruleId inboundSmsId studentId searchable by learner guardianId teacher_staff_id channel sms/email/portal/announcement direction outbound/inbound recipientAddress messageBody subject status sent/delivered/failed/read/replied provider providerReference cost currency sentAt deliveredAt readAt failureReason is_opted_out_at_send
- Endpoint GET /logs?studentId&guardianId&channel&status&fromDate&toDate&searchText&page&pageSize: query tenant_id and filters, full-text search message_body recipient_address via LIKE or fulltext index, returns PagedCommunicationLogDto items total page pageSize, includes studentName guardianName via joins
- Frontend LogsTab search inputs studentId searchText, button Search, list per log studentName guardianName channel direction status

### Preserves Existing Provider Abstraction, Usage Caps and Opt-Out Handling

- Provider abstraction: ISmsProvider/IEmailProvider + EcoCashSmsProvider/BulkSmsZwProvider/SmtpEmailProvider/SendGridEmailProvider + IMessagingProviderFactory GetSmsProviderAsync/GetEmailProviderAsync reads MessagingProviderSettings where channel active default providerName switch resolves via DI, swappable without touching calling code
- Usage caps: TenantMessagingUsage year/month sms_count/email_count sms_cost/email_cost sms_limit 1000 email_limit 5000, CostEstimator checks remaining = limit - used, after = remaining - recipients, if after<0 capExceeded true hard cap exceeded message, else if after<limit*0.1 capWarning near cap. Hard cap enforced again in Confirm batch endpoint and in background job before send.
- Opt-out handling: GuardianContactPreference sms_opt_in/email_opt_in sms_opt_out hard/email_opt_out opt_out_at, reason. Filter in TemplateService.PreviewAsync, AudienceResolver final filtering, CreateBatch filtering, background job double-check before send, delivery log is_opted_out true, counted as filteredOptOut, not sent, cost estimate excludes opted out. Endpoints POST /opt-out {guardianId, channel, reason} sets opt_out true, opt_in false.

### Migration

- V9_Communication.sql: announcements, scheduled_messages, audience_segments, template_categories, categorized_templates, inbound_sms, communication_rules (seed 4 rules per tenant: absence_3_days N=3, arrears_over_100 threshold 100, report_published, invoice_due_7), communication_logs with fulltext index message_body, plus seed template categories fees/attendance/academic/general.

### Frontend

- `Frontend/CommunicationFull.jsx`: Tabs Announcements, Scheduled, Saved Segments, Template Library Categories, Two-Way SMS, Rule Engine, Analytics by Campaign, Per-Learner Log searchable. Each tab with create form, list, preview, cost estimate, cap warning.

### Tests (to add)

- Audience segment dynamic re-evaluates at send time (arrears changes)
- Rule engine absence N days triggers only when consecutive >=N
- Opt-out filtered
- Cap exceeded blocks confirm
- Two-way inbound matched guardian via phone
- Communication log searchable by learner returns only that learner's logs (tenant filter + studentId)

