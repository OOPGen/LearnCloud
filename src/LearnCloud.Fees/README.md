# LearnCloud Fees Module V1 — Money Bugs Cost Customers Permanently
**HQ Bulawayo, Scope: billing learners only**

## Entities & Migration
- FeeItem: tuition, boarding, transport, levy, uniform, recurrence per_term/per_year/one_off, is_proratable, is_optional, gl_code, tenant_id leading index
- FeeStructure: name, academic_year_id, term_id, grade_id null=school-wide, stream_id null=grade-wide, student_id null=individual override, status draft/active/archived, currency USD, priority individual>stream>grade>school-wide, Items collection
- FeeStructureItem: fee_structure_id, fee_item_id, description snapshot, amount decimal(18,2), currency, quantity, line_total
- Discount: student_id, year/term, type percentage/fixed, value decimal, currency, applies_to_fee_item_ids_json null=total, reason, approver_user_id, status approved, effective from/to
- InvoiceSequence, ReceiptSequence: tenant_id, year, last_number FOR UPDATE to avoid duplicates, prefix INV/REC, format {prefix}-{year}-{number:5}
- FeeInvoice: invoice_number unique per tenant per year INV-2026-00001, academic_year/term, student, enrolment, fee_structure_id, structure_hash for idempotency, subtotal, discount, total, amount_paid, balance_due, currency, issue_date, due_date, status, is_prorated, proration_note. LineItems snapshot.
- FeeInvoiceItem: invoice_id, fee_structure_item_id, fee_item_id, description, quantity, unit_amount, line_total, currency, is_prorated, proration_detail
- Payment: student_id, amount decimal, currency, method cash/bank/ecocash/etc, reference, payment_date, receipt_number REC-2026-00001 unique per tenant, proof_url, status confirmed/pending/reversed, reversed_by, original_payment_id, reversal_reason
- PaymentAllocation: payment_id, invoice_id, invoice_item_id nullable, allocated_amount decimal, currency, is_manual_override, is_reversal
- LearnerCredit: student_id, amount positive credit held, currency, source overpayment/credit_note, source_payment_id, is_utilized
- CreditNote: invoice_id, student_id, amount positive, currency, reason >=10 chars, approver, credit_note_number CN-2026-00001, status approved, reduces invoice balance, audit
- FeeInvoiceBatch: batch_number, academic_year/term, status pending/running/completed/failed, total_students, processed, created, skipped, failed, result_json {created:[], skipped:[{studentId, reason}]}, progress_percent

Migration `V4_Fees.sql` with tenant_id leading indexes, unique invoice per tenant+number, unique per student+year+term+structure_hash for idempotency, decimal(18,2) + currency.

## FeeCalculationService — Single Source of Truth

**All monetary arithmetic lives here, no arithmetic elsewhere. CI grep forbids double/float/+ - * / % on money outside this file.**

Methods:
- CalculateInvoiceTotals(lineTotals, discounts): subtotal sum lines, percentage discounts first sorted, then fixed, each Round2 AwayFromZero, cap to remaining subtotal, total = subtotal - discountTotal >=0
- CalculateDiscountAmount
- AllocatePayment(paymentAmount, currency, outstandingInvoices FIFO due_date ASC issue_date ASC invoice_number ASC, manualAllocations?): validates currency, manual sum <= payment, invoices belong to learner, manual amount <= balance. FIFO: remaining=payment, for each invoice toAllocate=min(remaining,balance) Round2, allocations, remaining Round2, credit remaining. Manual: allocations from manual, credit remainder. Ensures sum allocations + credit == payment exact to cent.
- CalculateProratedAmount(original, daysEnrolled, totalDays): factor = daysEnrolled/totalDays decimal, raw=original*factor, Round2. Used for mid-term joiners if is_proratable true and tenant setting proration daily.
- ComputeArrearsAsAt(invoices, allocations, asAtDate): totalInvoiced where issue_date <= asAtDate, totalPaid where payment_date <= asAtDate, arrears = totalInvoiced - totalPaid >=0
- ComputeOverdueArrearsAsAt: filter due_date <= asAtDate also
- Round2/ Round4 helpers MidpointRounding.AwayFromZero

**Three real balances reproduced exactly:**
1. Thabo Ndlovu 95.00: 500+50=550 subtotal, 10% discount 55 total 495 paid 400 balance 95
2. Lindiwe Moyo 324.34: 333.33+200+66.67=600 subtotal, 12.5% discount 75 total 525 paid 100.33+100.33=200.66 balance 324.34 decimal exact (double would fail)
3. Kuda Dube 30 credit: 90 days term 27 days remaining factor 0.3 prorated 900*0.3=270+100 levy=370 payment 400 credit 30

Unit tests `FeeCalculationServiceTests` assert exact equality to cent, fail if out by cent.

## Services

