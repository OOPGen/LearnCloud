# Online Payments - Card, Bank Transfer, Mobile Money Where Available
**Parent portal initiation against outstanding balance, webhook hostile-until-verified, receipt allocation reuse existing rules, reconciliation manual match, settlement fee reporting**

## Entities

- **PaymentGatewaySettings** tenant_id, gateway_name PayNow (supports card, bank_transfer, mobile_money, ecocash, onemoney), is_active/is_default, config_json {apiKey, merchantId, integrationId, webhookSecret, supportedMethods}, supported_methods_json, fee_percentage 2.5%, fee_fixed $0.10, currency USD, enable_card/bank/mobile_money, chosen per tenant by configuration
- **OnlinePaymentInitiation** tenant_id, student_id, guardian_id, invoice_id nullable (partial payment allowed against outstanding balance, optional specific invoice), requested_amount decimal+currency, method card/bank_transfer/mobile_money, status Initiated/Pending/Processing/Succeeded/Failed/Cancelled/Expired, idempotency_key for safe retry, client_reference LC-{tenant}-{student}-{timestamp}-{rand} our reference sent to gateway, gateway_reference poll URL/payment ID from gateway, payment_url redirect URL for card/bank, failure_reason, expires_at 30min, created_by_user_id parent user, payment_id linked after success automatic receipt
- **GatewayTransaction** tenant_id, gateway_name PayNow, gateway_transaction_id unique from gateway, provider_reference, payload_json raw webhook body, signature received, is_signature_verified bool, status received/verified/matched/unmatched/failed/duplicate, amount currency method, client_reference to match initiation, matched_initiation_id, matched_payment_id, received_at, is_replay duplicate detection, is_out_of_order webhook arrived before initiation committed, failure_reason, retry_count
- **GatewaySettlement** settlement_id from gateway payout report, settlement_date, gross_amount sum payments, fee_amount gateway fees, net_amount gross-fee payout, currency, status pending/reconciled/discrepancy, raw_data_json, reconciled_by/at
- **SettlementTransaction** settlement_id FK, gateway_transaction_id, payment_id, amount, fee, net, currency, is_matched

## IPaymentGateway Abstraction

**Interface:**
- GatewayName
- InitiatePaymentAsync(InitiatePaymentRequest tenantId studentId guardianId amount currency method clientReference idempotencyKey description returnUrl cancelUrl customerInfo) → InitiatePaymentResult success, paymentUrl redirect, gatewayReference, failureReason, qrCode for mobile money USSD
- VerifyWebhookAsync(VerifyWebhookRequest payload signature headers) → VerifyWebhookResult isValid, failureReason, gatewayTransactionId, amount, currency, status succeeded/failed/pending, clientReference, method, providerReference, isReplay
- CalculateFeeAsync(amount, method) → fee
- SupportsMethod(method) card/bank_transfer/mobile_money/ecocash/onemoney

**Concrete Implementation PayNowGateway:**
- Supports card, bank_transfer, mobile_money, ecocash, onemoney, innbucks per Zimbabwe
- Initiate: builds PayNow payload id reference amount additionalinfo returnurl resulturl authemail status Message, hash SHA512(id+reference+amount+additionalinfo+returnurl+resulturl+authemail+status+integrationKey), POST to https://www.paynow.co.zw/interface/initiatetransaction (mocked for V1 with delay 100ms, gatewayRef PAYNOW-xxx, paymentUrl https://www.paynow.co.zw/interface/payment?guid=xxx)
- Verify: Treat every webhook as hostile until verified - parses payload form-url-encoded or JSON, verifies hash SHA512 values+integrationKey or HMACSHA256 payload integrationKey, if missing signature and required fields and dev mode allow with warning, else invalid signature hostile rejected, extracts gatewayTransactionId paynowreference, clientReference reference, amount, status Paid/Awaiting/Failed mapped to succeeded/pending/failed, method
- Fee: card 2.5% + $0.10, bank 1.5%, mobile 2%
- Never logs secrets, logs truncated

**Factory IPaymentGatewayFactory:** GetGatewayAsync(tenantId) reads PaymentGatewaySettings where is_active default, gatewayName switch resolves via DI, GetGatewayByNameAsync, so provider can be swapped via settings row update without touching calling code.

