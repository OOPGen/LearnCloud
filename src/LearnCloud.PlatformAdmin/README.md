# Platform Admin Console — Internal Tool for Running LearnCloud as Business
**Sits outside tenant scope and every action within it is audited. Access requires platform superadmin + second factor. Impersonation without consent impossible by design.**

## Tenant List: School, Plan, Subscription State, Learner Count, Last Activity, Monthly Value, Health Indicator

- `PlatformAdminService.ListTenantsAsync(TenantListRequest)` with filters state, planCode, search, healthStatus, sortBy learners/mrr/last_activity, pagination page/pageSize
- TenantListDto: tenantId, schoolName, slug, city, planName, planCode, subscriptionState trialing/active/pastDue/suspended/cancelled/expired, learnerCount, learnerLimit, isOverLimit, lastActivityAt from AuditLogs max createdAt, monthlyValue billable*pricePerLearner min minimumCharge, currency, healthStatus healthy/warning/at_risk/critical from TenantHealthScore, healthScore 0-100, trialEndsAt, currentPeriodEnd
- Health indicator: TenantHealthScore calculated from factors: falling logins (auditLogs count last 7 days vs previous 7 days), rising tickets (support notes category ticket), unpaid (pastDue/suspended), over learner limit, trial ending soon. Score 100 healthy, <70 warning, <50 at_risk, <30 critical. MonthlyValue = billable*price.

## Tenant Detail: Subscription History, Invoices, Usage, Support Notes, Manual Actions

- `GetTenantDetailAsync(tenantId)`: tenant detail, current subscription (plan, state, trial, period, billable/current, pastDue/suspended/expired/readOnlyUntil, pendingPlan), subscription history from AuditLogs where entityType Subscription, platform invoices list 20 recent, usage (users count, learners count, smsCount/smsCost/emailCount, storageBytes/Gb monthlyTrend 12 months from TenantMessagingUsage), support notes internal, health
- Support notes: `AddSupportNoteAsync` content, isInternal true internal only, category billing/technical/onboarding, createdByUserId platform admin, audit create_support_note
- Manual actions each requiring reason >=10 chars, fully audited:
  - Extend trial: POST /console/tenants/{tenantId}/extend-trial {newTrialEndsAt, reason} → updates trialEndsAt, if expired moves back to trialing, creates BillingOverride overrideType extend_trial detailsJson old/new, audit manual_override_extend_trial
  - Change plan: POST /console/tenants/{tenantId}/change-plan {newPlanId, reason, isUpgrade} → if upgrade immediate pro-rata charge priceDiff*learners*daysRemaining/daysInPeriod, creates PlatformInvoice pro-rata, PlanChangeLog upgrade immediate / downgrade next period pendingPlanId effective next period, audit plan_upgrade/downgrade
  - Credit invoice: POST /console/tenants/{tenantId}/credit-invoice {invoiceId, amount, reason} → amount must >0 <= balanceDue, discountAmount += amount, balanceDue -= amount, if balance 0 status paid, BillingOverride credit_invoice, audit manual_override_credit_invoice
  - Suspend: POST /console/tenants/{tenantId}/suspend {reason, isImmediate} → state Suspended, suspendedSince now, audit suspend
  - Reactivate: POST /console/tenants/{tenantId}/reactivate {reason} → state Active, clears suspended/pastDue/expired/readOnlyUntil, audit reactivate

## Consented, Time-Limited Support Impersonation