- **InvoiceGenerationService**: StartGeneration creates FeeInvoiceBatch pending, enqueues background task (Task.Run for V1, real Hangfire). GenerateForTerm: get active enrolments is_current true, totalStudents, for each enrolment resolve applicable structure priority individual>stream>grade>school-wide, compute structureHash for idempotency, check if invoice exists with same student+year+term+hash => skip reason "Already invoiced INV-...", if enrolment after term end skip "Enrolment after term end", else build line items from structure items with prorating if is_proratable and daysEnrolled<totalDays, discounts, calculate totals via FeeCalculationService, get next invoice number FOR UPDATE from InvoiceSequence, create FeeInvoice + LineItems, save. Progress reporting batch processed/created/skipped/failed + progressPercent + resultJson. Idempotent.

- **PaymentService**: RecordPayment validates student exists, generates receipt number from ReceiptSequence FOR UPDATE, creates Payment confirmed, gets outstanding invoices sorted FIFO, allocates via FeeCalculationService.AllocatePayment (default FIFO or manual override), updates invoice amount_paid, balance_due, status paid/partial, creates PaymentAllocation rows, if credit>0 creates LearnerCredit overpayment, audit log create. ReversePayment: reason >=10 chars required, creates reversal payment with same amount, receipt new, status reversed, finds original allocations and creates reverse allocations is_reversal true, restores invoice balances, soft-deletes credits from original, marks original status reversed, audit reverse. Allocate method clears existing allocations and re-allocates with manual override.

- **ArrearsService**: GetArrearsAsAt computes from ledger, GetArrearsByClass groups invoices balance>0 due_date<=asAtDate by grade/stream/student, daysOverdue = asAtDate - dueDate, guardianPhone placeholder, sorted balance desc. ByAmount groups by student sum balance. LearnerStatement: invoices + payments ordered date, running balance via FeeCalculationService, total invoiced, total paid, balance, credit.

## Endpoints

- Fee Items: GET/POST /api/fees/items [bursar, head view]
- Fee Structures: GET structures?year&term, POST structures [bursar]
- Invoices: POST /invoices/generate-term background job idempotent, GET batches/{batchId} progress, GET invoices?studentId&year&term&status, GET invoices/{id}, GET invoices/{id}/print HTML printable with #0F153A header greyscale legible
- Payments: POST /payments {studentId, amount decimal+currency, method, reference, paymentDate, manualAllocations optional}, POST payments/{id}/reverse {reason>=10}, GET payments/{id}, GET payments/{id}/receipt/print
- Arrears: GET arrears/by-class?year&term&asAtDate, GET by-amount?sort, GET statements/{studentId}?from&to
- Credit Notes: POST /credit-notes {invoiceId, amount, currency, reason>=10, approver} reduces balance, audit

Permissions: bursar can invoice and receipt (fees.invoices.create, fees.payments.create, fees.receipts.read), head can view (fees.invoices.read, fees.reports.read), teacher sees nothing (no fees.*). Enforced via [Authorize(Roles)] + [RequiresPermission] + TeacherAuthorizationService ensures teacher not assigned can't see fees.

## React Screens

- **FeeStructureSetup.jsx**: Add fee items + amounts to class/stream/learner, currency USD/ZWG, quantity, subtotal display via service only, save structure. Shows existing structures.
- **InvoiceGeneration.jsx**: Button Generate Invoices for Term, starts batch, polls /batches/{id} every 2s, shows progress bar total/processed/created/skipped/failed + resultJson created/skipped why (already invoiced, withdrawn, no structure). Idempotent.
- **PaymentCapture.jsx**: StudentId, amount decimal, currency, method, reference, paymentDate, load outstanding invoices oldest first, preview allocation FIFO or manual override checkbox, credit held, record payment -> receipt number + allocation. Reversal never deletion.
- **ArrearsList.jsx**: By class table student, class, invoice, balance, daysOverdue red if >60, by amount owed sorted desc total arrears, asAtDate filter, print A4 button, greyscale legible.

All screens mobile-first, 44px touch, Tailwind #0F153A, printable HTML border black.

## Tests

- FeeCalculationServiceTests: RealBalance 1 95.00 exact, 2 324.34 exact cents tricky with 100.33+100.33, 3 overpayment credit 30 exact, allocation FIFO, overpayment credit, manual override boarding first, manual sum exceeds throws, currency mismatch throws, arrears as at date filter, overdue filter, prorated daily exact 310, 270, 0, full, 333.33 rounding AwayFromZero, discount stacking percentage then fixed, no float proof 0.1+0.2=0.3 decimal.

- Integration: Invoice generation idempotent second run skips, payment reversal restores balance and audit, arrears as at reproduces three balances.

## How Mid-Term Joiner, Leaves Owing Handled

- Mid-term: default no prorating full term, bursar applies Discount with reason "Mid-term join" explicit audited. Optional daily prorating per fee_item is_proratable true: factor daysEnrolled/totalDays, amount rounded 2, note Prorated 31/90. Skipped if enrolment after term end.

- Leaves owing: Exit does not void invoices, future invoices stop (is_current false), arrears as at exit date remains 700 etc., credit stays, early withdrawal refund requires explicit CreditNote with reason approver audit reduces balance.

## Before Code Check - Three Real Balances Reproduced Exactly

All three example balances in `Fees_Calculation_Rules.md` reproduced to cent via unit tests. If any out by cent, test fails and we stop and find why (float vs decimal, rounding mode). We use decimal only, AwayFromZero, 4 decimal intermediate, 2 final.

End of fees module.
