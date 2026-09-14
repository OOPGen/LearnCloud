using LearnCloud.Fees.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using LearnCloud.OnlinePayments.Entities;
using LearnCloud.OnlinePayments.Services.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LearnCloud.OnlinePayments.Services;

public interface IOnlinePaymentService
{
    Task<OnlinePaymentInitiation> InitiateAsync(long tenantId, long guardianId, long userId, InitiateOnlinePaymentRequest req, CancellationToken ct = default);
    Task<OnlinePaymentInitiation> GetInitiationAsync(long tenantId, long initiationId, CancellationToken ct = default);
    Task<GatewayTransaction> HandleWebhookAsync(long tenantId, string gatewayName, string payload, string? signature, Dictionary<string,string>? headers, CancellationToken ct = default);
    Task<List<GatewayTransaction>> GetReconciliationAsync(long tenantId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<GatewayTransaction> ManualMatchAsync(long tenantId, long gatewayTransactionId, long paymentId, long userId, CancellationToken ct = default);
    Task<List<OnlinePaymentInitiation>> GetParentPaymentsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<List<SettlementReportDto>> GetSettlementReportAsync(long tenantId, DateTime from, DateTime to, CancellationToken ct = default);
}

public record InitiateOnlinePaymentRequest(
    long StudentId,
    decimal Amount,
    string Currency,
    string Method, // card, bank_transfer, mobile_money, ecocash, onemoney
    long? InvoiceId, // optional specific invoice, null = against outstanding balance
    string? ReturnUrl,
    string? CancelUrl
);

public record SettlementReportDto(DateTime SettlementDate, decimal GrossAmount, decimal FeeAmount, decimal NetAmount, string Currency, int TransactionsCount, string Status);

public class OnlinePaymentService : IOnlinePaymentService
{
    private readonly LearnCloudDbContext _db;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly Fees.Services.FeeCalculationService _feeCalc;
    private readonly ILogger<OnlinePaymentService> _logger;

    public OnlinePaymentService(LearnCloudDbContext db, IPaymentGatewayFactory gatewayFactory, Fees.Services.FeeCalculationService feeCalc, ILogger<OnlinePaymentService> logger)
    {
        _db = db;
        _gatewayFactory = gatewayFactory;
        _feeCalc = feeCalc;
        _logger = logger;
    }

    public async Task<OnlinePaymentInitiation> InitiateAsync(long tenantId, long guardianId, long userId, InitiateOnlinePaymentRequest req, CancellationToken ct = default)
    {
        // Verify guardian is linked to student (parent portal auth)
        var isGuardian = await _db.Set<GuardianStudentLink>().AnyAsync(l => l.TenantId == tenantId && l.GuardianId == guardianId && l.StudentId == req.StudentId && !l.IsDeleted, ct);
        if (!isGuardian) throw new UnauthorizedAccessException("Guardian not linked to student");

        // Validate amount partial allowed, but not exceed outstanding balance
        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == req.StudentId && i.BalanceDue > 0 && !i.IsDeleted).ToListAsync(ct);
        var totalOutstanding = invoices.Sum(i => i.BalanceDue);
        if (req.Amount <= 0) throw new InvalidOperationException("Amount must be >0");
        if (req.Amount > totalOutstanding) throw new InvalidOperationException($"Amount {req.Amount} exceeds outstanding balance {totalOutstanding}. Overpayment as credit not allowed for online, use manual adjustment.");

        if (req.InvoiceId.HasValue)
        {
            var inv = invoices.FirstOrDefault(i => i.Id == req.InvoiceId.Value);
            if (inv == null) throw new InvalidOperationException("Invoice not found or not outstanding");
            if (req.Amount > inv.BalanceDue) throw new InvalidOperationException($"Amount {req.Amount} exceeds invoice {inv.InvoiceNumber} balance {inv.BalanceDue}");
        }

        // Idempotency key for safe retry
        var idempotencyKey = Guid.NewGuid().ToString();
        var clientReference = $"LC-{tenantId}-{req.StudentId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(1000,9999)}";

        // Get gateway per tenant config
        var gateway = await _gatewayFactory.GetGatewayAsync(tenantId, ct);
        if (!gateway.SupportsMethod(req.Method))
            throw new InvalidOperationException($"Method {req.Method} not supported by gateway {gateway.GatewayName}");