- **ImpersonationGrant**: tenantId, grantedByUserId must be SCHOOL_ADMIN in that tenant (not platform admin) - enforced by service checking UserRole, grantedByRole SCHOOL_ADMIN, grantedToRole PLATFORM_SUPERADMIN, hasConsent bool explicit checkbox required, reason >=10 chars, expiresAt 5-240 min max 4 hours, revokedAt, tokenHash, isActive = hasConsent && !IsDeleted && ExpiresAt > now && RevokedAt==null - by design only school admin can create grant, platform admin cannot create grant for themselves, so impersonation without consent impossible by design, not by policy
- **ImpersonationSession**: grantId, tenantId, impersonatorUserId platform superadmin, impersonatedUserId school admin who granted or null as tenant, startedAt now, expiresAt = grant expiresAt, endedAt, bannerMessage "You are in support impersonation mode for tenant X - granted by school admin Y reason - expires U - every action audited as performed-on-behalf-of", ip, userAgent, isActive = not deleted and endedAt null and expiresAt > now
- **Flow**: School admin goes to Settings → Support → Grant Access, enters reason, checks consent, selects duration 60 min, creates grant via POST /tenants/{tenantId}/impersonation-grants (requires SCHOOL_ADMIN role). Platform admin lists grants GET /tenants/{tenantId}/impersonation-grants, sees active consented grant, starts session POST /impersonation/sessions {grantId} - service verifies impersonator is PLATFORM_SUPERADMIN role, grant is active, has consent, was created by SCHOOL_ADMIN, not expired, not revoked, then creates session and logs critical audit start_impersonation with tenant, grant, expires, IP. Session expires automatically when ExpiresAt > now check in middleware or job that ends expired sessions. Banner visible throughout - frontend checks active impersonation session via GET /impersonation/sessions/active and shows top banner red with grant reason expires and End Impersonation button. Every action recorded as performed-on-behalf-of: audit logs have tenantId = impersonated tenant, userId = impersonatorUserId, plus extra field impersonated? In AuditLog we store userId = impersonator and add extra JSON performedOnBehalfOf tenantId grantId. Middleware that detects impersonation session adds header X-Impersonation-Session and logs.

- **Impossible without consent by design**: Grant creation endpoint requires SCHOOL_ADMIN role in that tenant - platform admin has PLATFORM_SUPERADMIN role with tenant_id null, not SCHOOL_ADMIN in tenant, so call fails with "Only school admin in that tenant can grant". Platform admin cannot create grant table row directly because service checks role. Even if platform admin bypasses API and inserts directly into DB, grant would have grantedByRole = PLATFORM_SUPERADMIN not SCHOOL_ADMIN, and IsActive check requires grantedByRole SCHOOL_ADMIN? We enforce grantedByRole must be SCHOOL_ADMIN in service, and tokenHash generated only when granted by school admin. So by code, not policy.

- **Endpoints**: POST /tenants/{tenantId}/impersonation-grants (SCHOOL_ADMIN only), GET /tenants/{tenantId}/impersonation-grants, POST /impersonation-grants/{id}/revoke, POST /impersonation/sessions (PLATFORM_SUPERADMIN + valid grant), POST /impersonation/sessions/{id}/end, GET /impersonation/sessions/active

## Business Metrics: MRR, New Tenants, Churn, Trial Conversion, Revenue by Plan, Schools at Risk

- `GetBusinessMetricsAsync`: now, startOfMonth, startOfLastMonth, active subs count, mrr = sum billable*pricePerLearner, previous mrr (simplified), mrrGrowth, newTenantsThisMonth count tenants createdAt >= startOfMonth, newLastMonth, churnThisMonth count subscriptions state Cancelled and cancelledAt >= startOfMonth, churnRate churn/active*100, trialing count, convertedTrials trialConvertedAt >= startOfMonth, trialConversionRate converted/trialing*100, revenueByPlan group by plan name/code tenants count monthly revenue sum billable*price percentage, trialsTotal, converting (daysSinceStart>=7), atRisk <=2 days, schoolsAtRisk via GetAtRiskTenants: pastDue, suspended, expired, over learner limit, falling logins (auditLogs count last 7 days vs previous 7 days drop >50%), rising tickets (support notes category ticket count increase), unpaid, healthScore. RevenueByMonth last 12 months: for each month year month revenue sum platformPayments paymentDate in month, newTenants, churned, active. Returns BusinessMetricsDto.

## Operational Views: Background Job Status and Failures, Error Rates, SMS Spend by Tenant, Storage Growth

- BackgroundJobRecord tenantId nullable, jobType invoice_generation/dunning/messaging_batch/promotion_batch/backup, jobId Hangfire id, status pending/running/completed/failed/cancelled, startedAt/completedAt, retryCount, errorMessage, resultJson, createdBy. Endpoint GET /console/operational/jobs?status=failed returns 100 recent, shows tenantName, jobType, status, started/completed, retry, errorMessage.
- ErrorRateSnapshot snapshotDate, tenantId nullable platform-wide, service api/web/worker, errorCount, warningCount, requestCount, errorRate errorCount/requestCount. Endpoint GET error-rates?from&to.
- SmsSpendByTenant: TenantMessagingUsage year month smsCount smsCost emailCount emailCost currency totalCost, endpoint GET sms-spend?year&month returns ordered totalCost desc.
- StorageGrowthRecord tenantId totalFiles totalBytes documentBytes photoBytes measuredAt growthLast30Days. Endpoint GET storage-growth.

