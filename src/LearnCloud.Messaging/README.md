# LearnCloud Messaging Module V1 — SMS & Email to Guardians
**Provider abstraction, audience selection, merge-field templates, background job batching retry, delivery logs, per-tenant cap, opt-out honoured, cost estimate before confirm**

## Entities
- **MessageTemplate:** tenant_id, name, code unique per tenant, channel Sms/Email/Portal, subject, body with merge fields {{learner_name}}, {{class}}, {{amount_owed}}, {{date}}, {{school_name}}, is_system (seeded protected), merge_fields_json
- **MessageBatch:** batch_number MSG-2026-00001, title, template_id nullable, channel, audience_type (Class, Stream, YearGroup, AllGuardians, ArrearsOverX, AbsentToday, Manual, DynamicList), audience_filter_json, body snapshot, subject, total_recipients, sent/delivered/failed counts, estimated_cost/actual_cost decimal+currency, status Draft/Queued/Sending/Sent/Failed, queued/started/completed at, created_by_user_id, cost_estimate_json breakdown
- **MessageDeliveryLog:** batch_id FK, guardian_id, student_id (context which learner), recipient_name, recipient_address phone/email, channel, status Queued/Sending/Sent/Delivered/Failed/Cancelled, provider (EcoCashSms, BulkSmsZw, Smtp, SendGrid), provider_reference, cost decimal+currency, rendered_body per recipient (merge resolved), retry_count, last_attempt_at, delivered_at, failure_reason, is_opted_out
- **TenantMessagingUsage:** tenant_id, year, month, sms_count, email_count, sms_cost, email_cost, sms_limit default 1000, email_limit 5000, currency — per-tenant usage counter for billing SMS bundles
- **GuardianContactPreference:** guardian_id, sms_opt_in true, email_opt_in true, sms_opt_out false hard opt-out, email_opt_out, opt_out_at, reason, preferred_language — opt-out always honoured
- **MessagingProviderSettings:** tenant_id, channel Sms/Email, provider_name EcoCashSms/BulkSmsZw/Smtp/SendGrid, is_active, is_default, config_json apiKey/senderId/smtp host, cost_per_sms 0.05, cost_per_email 0.01, currency, rate_limit_per_second 10, daily_cap 1000 hard cap — swappable without touching calling code

## Provider Abstraction

**Interfaces:**
- `ISmsProvider`: ProviderName, `Task<SmsResult> SendAsync(SmsMessage To, Body, From, CallbackUrl)` returns success, providerReference, failureReason, cost, currency, `GetBalanceAsync`
- `IEmailProvider`: ProviderName, `Task<EmailResult> SendAsync(EmailMessage To, ToName, Subject, HtmlBody, TextBody, From, FromName, Attachments)`

**Concrete implementations, one each:**
- `EcoCashSmsProvider` : ISmsProvider, options ApiKey, ApiUrl, SenderId LearnCloud, CostPerSms 0.05 USD, mock 95% success 5% transient failure for retry testing, 50ms delay, logs truncated
- `BulkSmsZwProvider` : ISmsProvider alternative, 0.04 USD, swappable via settings provider_name = BulkSmsZw
- `SmtpEmailProvider` : IEmailProvider, options FromEmail noreply@learncloud.co.zw, SmtpHost, Port, User/Pass, CostPerEmail 0.01, mock 3% failure
- `SendGridEmailProvider` : IEmailProvider alternative, 0.005 USD

**Factory `IMessagingProviderFactory`:** `GetSmsProviderAsync(tenantId)` looks up `messaging_provider_settings` where channel=Sms is_active and is_default, provider_name switch resolves via DI (keyed services), same for email. So provider can be swapped by updating settings row, no code change in calling code.

## Audience Selection

`AudienceResolver.ResolveAsync(tenantId, AudienceRequest)`:

- **Class:** gradeId + streamId required, gets enrolments is_current, for each student gets guardian links (primary/billing) → RecipientInfo guardian + student + class + school_name
- **Stream:** similar
- **YearGroup:** gradeId only, all streams in grade
- **AllGuardians:** all guardians tenant
- **ArrearsOverX:** dynamic list guardians of learners with arrears over X threshold: query fee_invoices balance_due > X, studentIds distinct, get billing guardian links, RecipientInfo includes amount_owed sum, currency, due_date, studentName
- **AbsentToday:** dynamic list guardians of learners absent today: attendance_records attendance_date = today and status Absent/Sick, guardian links
- **Manual:** manualGuardianIds list
- Deduplicates per child for personalization (same guardian multiple children kept separate rows per child for merge learner_name)