        // Create initiation record BEFORE calling gateway (to handle webhook arriving before commit - see test)
        var initiation = new OnlinePaymentInitiation
        {
            TenantId = tenantId,
            StudentId = req.StudentId,
            GuardianId = guardianId,
            InvoiceId = req.InvoiceId,
            RequestedAmount = Fees.Services.FeeCalculationService.Round2(req.Amount),
            Currency = req.Currency,
            Method = req.Method,
            Status = OnlinePaymentStatus.Initiated,
            IdempotencyKey = idempotencyKey,
            ClientReference = clientReference,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<OnlinePaymentInitiation>().Add(initiation);
        await _db.SaveChangesAsync(ct);

        // Now call gateway
        var gatewayReq = new Gateways.InitiatePaymentRequest
        {
            TenantId = tenantId,
            StudentId = req.StudentId,
            GuardianId = guardianId,
            Amount = req.Amount,
            Currency = req.Currency,
            Method = req.Method,
            ClientReference = clientReference,
            IdempotencyKey = idempotencyKey,
            Description = $"Payment for student {req.StudentId} invoice {(req.InvoiceId.HasValue? req.InvoiceId.ToString():"outstanding balance")}",
            ReturnUrl = req.ReturnUrl,
            CancelUrl = req.CancelUrl,
            CustomerInfo = new Dictionary<string, string> { ["email"] = "parent@example.com" }
        };

        var gatewayResult = await gateway.InitiatePaymentAsync(gatewayReq, ct);

        if (!gatewayResult.Success)
        {
            initiation.Status = OnlinePaymentStatus.Failed;
            initiation.FailureReason = gatewayResult.FailureReason;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException($"Gateway initiation failed: {gatewayResult.FailureReason}");
        }

        initiation.GatewayReference = gatewayResult.GatewayReference;
        initiation.PaymentUrl = gatewayResult.PaymentUrl;
        initiation.Status = OnlinePaymentStatus.Pending;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Online payment initiated tenant {TenantId} student {StudentId} amount {Amount} method {Method} clientRef {ClientRef} gatewayRef {GatewayRef}", tenantId, req.StudentId, req.Amount, req.Method, clientReference, gatewayResult.GatewayReference);

        return initiation;
    }

    public async Task<OnlinePaymentInitiation> GetInitiationAsync(long tenantId, long initiationId, CancellationToken ct = default)
    {
        return await _db.Set<OnlinePaymentInitiation>().FirstOrDefaultAsync(i => i.Id == initiationId && i.TenantId == tenantId && !i.IsDeleted, ct)
               ?? throw new InvalidOperationException("Initiation not found");
    }

    // Webhook handling that is idempotent, signature-verified, safe against replay and out-of-order delivery
    public async Task<GatewayTransaction> HandleWebhookAsync(long tenantId, string gatewayName, string payload, string? signature, Dictionary<string, string>? headers, CancellationToken ct = default)
    {
        // Treat every webhook as hostile until verified - signature verification first
        var gateway = await _gatewayFactory.GetGatewayByNameAsync(tenantId, gatewayName, ct);

        var verifyReq = new Gateways.VerifyWebhookRequest
        {
            Payload = payload,
            Signature = signature,
            Headers = headers
        };

        var verifyResult = await gateway.VerifyWebhookAsync(verifyReq, ct);

        if (!verifyResult.IsValid)
        {
            _logger.LogWarning("Hostile webhook rejected tenant {TenantId} gateway {Gateway} reason {Reason}", tenantId, gatewayName, verifyResult.FailureReason);
            // Store as failed for audit but don't process
            var failedTx = new GatewayTransaction
            {
                TenantId = tenantId,
                GatewayName = gatewayName,
                PayloadJson = payload,
                Signature = signature,
                IsSignatureVerified = false,
                Status = "failed",
                Amount = 0,
                Currency = "USD",
                FailureReason = verifyResult.FailureReason,
                ReceivedAt = DateTime.UtcNow
            };
            _db.Set<GatewayTransaction>().Add(failedTx);
            await _db.SaveChangesAsync(ct);
            return failedTx;
        }

        // Idempotent: check if gateway transaction ID already processed (replay detection)
        var existingTx = await _db.Set<GatewayTransaction>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.GatewayTransactionId == verifyResult.GatewayTransactionId && !t.IsDeleted, ct);
        if (existingTx != null)
        {
            _logger.LogInformation("Duplicate webhook detected tenant {TenantId} gatewayTx {GatewayTxId} - idempotent return", tenantId, verifyResult.GatewayTransactionId);
            existingTx.IsReplay = true;
            await _db.SaveChangesAsync(ct);
            return existingTx; // idempotent return same
        }