## Payment Initiation From Parent Portal Against Outstanding Balance, Partial Allowed

- Endpoint POST /api/parent/payments/initiate {studentId, amount, currency, method, invoiceId optional, returnUrl, cancelUrl} → verifies guardian is linked to student via GuardianStudentLink, validates amount >0 and <= total outstanding (invoices sum balanceDue), if invoiceId provided validates invoice belongs to student and amount <= invoice balanceDue, idempotencyKey Guid new, clientReference LC-{tenant}-{student}-{timestamp}-{rand}, creates OnlinePaymentInitiation status Initiated, saves BEFORE calling gateway (to handle out-of-order webhook arriving before commit - see test), calls gateway InitiatePaymentAsync, if fail sets Failed, else sets GatewayReference PaymentUrl Pending, saves, logs, returns initiation with paymentUrl for redirect.

- Partial payment allowed: amount can be less than outstanding, e.g. outstanding 500, pay 200 partial, allocation will allocate oldest first via existing rules.

- PaymentUrl: for card/bank transfer/mobile money, parent redirects to gateway, after payment gateway redirects to ReturnUrl parent portal /parent/payments/return?ref=xxx, parent may close browser before return - webhook still allocates.

## Webhook Handling Idempotent, Signature-Verified, Safe Against Replay and Out-of-Order

- Endpoint POST /api/webhooks/payments/{gatewayName} AllowAnonymous - treats every webhook as hostile until verified
- Reads raw body, signature from header X-Paynow-Signature or X-Signature, headers dict
- Calls gateway VerifyWebhookAsync - SHA512 or HMACSHA256 verification, if invalid logs warning hostile rejected, stores as failed transaction for audit but does not process, returns 401
- Idempotent: checks if gateway_transaction_id already exists in GatewayTransaction table unique (tenant_id, gateway_transaction_id) → if exists, marks IsReplay true, returns existing transaction, does not create duplicate payment (duplicate webhooks test)
- Safe against out-of-order: webhook may arrive before initiation record committed (race condition). We create GatewayTransaction with matched_initiation_id null, is_out_of_order true, status unmatched, store payload. Later when initiation is created, second webhook or retry will match, or manual reconciliation can match. In ProcessSuccessfulPaymentAsync, if initiation null, tries again to find initiation by clientReference, if found now matched, updates IsOutOfOrder false.
- If succeeded, automatic receipt generation and allocation to invoice lines using existing allocation rules - never separate code path: calls FeeCalculationService.AllocatePayment with outstanding invoices FIFO, creates Fees.Payment with reference gatewayTransactionId, receipt number from ReceiptSequence FOR UPDATE, amount, currency, method Card, creates PaymentAllocation rows via existing logic, updates invoice amount_paid balance_due status paid/partial, creates LearnerCredit if overpayment credit. Same code path as manual capture - manual capture also uses FeeCalculationService.AllocatePayment, so no separate path.

## Automatic Receipt Generation and Allocation Using Existing Rules

- ProcessSuccessfulPaymentAsync: after signature verified and idempotency checked, finds initiation by clientReference, if not found leaves unmatched for manual reconciliation (out-of-order safe)
- Checks idempotent payment creation: if payment with reference gatewayTransactionId already exists, return existing, do not duplicate
- Gets outstanding invoices sorted FIFO due_date ASC issue_date ASC invoice_number ASC, calls _calc.AllocatePayment(paymentAmount, currency, outstanding, null) - same method used by manual PaymentService.RecordPaymentAsync
- Creates Fees.Payment with reference gatewayTransactionId, receiptNumber from sequence, amount, currency, method, status Confirmed, creates PaymentAllocation rows, updates invoice balances, creates LearnerCredit if credit>0
- Links gateway transaction matched_payment_id, status matched, initiation status Succeeded
- Logs

## Reconciliation Screen Showing Gateway Transactions Against Recorded Payments, Unmatched Highlighted and Manual Match Action