## Templates with Merge Fields

- Available fields: learner_name, learner_first_name, learner_last_name, learner_number, class, grade, stream, school_name, amount_owed, currency, date, due_date, guardian_name, guardian_first_name, teacher_name, next_term_date, message_body, absence_reason
- Extraction via regex `{{\\s*(\\w+)\\s*}}`
- `TemplateService.RenderAsync(body, mergeData)` replaces placeholders, keeps unknown as is
- Previewed against real recipient: `PreviewAsync` resolves audience, filters opt-out and no contact, picks sample recipient (or sampleGuardianId), builds merge data from RecipientInfo, renders body/subject, returns totalRecipients, filteredOptOut, filteredNoContact, sampleRecipientName/Address, renderedBody/Subject, mergeFieldsUsed, costEstimate

**Three seeded templates (plus email variant):**
1. **Fee Reminder** Sms: "Dear {{guardian_name}}, learner {{learner_name}} ({{class}}) has outstanding balance {{currency}} {{amount_owed}} due {{due_date}}. Please settle at school. {{school_name}}"
2. **Absence Notification** Sms: "Dear {{guardian_name}}, {{learner_name}} ({{class}}) was absent today {{date}}. Reason: {{absence_reason}}. Please contact school. {{school_name}}"
3. **General Notice** Email: Subject "{{school_name}} - {{subject}}" Body "Dear {{guardian_name}}, notice for {{learner_name}} ({{class}}): {{message_body}} Thank you, {{school_name}} Date {{date}}"
4. Fee Reminder Email variant HTML

Seeded via POST /api/messaging/templates/seed, is_system true protected.

## Sending as Background Job with Batching, Retry Backoff, Per-Tenant Rate Limits

- `MessagingBackgroundJob.ProcessBatchAsync(batchId)`:
  - Loads batch, checks status Queued/Sending
  - Loads delivery logs where status Queued, order Id
  - Batching: 50 messages per chunk
  - Rate limiting: providerSettings rate_limit_per_second 10, delayBetweenBatches = batchSize / rateLimitPerSecond
  - For each log: double-check opt-out (in case opt-out happened after queuing) → Cancelled if opted out, failed count
  - Retry with backoff: 3 retries exponential 2^retry seconds (1s,2s,4s) on transient failure
  - Calls provider via factory GetSmsProviderAsync / GetEmailProviderAsync
  - On success: status Sent, provider, providerReference, cost, deliveredAt, sent/delivered counters, actualCost sum Round2
  - On failure after retries: status Failed, failureReason, failed count
  - After each chunk: update batch sent/delivered/failed, actualCost, progressPercent, save, update TenantMessagingUsage sms_count/email_count and costs
  - At end: batch status Sent (or Failed if all failed), completedAt, progress 100%

## Delivery Log Per Message + Usage Counter

- `MessageDeliveryLog` per recipient: recipient, channel, status, provider, providerReference, cost decimal, timestamp lastAttempt, deliveredAt, failureReason, renderedBody per recipient, retryCount, isOptedOut
- `TenantMessagingUsage` per tenant year/month: sms_count, email_count, sms_cost, email_cost, sms_limit, email_limit, currency — updated after each chunk in background job, used for billing SMS bundles

## Hard Per-Tenant Sending Cap with Warning Before Hit

- `MessagingProviderSettings.daily_cap` e.g. 1000 SMS per tenant per month (or daily, here monthly for simplicity but field named dailyCap for V1)
- `TenantMessagingUsage` tracks used vs limit
- `CostEstimator.EstimateAsync` computes totalCost, checks remaining = limit - used, after = remaining - recipients
  - If after <0 → capExceeded true, warningMessage "Hard cap exceeded: limit 1000, used 800, trying 300, exceed by 100. Reduce audience or increase cap" → frontend blocks confirm, returns 400
  - Else if after < limit*0.1 (10% remaining) → capWarning true, message "Warning: You are about to hit your SMS cap. Limit 1000, used 800, this send 150, remaining after 50. Consider topping up."
  - RemainingAfterSend = after