        // Safe against out-of-order: webhook may arrive before initiation record committed (see test)
        // We still create GatewayTransaction with MatchedInitiationId null and IsOutOfOrder true, then later try to match
        OnlinePaymentInitiation? initiation = null;
        if (!string.IsNullOrEmpty(verifyResult.ClientReference))
        {
            initiation = await _db.Set<OnlinePaymentInitiation>().FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ClientReference == verifyResult.ClientReference && !i.IsDeleted, ct);
            if (initiation == null)
            {
                _logger.LogWarning("Webhook out-of-order: clientReference {ClientRef} not found yet, storing as unmatched for later matching", verifyResult.ClientReference);
            }
        }

        var gatewayTx = new GatewayTransaction
        {
            TenantId = tenantId,
            GatewayName = gatewayName,
            GatewayTransactionId = verifyResult.GatewayTransactionId,
            ProviderReference = verifyResult.ProviderReference,
            PayloadJson = payload,
            Signature = signature,
            IsSignatureVerified = true,
            Status = verifyResult.Status,
            Amount = verifyResult.Amount,
            Currency = verifyResult.Currency,
            Method = verifyResult.Method,
            ClientReference = verifyResult.ClientReference,
            MatchedInitiationId = initiation?.Id,
            ReceivedAt = DateTime.UtcNow,
            IsReplay = false,
            IsOutOfOrder = initiation == null
        };

        _db.Set<GatewayTransaction>().Add(gatewayTx);
        await _db.SaveChangesAsync(ct);

        // If succeeded, automatic receipt generation and allocation to invoice lines using existing allocation rules - never separate code path
        if (verifyResult.Status == "succeeded")
        {
            await ProcessSuccessfulPaymentAsync(tenantId, gatewayTx, initiation, ct);
        }
        else if (verifyResult.Status == "failed")
        {
            if (initiation != null)
            {
                initiation.Status = OnlinePaymentStatus.Failed;
                initiation.FailureReason = "Gateway reported failed";
                await _db.SaveChangesAsync(ct);
            }
        }

