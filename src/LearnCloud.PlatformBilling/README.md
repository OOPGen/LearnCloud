# LearnCloud Platform Billing - How Schools Pay You, Not How Learners Pay School
**Turns software into business — Subscription & Billing**

## Model

**Plan:** name, description, price per learner per term, minimum charge, included modules as feature flags JSON, included SMS bundle, learner limit, currency USD. Seed: Starter 150-300 learners $0.50/learner min $99 300 SMS, Growth 301-800 $1.00 min $149 500 SMS, Scale 801-2000 $2.00 min $199 1000 SMS.

**Subscription per tenant with state machine: trialing, active, past_due, suspended, cancelled, expired, archived**

**Legal Transitions (every):**

- Trialing -> Active via TrialConverted / PaymentReceived (payment during trial converts)
- Trialing -> Expired via TrialExpired (14 days no payment, 30-day read-only window)
- Trialing -> Cancelled via CancelledBySchool/Admin
- Active -> PastDue via InvoiceOverdue (dunning job moves overdue)
- Active -> Cancelled via CancelledBySchool/Admin
- Active -> Active via PlanUpgraded (immediate pro-rata) and PlanDowngraded (next period pending)
- PastDue -> Active via PaymentReceived
- PastDue -> Suspended via GracePeriodEnded (7 days configurable)
- PastDue -> Cancelled via CancelledBySchool/Admin
- Suspended -> Active via PaymentReceived (win-back, read-only removed)
- Suspended -> Cancelled via SuspensionGraceEnded (30 days) or CancelledByAdmin/School
- Expired -> Active via PaymentReceived (win-back during 30-day read-only) or Reactivated
- Expired -> Archived via ReadOnlyWindowEnded (30 days after expiry)
- Expired -> Cancelled via CancelledByAdmin
- Cancelled -> Active via Reactivated (win-back)
- Cancelled -> Archived via ReadOnlyWindowEnded
- Archived -> Active via Reactivated
- Trialing -> Trialing via ManualOverride (extend trial)
- Plus manual overrides: PastDue->Active, Suspended->Active, Active->Active credit invoice

Triggers: TrialStarted, TrialConverted, PaymentReceived, InvoiceOverdue, GracePeriodEnded, SuspensionGraceEnded, TrialExpired, ReadOnlyWindowEnded, CancelledBySchool/Admin, PlanUpgraded/Downgraded, ManualOverride, Reactivated

Guards and audit on each transition, PreviousState stored, PastDueSince, SuspendedSince, ExpiredSince, ReadOnlyUntil, CancelledAt.

**Billing period aligned to school term, enrolment snapshot at period start used as billable learner count:**

- CurrentPeriodStart/End aligned to term dates (e.g., Term2 2026 May10-Aug10)
- BillableLearnerCount snapshot taken at period start: `SELECT COUNT(*) FROM students WHERE tenant_id=X AND enrolment is_current true`
- CurrentLearnerCount live for over-limit warning
- Invoice subtotal = billable * price_per_learner, but minimum charge applied if subtotal < minimum

**Platform invoices to school:** InvoiceNumber PLAT-2026-00001 per tenant per year, line items description "Growth plan - 542 learners @ $2.00/learner - Term2 2026 (billable 542)", quantity learner count, unit price, line total, metadata billable/plan/minCharge, issue_date, due_date 14 days, subtotal, minimumChargeApplied, proRataAdjustment, discount, total, amount_paid, balance_due, currency, status draft/issued/partial/paid/void, notes.

**Platform payment recording manual capture at first, gateway later:** PlatformPayment tenant, invoice_id, amount decimal, currency, method manual/paynow/stripe/bank_transfer, reference, payment_date, status confirmed/pending/reversed, audit.

**Dunning:** Scheduled job `DunningJob` runs daily:
- Trial reminders day7, day12, expiry, 30-day read-only window before archival
- Overdue invoices -> Active to PastDue, escalating reminders every 3 days while past due
- PastDue grace 7 days -> Suspended after, suspension warning 2 days and 1 day before
- Suspended grace 30 days -> Cancelled, plus suspension warning
- Expired read-only window 30 days -> Archived
- Each event writes DunningEvent tenant/subscription/invoice, event_type trial_reminder_day_7 etc, channel email, recipient contactEmail, subject/body, sent_at, success
- Never delete data, never lock out entirely — suspension read-only banner and payment link

**Suspension behaviour:** Tenant becomes read-only with clear banner and payment link: "Your account is suspended due to non-payment since X. You are in read-only mode with banner and payment link. You can view and export your records, but cannot edit. Pay now to reactivate. Never delete data, never lock out entirely." Payment link `https://{slug}.learncloud.co.zw/billing/pay`, data retained 7 years, export allowed, win-back possible.

**Feature gating:** Middleware `FeatureGatingMiddleware` ordered after authentication before authorization, maps endpoint prefix to required feature flag: /api/students→students, /api/attendance→attendance, /api/fees→fees, etc. Loads subscription plan included_modules_json, if required feature not in list returns 402 Payment Required JSON {error:upgrade_required, message, requiredFeature, currentPlan, upgradeUrl, includedModules}. Also attribute `[RequiresFeature("students")]` for controller level.

**Trial:** 14 days no card, reminders at day 7 (Trial reminder 7 days left), day 12 (expires soon), expiry (Trial expired read-only 30 days), 30-day read-only window after expiry before archival. Trial plan is Starter with limited modules.

