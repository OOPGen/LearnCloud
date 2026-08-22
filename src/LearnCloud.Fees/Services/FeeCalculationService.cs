namespace LearnCloud.Fees.Services;

/// <summary>
/// SINGLE SOURCE OF TRUTH FOR ALL MONETARY ARITHMETIC
/// No other file may contain + - * / % on money. CI grep enforces.
/// All decimal(18,2) + explicit currency, MidpointRounding.AwayFromZero
/// Money bugs cost customers permanently.
/// </summary>
public class FeeCalculationService
{
    private const int MONEY_SCALE = 2;
    private const int INTERMEDIATE_SCALE = 4;

    // Invoice totals: subtotal = sum line totals, discount_total = sum discounts (capped), total = subtotal - discount
    public (decimal subtotal, decimal discountTotal, decimal total) CalculateInvoiceTotals(
        List<decimal> lineTotals, // each already rounded 2
        List<(decimal value, bool isPercentage)> discounts, // value = 10 for 10% or fixed amount
        string currency)
    {
        // All amounts must be same currency - caller ensures
        var subtotal = Round2(lineTotals.Sum());

        decimal discountTotal = 0m;
        decimal remainingSubtotal = subtotal;

        // Rule: percentage discounts first, then fixed, each rounded
        var percentageDiscounts = discounts.Where(d => d.isPercentage).ToList();
        var fixedDiscounts = discounts.Where(d => !d.isPercentage).ToList();

        foreach (var disc in percentageDiscounts)
        {
            // discount = remainingSubtotal * percentage /100
            var raw = remainingSubtotal * disc.value / 100m;
            var rounded = Round2(raw);
            // Cap to remaining
            if (rounded > remainingSubtotal) rounded = remainingSubtotal;
            discountTotal += rounded;
            discountTotal = Round2(discountTotal);
            remainingSubtotal -= rounded;
            remainingSubtotal = Round2(remainingSubtotal);
            if (remainingSubtotal < 0) remainingSubtotal = 0m;
        }

        foreach (var disc in fixedDiscounts)
        {
            var rounded = Round2(disc.value);
            if (rounded > remainingSubtotal) rounded = remainingSubtotal;
            discountTotal += rounded;
            discountTotal = Round2(discountTotal);
            remainingSubtotal -= rounded;
            remainingSubtotal = Round2(remainingSubtotal);
            if (remainingSubtotal < 0) remainingSubtotal = 0m;
        }

        if (discountTotal > subtotal) discountTotal = subtotal;

        var total = Round2(subtotal - discountTotal);
        if (total < 0) total = 0m;

        return (subtotal, discountTotal, total);
    }

    public decimal CalculateDiscountAmount(decimal subtotal, decimal discountValue, bool isPercentage)
    {
        if (isPercentage)
        {
            var raw = subtotal * discountValue / 100m;
            return Round2(raw);
        }
        else
        {
            return Round2(discountValue);
        }
    }

    // Part-payment allocation: oldest invoice first by default, manual override
    public (List<AllocationResult> allocations, decimal credit) AllocatePayment(
        decimal paymentAmount,
        string paymentCurrency,
        List<OutstandingInvoice> outstandingInvoicesSortedFifo, // must be sorted due_date ASC, issue_date ASC, invoice_number ASC
        List<ManualAllocation>? manualAllocations = null)
    {
        if (paymentAmount < 0) throw new ArgumentException("Payment amount cannot be negative");

        paymentAmount = Round2(paymentAmount);

        // Validate currencies
        foreach (var inv in outstandingInvoicesSortedFifo)
        {
            if (inv.Currency != paymentCurrency)
                throw new InvalidOperationException($"Currency mismatch: payment {paymentCurrency} cannot allocate to invoice {inv.InvoiceNumber} {inv.Currency}");
        }

        if (manualAllocations != null && manualAllocations.Count > 0)
        {
            // Manual override path
            var manualSum = Round2(manualAllocations.Sum(m => m.Amount));
            if (manualSum > paymentAmount)
                throw new InvalidOperationException($"Manual allocation sum {manualSum} exceeds payment {paymentAmount}");

            foreach (var ma in manualAllocations)
            {
                if (ma.Currency != paymentCurrency)
                    throw new InvalidOperationException($"Manual allocation currency mismatch {ma.Currency} vs {paymentCurrency}");
                var inv = outstandingInvoicesSortedFifo.FirstOrDefault(i => i.InvoiceId == ma.InvoiceId) ?? throw new InvalidOperationException($"Manual allocation invoice {ma.InvoiceId} not in outstanding list or not belong to learner");
                if (ma.Amount > inv.BalanceDue)
                    throw new InvalidOperationException($"Manual allocation {ma.Amount} exceeds invoice {inv.InvoiceNumber} balance {inv.BalanceDue}");
            }

            var allocations = manualAllocations.Select(ma => new AllocationResult(ma.InvoiceId, Round2(ma.Amount), ma.Currency, true)).ToList();
            var credit = Round2(paymentAmount - manualSum);
            return (allocations, credit);
        }
        else
        {
            // FIFO default
            decimal remaining = paymentAmount;
            var allocations = new List<AllocationResult>();

            foreach (var inv in outstandingInvoicesSortedFifo)
            {
                if (remaining <= 0) break;
                var toAllocate = Math.Min(remaining, inv.BalanceDue);
                toAllocate = Round2(toAllocate);
                if (toAllocate > 0)
                {
                    allocations.Add(new AllocationResult(inv.InvoiceId, toAllocate, paymentCurrency, false));
                    remaining = Round2(remaining - toAllocate);
                }
            }

            var credit = Round2(remaining);
            return (allocations, credit);
        }
    }