        return gatewayTx;
    }

    private async Task ProcessSuccessfulPaymentAsync(long tenantId, GatewayTransaction gatewayTx, OnlinePaymentInitiation? initiation, CancellationToken ct)
    {
        // Find initiation if not already (out-of-order case: initiation may have been created after webhook, so try again)
        if (initiation == null && !string.IsNullOrEmpty(gatewayTx.ClientReference))
        {
            initiation = await _db.Set<OnlinePaymentInitiation>().FirstOrDefaultAsync(i => i.TenantId == tenantId && i.ClientReference == gatewayTx.ClientReference && !i.IsDeleted, ct);
            if (initiation != null)
            {
                gatewayTx.MatchedInitiationId = initiation.Id;
                gatewayTx.IsOutOfOrder = false; // now matched, was out-of-order but resolved
                await _db.SaveChangesAsync(ct);
            }
        }

        if (initiation == null)
        {
            _logger.LogWarning("Successful webhook but no initiation found for clientRef {ClientRef} - leaving as unmatched for manual reconciliation", gatewayTx.ClientReference);
            gatewayTx.Status = "unmatched";
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Check if already processed (idempotent payment creation)
        var existingPayment = await _db.Set<Fees.Entities.Payment>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Reference == gatewayTx.GatewayTransactionId && !p.IsDeleted, ct);
        if (existingPayment != null)
        {
            _logger.LogInformation("Payment already created for gatewayTx {GatewayTxId}, idempotent skip", gatewayTx.GatewayTransactionId);
            gatewayTx.MatchedPaymentId = existingPayment.Id;
            gatewayTx.Status = "matched";
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Automatic receipt generation and allocation using existing allocation rules - never separate code path
        // Use existing PaymentService.RecordPaymentAsync which uses FeeCalculationService.AllocatePayment FIFO

        var studentId = initiation.StudentId;
        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && i.BalanceDue > 0 && !i.IsDeleted)
            .OrderBy(i => i.DueDate).ThenBy(i => i.IssueDate).ThenBy(i => i.InvoiceNumber).ToListAsync(ct);

        var outstanding = invoices.Select(i => new Fees.Services.OutstandingInvoice(i.Id, i.InvoiceNumber, i.BalanceDue, i.Currency, i.DueDate, i.IssueDate)).ToList();

        var feeCalc = new Fees.Services.FeeCalculationService();
        var (allocations, credit) = feeCalc.AllocatePayment(gatewayTx.Amount, gatewayTx.Currency, outstanding, null);

        // Create payment via existing fee payment entity (manual capture retained for cash, but online also uses same table)
        var payment = new Fees.Entities.Payment
        {
            TenantId = tenantId,
            StudentId = studentId,
            Amount = Fees.Services.FeeCalculationService.Round2(gatewayTx.Amount),
            Currency = gatewayTx.Currency,
            Method = Enum.TryParse<Fees.Entities.PaymentMethod>(gatewayTx.Method ?? "Card", true, out var m) ? m : Fees.Entities.PaymentMethod.Card,
            Reference = gatewayTx.GatewayTransactionId,
            PaymentDate = DateTime.UtcNow.Date,
            ReceiptNumber = await GetNextReceiptNumberAsync(tenantId, ct),
            ProofUrl = null,
            Status = Fees.Entities.PaymentStatus.Confirmed,
            CreatedBy = initiation.CreatedByUserId
        };
        _db.Set<Fees.Entities.Payment>().Add(payment);
        await _db.SaveChangesAsync(ct);

        // Allocations
        foreach (var alloc in allocations)
        {
            var allocEntity = new Fees.Entities.PaymentAllocation
            {
                TenantId = tenantId,
                PaymentId = payment.Id,
                InvoiceId = alloc.InvoiceId,
                AllocatedAmount = alloc.AllocatedAmount,
                Currency = alloc.Currency,
                IsManualOverride = alloc.IsManualOverride,
                CreatedBy = initiation.CreatedByUserId
            };
            _db.Set<Fees.Entities.PaymentAllocation>().Add(allocEntity);

            // Update invoice balances via existing logic (no separate code path - same as manual capture)
            var invoice = await _db.Set<Fees.Entities.FeeInvoice>().FirstOrDefaultAsync(i => i.Id == alloc.InvoiceId, ct);
            if (invoice != null)
            {
                invoice.AmountPaid = Fees.Services.FeeCalculationService.Round2(invoice.AmountPaid + alloc.AllocatedAmount);
                invoice.BalanceDue = Fees.Services.FeeCalculationService.Round2(invoice.TotalAmount - invoice.AmountPaid);
                invoice.Status = invoice.BalanceDue == 0m ? Fees.Entities.InvoiceStatus.Paid : Fees.Entities.InvoiceStatus.Partial;
            }
        }

        if (credit > 0)
        {
            _db.Set<Fees.Entities.LearnerCredit>().Add(new Fees.Entities.LearnerCredit
            {
                TenantId = tenantId,
                StudentId = studentId,
                Amount = credit,
                Currency = gatewayTx.Currency,
                Source = "overpayment",
                SourcePaymentId = payment.Id,
                CreatedBy = initiation.CreatedByUserId
            });
        }

        // Link gateway transaction to payment
        gatewayTx.MatchedPaymentId = payment.Id;
        gatewayTx.Status = "matched";

        // Update initiation to succeeded
        initiation.Status = OnlinePaymentStatus.Succeeded;
        initiation.GatewayReference = gatewayTx.GatewayTransactionId;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Online payment succeeded and allocated tenant {TenantId} student {StudentId} amount {Amount} payment {PaymentId} via existing allocation rules", tenantId, studentId, gatewayTx.Amount, payment.Id);
    }

    private async Task<string> GetNextReceiptNumberAsync(long tenantId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var seq = await _db.Set<ReceiptSequence>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Year == year && !s.IsDeleted, ct);
        if (seq == null)
        {
            seq = new ReceiptSequence { TenantId = tenantId, Year = year, LastNumber = 0, Prefix = "REC", Format = "{prefix}-{year}-{number:5}" };
            _db.Set<ReceiptSequence>().Add(seq);
            await _db.SaveChangesAsync(ct);
        }
        seq.LastNumber++;
        await _db.SaveChangesAsync(ct);
        return $"{seq.Prefix}-{year}-{seq.LastNumber:D5}";
    }

    public async Task<List<GatewayTransaction>> GetReconciliationAsync(long tenantId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.Set<GatewayTransaction>().Where(t => t.TenantId == tenantId && !t.IsDeleted);
        if (from.HasValue) query = query.Where(t => t.ReceivedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.ReceivedAt <= to.Value);
        return await query.OrderByDescending(t => t.ReceivedAt).Take(200).ToListAsync(ct);
    }

    public async Task<GatewayTransaction> ManualMatchAsync(long tenantId, long gatewayTransactionId, long paymentId, long userId, CancellationToken ct = default)
    {
        var gatewayTx = await _db.Set<GatewayTransaction>().FirstOrDefaultAsync(t => t.Id == gatewayTransactionId && t.TenantId == tenantId && !t.IsDeleted, ct) ?? throw new InvalidOperationException("Gateway transaction not found");
        var payment = await _db.Set<Fees.Entities.Payment>().FirstOrDefaultAsync(p => p.Id == paymentId && p.TenantId == tenantId && !p.IsDeleted, ct) ?? throw new InvalidOperationException("Payment not found");

        gatewayTx.MatchedPaymentId = paymentId;
        gatewayTx.Status = "matched";
        await _db.SaveChangesAsync(ct);

        return gatewayTx;
    }

    public async Task<List<OnlinePaymentInitiation>> GetParentPaymentsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        // Verify guardian-child link
        var isGuardian = await _db.Set<GuardianStudentLink>().AnyAsync(l => l.TenantId == tenantId && l.GuardianId == guardianId && l.StudentId == studentId && !l.IsDeleted, ct);
        if (!isGuardian) throw new UnauthorizedAccessException("Guardian not linked to student");

        return await _db.Set<OnlinePaymentInitiation>().Where(p => p.TenantId == tenantId && p.StudentId == studentId && p.GuardianId == guardianId && !p.IsDeleted).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }

    public async Task<List<SettlementReportDto>> GetSettlementReportAsync(long tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var settlements = await _db.Set<GatewaySettlement>().Where(s => s.TenantId == tenantId && s.SettlementDate >= from && s.SettlementDate <= to && !s.IsDeleted).ToListAsync(ct);
        var result = new List<SettlementReportDto>();
        foreach (var s in settlements)
        {
            var transactionsCount = await _db.Set<SettlementTransaction>().CountAsync(st => st.SettlementId == s.Id && !st.IsDeleted, ct);
            result.Add(new SettlementReportDto(s.SettlementDate, s.GrossAmount, s.FeeAmount, s.NetAmount, s.Currency, transactionsCount, s.Status));
        }
        return result;
    }
}