- Warning shown in preview and cost-estimate and compose flow before confirming bulk send, to protect both school and you from runaway costs
- Hard cap enforced again in Confirm endpoint: checks used+trying > cap → 400 Hard cap exceeded

## Guardian Contact Preferences and Opt-Out Always Honoured

- `GuardianContactPreference` sms_opt_in, email_opt_in, sms_opt_out hard, email_opt_out, opt_out_at, reason
- In `TemplateService.PreviewAsync` and `AudienceResolver` final filtering and in `CreateBatch` and in background job double-check: if pref sms_opt_out or !sms_opt_in → filtered out counted as FilteredOptOut, not sent
- Opt-out endpoint POST /api/messaging/opt-out {guardianId, channel, reason} sets opt_out true and opt_in false, opt_out_at now
- Update preference endpoint POST /contact-preferences
- Always honoured: even if batch already queued, background job re-checks before send

## Cost Estimate Shown Before Confirming Bulk Send

- Preview endpoint returns CostEstimateDto: totalRecipients, smsRecipients, emailRecipients, filteredOutOptOut, filteredOutNoContact, costPerSms, costPerEmail, totalCost, currency, capWarning, capWarningMessage, capExceeded, remainingAfterSend
- Compose flow: audience selection → template choice → compose with merge fields → Preview button → shows sample recipient name/address, rendered subject/body, merge fields used, total recipients, filtered counts, cost breakdown, cap warning
- Cost estimate endpoint separate POST /cost-estimate
- Create batch returns batch + costEstimate + filtered counts + capWarning
- Confirm batch checks cap again
- Frontend shows cost before Confirm: "This send will cost $7.50 (150 SMS @ $0.05) — remaining after 50 — Warning near cap"

## Backend Endpoints

- Templates: GET /templates, POST /templates, POST /templates/seed (3 templates)
- Preview: POST /preview {templateId, channel, audience, body, subject, sampleGuardianId} → total, filtered, sample rendered, cost estimate
- Cost: POST /cost-estimate
- Batches: POST /batches {title, templateId, channel, audience, customBody, customSubject} → creates batch + delivery logs filtered opt-out, returns batch + cost + filtered counts + warning; POST /batches/{id}/confirm → checks hard cap, queues, starts background job; GET /batches list 50, GET /batches/{id}, GET /batches/{id}/logs 200
- Usage: GET /usage year/month counts/limits remaining nearCap warning
- Opt-out: POST /opt-out {guardianId, channel, reason}, POST /contact-preferences {guardianId, smsOptIn, emailOptIn, smsOptOut, emailOptOut}
- Provider settings: would be admin endpoint to swap provider via configJson without code

## React Compose Flow

`Frontend/MessagingCompose.jsx`:

- Step 1 Template choice: list templates seeded, select → body/subject/channel auto-filled, shows merge fields, button Seed 3 Templates
- Step 2 Audience selection: dropdown class/stream/year_group/all_guardians/arrears_over_x/absent_today/manual, inputs gradeId/streamId/academicYearId/arrearsThreshold/date, note audience resolved server-side tenant filter + guardian links
- Step 3 Channel: SMS 0.05 USD / Email 0.01 USD, note provider swappable via settings
- Step 4 Compose with merge fields: subject, body textarea with placeholders {{guardian_name}} etc., available fields list
- Step 5 Preview against real recipient + cost estimate before confirm: button Preview + Cost Estimate → calls /preview, shows total recipients, filtered opt-out/no-contact, sample recipient name/address, rendered subject/body, merge fields used, cost breakdown totalCost, cap warning/exceeded, remaining after
- Step 6 Confirm bulk send: Create Batch button → POST /batches returns batchNumber totalRecipients estimatedCost filteredOutOptOut capWarning, shows, then confirmation dialog "Confirm send X messages costing $Y?" → POST /batches/{id}/confirm queues background job with batching 50 retry 3 exponential backoff per-tenant rate limit 10/sec, delivery logs per message

Usage + cap warning top shows SMS 800/1000 remaining 200 etc.

Opt-out honoured: filtered counts shown, background job double-check.

## Tests (to add)

- Provider abstraction: swap EcoCashSms to BulkSmsZw via settings without touching calling code (factory returns different provider)
- Audience arrears_over_x returns only billing guardians with amount_owed
- Opt-out filtered
- Cap exceeded blocks confirm
- Cost estimate exact to cent
- Template merge renders correctly