    // Mid-term joiner prorated calculation
    public decimal CalculateProratedAmount(decimal originalAmount, int daysEnrolled, int totalDays)
    {
        if (totalDays <= 0) throw new ArgumentException("Total days must be >0");
        if (daysEnrolled <= 0) return 0m;
        if (daysEnrolled >= totalDays) return Round2(originalAmount);

        // Factor with 4 decimal intermediate
        decimal factor = (decimal)daysEnrolled / (decimal)totalDays;
        var raw = originalAmount * factor;
        return Round2(raw);
    }

    // Arrears as at date: sum invoice balances where issue_date <= asAtDate and due_date <= asAtDate, minus payments allocated with payment_date <= asAtDate
    // For V1 simplified: if we have current balances, we filter due_date <= asAtDate and sum balance_due
    // For historical exact: need to compute from allocations with payment_date <= asAtDate
    public decimal ComputeArrearsAsAt(
        List<InvoiceForArrears> invoices,
        List<PaymentAllocationForArrears> allocations, // allocations with payment_date
        DateTime asAtDate)
    {
        // Invoices issued up to asAtDate
        var relevantInvoices = invoices.Where(i => i.IssueDate.Date <= asAtDate.Date).ToList();
        var totalInvoiced = Round2(relevantInvoices.Sum(i => i.TotalAmount));

        // Payments allocated up to asAtDate
        var relevantAllocations = allocations.Where(a => a.PaymentDate.Date <= asAtDate.Date).ToList();
        var totalPaid = Round2(relevantAllocations.Sum(a => a.AllocatedAmount));

        var arrears = Round2(totalInvoiced - totalPaid);
        if (arrears < 0) arrears = 0m; // overpayment becomes credit, not negative arrears

        // Also filter by due_date <= asAtDate for overdue arrears? For this method we compute total arrears as at (all issued), then caller can filter overdue separately
        // For overdue arrears: filter invoices where DueDate <= asAtDate
        return arrears;
    }

    public decimal ComputeOverdueArrearsAsAt(
        List<InvoiceForArrears> invoices,
        List<PaymentAllocationForArrears> allocations,
        DateTime asAtDate)
    {
        var relevantInvoices = invoices.Where(i => i.IssueDate.Date <= asAtDate.Date && i.DueDate.Date <= asAtDate.Date).ToList();
        var totalInvoiced = Round2(relevantInvoices.Sum(i => i.TotalAmount));

        // Allocations for those relevant invoices up to asAtDate
        var relevantInvoiceIds = relevantInvoices.Select(i => i.InvoiceId).ToHashSet();
        var relevantAllocations = allocations.Where(a => relevantInvoiceIds.Contains(a.InvoiceId) && a.PaymentDate.Date <= asAtDate.Date).ToList();
        var totalPaid = Round2(relevantAllocations.Sum(a => a.AllocatedAmount));

        var arrears = Round2(totalInvoiced - totalPaid);
        return arrears < 0 ? 0m : arrears;
    }

    // Helper rounding
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    // For discount stacking
    public (decimal subtotal, decimal discountTotal, decimal total, decimal credit) CalculateFullExample(
        List<decimal> lineTotals,
        List<(decimal value, bool isPercentage)> discounts,
        decimal paymentAmount)
    {
        var (subtotal, discountTotal, total) = CalculateInvoiceTotals(lineTotals, discounts, "USD");
        var balance = Round2(total - paymentAmount);
        decimal credit = 0m;
        if (balance < 0)
        {
            credit = Round2(-balance);
            balance = 0m;
        }
        return (subtotal, discountTotal, total, credit);
    }
}

// DTOs for calculation service - no EF, pure
public record OutstandingInvoice(long InvoiceId, string InvoiceNumber, decimal BalanceDue, string Currency, DateTime DueDate, DateTime IssueDate);
public record ManualAllocation(long InvoiceId, decimal Amount, string Currency);
public record AllocationResult(long InvoiceId, decimal AllocatedAmount, string Currency, bool IsManualOverride);
public record InvoiceForArrears(long InvoiceId, decimal TotalAmount, DateTime IssueDate, DateTime DueDate);
public record PaymentAllocationForArrears(long InvoiceId, decimal AllocatedAmount, DateTime PaymentDate);