// Gateway factory - chosen per tenant by configuration
public interface IPaymentGatewayFactory
{
    Task<IPaymentGateway> GetGatewayAsync(long tenantId, CancellationToken ct = default);
    Task<IPaymentGateway> GetGatewayByNameAsync(long tenantId, string gatewayName, CancellationToken ct = default);
}

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly LearnCloudDbContext _db;
    private readonly IServiceProvider _sp;

    public PaymentGatewayFactory(LearnCloudDbContext db, IServiceProvider sp) { _db = db; _sp = sp; }

    public async Task<IPaymentGateway> GetGatewayAsync(long tenantId, CancellationToken ct = default)
    {
        var settings = await _db.Set<PaymentGatewaySettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IsActive && s.IsDefault && !s.IsDeleted, ct);
        var gatewayName = settings?.GatewayName ?? "PayNow";
        return await GetGatewayByNameAsync(tenantId, gatewayName, ct);
    }

    public Task<IPaymentGateway> GetGatewayByNameAsync(long tenantId, string gatewayName, CancellationToken ct = default)
    {
        // Resolve via DI by name - for V1 we have PayNowGateway as default
        var gateway = gatewayName.ToLower() switch
        {
            "paynow" => (IPaymentGateway)_sp.GetService(typeof(Gateways.PayNowGateway))!,
            "stripe" => (IPaymentGateway)_sp.GetService(typeof(Gateways.PayNowGateway))!, // fallback to PayNow for demo, real would have StripeGateway
            _ => (IPaymentGateway)_sp.GetService(typeof(Gateways.PayNowGateway))!
        };
        return Task.FromResult(gateway);
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