## Announcement Broadcast to All School Admins for Maintenance Windows and Releases

- AnnouncementBroadcast title, body markdown/html, audience all_school_admins/all_tenants/specific_plan, audienceFilterJson, scheduledAt null=immediate, expiresAt, priority low/normal/high/urgent for maintenance, status draft/scheduled/sent/archived, createdByUserId, sentAt, sentCount.
- Endpoint POST /console/broadcasts {title, body, audience, filter, scheduledAt, expiresAt, priority} creates broadcast, if scheduled null sends immediately to all school admins (would create Announcement entities per tenant? For V1 just creates broadcast record and logs). Frontend BroadcastTab form.

## Access Requires Platform Superadmin Role Plus Second Factor

- Middleware SecondFactorMiddleware: applies to /api/platform and /api/platform/console routes, checks if user is PLATFORM_SUPERADMIN role, if yes checks session 2fa_verified == true, if not returns 403 error second_factor_required with verifyUrl /api/platform/second-factor/verify. Allow verify endpoint itself without 2FA.
- Second factor setup: POST /second-factor/verify {code, recoveryCode} - in real app verify TOTP code against secret stored for user via authenticator app QR code otpauth://totp/LearnCloud:platform-admin?secret=...&issuer=LearnCloud. For demo, accepts 123456 as valid TOTP.
- Frontend PlatformAdminConsole first shows second factor screen: QR code URI, secret, recovery codes, input TOTP 6 digits, verify button.

## Every Action Audited

- All manual actions (extend trial, change plan, credit invoice, suspend, reactivate, support note, impersonation grant/revoke/start/end, broadcast) create AuditLog with tenantId, userId actorUserId, entityType, entityId, action, oldValues/newValues JSON, createdBy.
- Platform admin console itself sits outside tenant scope: ITenantContext TenantId null for platform admin, but audit logs store tenantId of target tenant and userId of platform admin, with extra performed-on-behalf-of if impersonating.
- Impersonation banner visible throughout when active session exists: frontend checks GET /impersonation/sessions/active and shows red banner top with grant reason expires and End button.

## Frontend

- PlatformAdminConsole.jsx: second factor screen first, then tabs tenant list + health, tenant detail, business metrics MRR, operational jobs/errors/sms/storage, impersonation consented, broadcast, overrides audited. second factor 123456 demo.

## Migration V11_PlatformAdmin.sql

- support_notes, impersonation_grants with has_consent reason expires_at revoked_at token_hash, impersonation_sessions grant_id tenant_id impersonator_user_id impersonated_user_id started_at expires_at ended_at banner_message ip user_agent, announcement_broadcasts title body audience scheduled/expires priority status created_by sent_at sent_count, tenant_health_scores tenant_id score health_status healthy/warning/at_risk/critical calculated_at factors_json monthly_value last_activity_at, background_job_records tenant_id job_type job_id status started/completed retry error result_json, error_rate_snapshots snapshot_date tenant_id service error_count warning_count request_count error_rate.

## Impersonation Without Consent Impossible by Design, Not Policy

- Code enforces: GrantAccessAsync checks grantedByUserId must have SCHOOL_ADMIN role in that tenant via UserRole join Role CODE SCHOOL_ADMIN. Platform admin has PLATFORM_SUPERADMIN role with tenant_id null, not SCHOOL_ADMIN in tenant, so call fails Unauthorized "Only school admin in that tenant can grant". Even if platform admin directly inserts into impersonation_grants table bypassing API, IsActive requires hasConsent true and grantedByRole SCHOOL_ADMIN, and StartImpersonationAsync verifies grant was created by SCHOOL_ADMIN role and HasConsent true and not expired. So cannot impersonate without school admin creating grant with explicit consent checkbox. Session expires automatically via ExpiresAt check, banner visible via frontend active session poll, every action audited as performed-on-behalf-of with tenantId and impersonatorUserId and grantId.