**Plan changes:** Upgrade takes effect immediately with pro-rata charge; downgrade takes effect at next period; both logged in PlanChangeLog from_plan, to_plan, change_type upgrade/downgrade, effective_type immediate/next_period, effective_at, proRataCharge calculation priceDiff * learners * daysRemaining/daysInPeriod rounded 2 AwayFromZero, reason, changed_by_user_id, notes. Upgrade creates pro-rata invoice immediately.

**Platform admin views:**
- Tenant list with subscription state: tenantId, name, slug, city, plan, planCode, state, trialEndsAt, period start/end, billable/current/learnerLimit, overLimit flag, pastDue/suspended/expired dates, learnerLimit, pricePerLearner
- Revenue by month: year, totalRevenue, byMonth monthName revenue invoicesIssued/Paid mrr, bar chart width
- Trials converting: totalTrials, converting 7+ days, atRisk ≤2 days, trials list started/ends/daysLeft/isAtRisk
- Tenants at risk: past_due, suspended, expired, over learner limit, reason
- Manual override to extend trial or credit invoice, fully audited: POST /overrides/extend-trial {tenantId, newTrialEndsAt, reason>=10} audited BillingOverride + AuditLog, POST /overrides/credit-invoice {tenantId, invoiceId, amount, reason} reduces balance, audit
- Revenue summary: totalTenants, active, trialing, pastDue, suspended, mrr (payments this month), outstanding, churnRisk
- Overrides list recent 100

**Entities:**
- Plan, Subscription (state machine), PlatformInvoice, PlatformInvoiceLine, PlatformPayment, DunningEvent, PlanChangeLog, BillingOverride (manual override audited)

**State Machine Unit Tests:**
- `Tests/SubscriptionStateMachineTests.cs` - every transition: trialing->active via TrialConverted, trialing->expired via TrialExpired, trialing->cancelled, active->pastDue, active->cancelled, active plan upgraded stays active, active plan downgraded stays active, pastDue->active via payment, pastDue->suspended via grace ended, suspended->active via payment, suspended->cancelled via suspension grace, expired->active win-back, expired->archived, cancelled->active reactivated, illegal trialing->suspended not allowed, active->expired not allowed, archived->pastDue not allowed, manual override extend trial stays trialing, apply transition sets pastDueSince and readOnlyUntil, upgrade pro-rata exact 66.67, downgrade takes effect next period not immediate, all legal transitions count >=20

**Background Jobs:**
- DunningJob: ProcessTrials (day7,12,expiry), ProcessOverdueInvoices (active->pastDue), ProcessPastDueGrace (pastDue->suspended after 7d, suspended->cancelled after 30d), ProcessExpiredReadOnlyWindow (expired->archived after 30d), SendDunningEvent writes dunning_events and logs

**Endpoints:**
- Tenant billing: GET /api/billing/subscription with banner isReadOnly + paymentLink + canEdit, GET /api/billing/invoices, GET /invoices/{id}, POST /api/billing/plan/change {newPlanId, reason} upgrade immediate pro-rata / downgrade next period, GET /api/billing/read-only-status
- Platform admin: GET /api/platform/tenants?state, GET /revenue/by-month?year, GET /trials/converting, GET /tenants/at-risk, POST /overrides/extend-trial, POST /overrides/credit-invoice, GET /overrides, GET /revenue/summary
- Billing service: POST /api/fees already for learner billing, here platform invoices: via BillingService.CreateInvoiceAsync billable snapshot, RecordPaymentAsync manual capture

**Frontend Admin Console:**
- `Frontend/AdminConsole.jsx` tabs: Tenants + State table with state badge Active green PastDue warning Suspended danger Trialing primary, billable/current/limit over flag, trial ends, period; Revenue by Month bar chart total revenue, 12 months revenue invoicesPaid; Trials Converting total/converting/atRisk + table school slug started/ends/daysLeft; Tenants at Risk past_due/suspended/expired/over limit reason; Manual Overrides extend trial (tenantId, new date, reason>=10 audited) + credit invoice (tenantId, invoiceId, amount, reason) + recent overrides table tenant/type/details/reason/admin/date; info box suspension behaviour read-only banner payment link never delete, feature gating 402 upgrade_required, plan changes upgrade immediate pro-rata downgrade next period logged
- `Frontend/BillingScreens.jsx`: ReadOnlyBanner component checks /read-only-status, shows warning with payment link if suspended/expired/pastDue, never delete data; TenantBilling component subscription plan/state, billable/current/limit, trial ends, period, pending downgrade, banner, paymentLink, plan change select upgrade/downgrade, invoices table number issue/due subtotal/total/balance/status

**Migration:** `V6_PlatformBilling.sql` plans, subscriptions with state int, past_due/suspended/expired/read_only_until, pending_plan, platform_invoices numbering PLAT-YYYY-XXXXX, lines, payments, dunning_events, plan_change_logs, billing_overrides, seed plans Starter $0.50 min $99 300 SMS, Growth $1.00 min $149 500 SMS, Scale $2.00 min $199 1000 SMS with included_modules JSON.

**Never delete data, never lock out entirely:** Suspension sets read-only flag, frontend shell checks /read-only-status and shows banner but allows GET list/export, blocks POST/PUT/DELETE via middleware? Actually read-only middleware would block mutations if subscription suspended/expired/pastDue? In V1 we show banner but still allow view/export, block edit via feature? Spec says read-only with banner and payment link, never lock out entirely - so allow GET but block POST/PUT/DELETE for tenant in read-only.