- Endpoint GET /api/bursar/payments/reconciliation?from&to - returns gateway transactions with status unmatched/received, and matched list, total count
- Frontend BursarReconciliation component: date filters from/to, Load Unmatched button, table gatewayTxId, amount, clientReference, status, isOutOfOrder/isReplay badges, action Manual Match button prompts Enter Payment ID to manually match - calls POST /api/bursar/payments/reconciliation/{gatewayTxId}/match/{paymentId} which sets matched_payment_id, status matched
- Unmatched highlighted bg-warning-50, out-of-order bg-primary-50
- Failed and pending states surfaced clearly to parent, with retry

## Failed and Pending States Surfaced Clearly to Parent, With Retry

- Parent portal GET /api/parent/payments?studentId and GET /{id} returns initiation status Initiated/Pending/Processing/Succeeded/Failed/Cancelled/Expired
- Frontend ParentOnlinePayment component shows status badge color: Succeeded green, Failed danger, Pending warning, Initiated neutral, displays gatewayReference, failureReason, paymentUrl Open Payment URL button, Check Status button polls GET, Retry button for Failed/Cancelled - POST /{id}/retry re-initiates with same amount new clientReference
- Initiation expires_at 30 min - if not paid by then status Expired

## Manual Capture Retained for Cash, Bank Deposit and Off-Platform Payments

- Existing Fees module PaymentService.RecordPaymentAsync for cash/bank_transfer/ecocash manual capture remains, same Payment table, same allocation FIFO rules via FeeCalculationService - online and manual both use same table and same allocation logic, never separate code path, so bursar sees both in same reconciliation?

## Settlement and Fee Reporting

- GatewaySettlement settlement_id from gateway payout report (e.g., PayNow settlement file), settlement_date, gross_amount sum payments, fee_amount gateway fees, net_amount gross-fee payout, currency, status pending/reconciled/discrepancy, raw_data_json
- SettlementTransaction settlement_id, gateway_transaction_id, payment_id, amount, fee, net, is_matched
- Endpoint GET /api/bursar/settlements?from&to returns SettlementReportDto settlementDate gross fee net currency transactionsCount status
- Bursar can reconcile gateway payout net amount against receipts: gross - fee = net, compare net to bank statement credit, if discrepancy flag
- Frontend SettlementReporting table settlementDate gross fee net payout currency tx count status

## Tests for Hostile Webhooks

- Duplicate webhooks idempotent same transaction returned: first webhook creates payment and allocation, second same gatewayTransactionId detected as existing, marked IsReplay true, returns existing, only one payment created (Assert.Single payments)
- Webhook arriving before initiation committed out-of-order safe: simulate webhook with clientReference not yet in DB, service creates gateway transaction status unmatched isOutOfOrder true, does not crash, later initiation created with same clientReference, second webhook or retry finds initiation and matches, payment created
- Payment succeeding after parent closed browser still allocated: parent initiates then closes browser (no polling), webhook arrives later, ProcessSuccessfulPaymentAsync still creates payment and allocates via existing FIFO rules, invoice balance 0 status paid, allocations created, initiation status Succeeded even though parent closed browser - parent can later check status via GET and see Succeeded

## Security

- Every webhook treated as hostile until verified: signature verification SHA512/HMACSHA256, failure returns 401 invalid signature - hostile webhook rejected, stored as failed for audit but not processed
- Replay safe: unique index (tenant_id, gateway_transaction_id) prevents duplicate, IsReplay flag
- Out-of-order safe: stores unmatched and tries to match later, isOutOfOrder flag
- Idempotency key for initiation safe retry
- No secrets logged, only truncated
- Tenant filter + guardian-child link enforced for parent initiation and parent payments list

## Migration

- V10_OnlinePayments.sql: payment_gateway_settings tenant gateway_name is_active default supported_methods_json fee_percentage fee_fixed currency, online_payment_initiations tenant student guardian invoice amount currency method status Initiated/Pending/Succeeded/Failed idempotency_key client_reference unique gateway_reference payment_url failureReason expiresAt paymentId, gateway_transactions gateway_name gateway_transaction_id unique provider_reference payload_json signature is_signature_verified status amount currency method client_reference matched_initiation_id matched_payment_id received_at is_replay is_out_of_order, gateway_settlements settlement_id settlement_date gross fee net currency status, settlement_transactions, seed PayNow default supporting card/bank_transfer/mobile_money/ecocash/onemoney

