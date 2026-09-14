using LearnCloud.Fees.Services;
using Xunit;

namespace LearnCloud.Fees.Tests;

public class FeeCalculationServiceTests
{
    private readonly FeeCalculationService _svc = new();

    // Three real balances from spreadsheet - must reproduce exactly to cent, if out by cent stop and find why

    [Fact]
    public void RealBalance_1_Thabo_Ndlovu_95_00_Exact()
    {
        // Source: Petra High Grade 5 Blue
        // Fee items: Tuition 500 + Levy 50 = 550
        // Discount: Sibling 10% = 55
        // Total: 495
        // Payments: 300 + 100 = 400
        // Balance: 95

        var lineTotals = new List<decimal> { 500.00m, 50.00m };
        var discounts = new List<(decimal value, bool isPercentage)> { (10m, true) };

        var (subtotal, discountTotal, total) = _svc.CalculateInvoiceTotals(lineTotals, discounts, "USD");

        Assert.Equal(550.00m, subtotal);
        Assert.Equal(55.00m, discountTotal);
        Assert.Equal(495.00m, total);

        var paid = 300.00m + 100.00m;
        var balance = FeeCalculationService.Round2(total - paid);

        Assert.Equal(400.00m, paid);
        Assert.Equal(95.00m, balance);

        // Prove no float error
        Assert.True(balance == 95.00m, $"Balance {balance} not exact 95.00 - money bug!");
    }

    [Fact]
    public void RealBalance_2_Lindiwe_Moyo_324_34_Exact_Cents_Tricky()
    {
        // Source: Form 1A, Tuition 333.33 + Boarding 200 + Transport 66.67 = 600, discount 12.5% =75, total 525, payments 100.33+100.33=200.66, balance 324.34
        // This tests rounding and decimal exactness - double would fail

        var lineTotals = new List<decimal> { 333.33m, 200.00m, 66.67m };
        var discounts = new List<(decimal value, bool isPercentage)> { (12.5m, true) };

        var (subtotal, discountTotal, total) = _svc.CalculateInvoiceTotals(lineTotals, discounts, "USD");

        Assert.Equal(600.00m, subtotal); // 333.33+200+66.67=600 exact decimal
        Assert.Equal(75.00m, discountTotal); // 600*0.125=75
        Assert.Equal(525.00m, total);

        var paid = 100.33m + 100.33m;
        Assert.Equal(200.66m, paid); // decimal exact, double would be 200.659999...

        var balance = FeeCalculationService.Round2(total - paid);
        Assert.Equal(324.34m, balance); // 525-200.66=324.34 exact

        // If any is out by cent, fail
        Assert.True(balance == 324.34m, $"Balance {balance} != 324.34 - cent error, stop and find why!");
    }

    [Fact]
    public void RealBalance_3_Kuda_Dube_Overpayment_Credit_30_Exact()
    {
        // Mid-term joiner prorated: Term 90 days, 27 days remaining, tuition 900 prorated 270, levy 100 =370, payment 400, credit 30
        var totalDays = 90;
        var daysRemaining = 27;
        var tuitionProrated = _svc.CalculateProratedAmount(900.00m, daysRemaining, totalDays);
        Assert.Equal(270.00m, tuitionProrated); // 900*0.3=270 exact

        var levy = 100.00m;
        var subtotal = FeeCalculationService.Round2(tuitionProrated + levy);
        Assert.Equal(370.00m, subtotal);

        var payment = 400.00m;
        var outstanding = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV-2026-003", 370.00m, "USD", new DateTime(2026,3,15), new DateTime(2026,3,15))
        };

        var (allocations, credit) = _svc.AllocatePayment(payment, "USD", outstanding, null);

        Assert.Single(allocations);
        Assert.Equal(370.00m, allocations[0].AllocatedAmount);
        Assert.Equal(30.00m, credit);
        Assert.Equal(0m, FeeCalculationService.Round2(370.00m + 30.00m - payment)); // allocation + credit = payment exact
    }

    // Allocation tests

    [Fact]
    public void AllocatePayment_FifoOldestFirst_Default()
    {
        var invoices = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV001", 500.00m, "USD", new DateTime(2026,2,10), new DateTime(2026,1,20)),
            new OutstandingInvoice(2, "INV002", 300.00m, "USD", new DateTime(2026,5,10), new DateTime(2026,5,01))
        };

        var (alloc, credit) = _svc.AllocatePayment(600.00m, "USD", invoices, null);

        Assert.Equal(2, alloc.Count);
        Assert.Equal(500.00m, alloc[0].AllocatedAmount);
        Assert.Equal(100.00m, alloc[1].AllocatedAmount);
        Assert.Equal(0m, credit);
        Assert.Equal(600.00m, alloc.Sum(a=>a.AllocatedAmount) + credit);
    }

    [Fact]
    public void AllocatePayment_Overpayment_HeldAsCredit()
    {
        var invoices = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV001", 500m, "USD", new DateTime(2026,2,10), DateTime.UtcNow),
            new OutstandingInvoice(2, "INV002", 300m, "USD", new DateTime(2026,5,10), DateTime.UtcNow)
        };

        var (alloc, credit) = _svc.AllocatePayment(900m, "USD", invoices, null);

        Assert.Equal(2, alloc.Count);
        Assert.Equal(800m, alloc.Sum(a=>a.AllocatedAmount));
        Assert.Equal(100m, credit);
    }

    [Fact]
    public void AllocatePayment_ManualOverride_BoardingFirst()
    {
        var invoices = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV001", 500m, "USD", new DateTime(2026,2,10), DateTime.UtcNow), // tuition
            new OutstandingInvoice(2, "INV002", 300m, "USD", new DateTime(2026,2,10), DateTime.UtcNow)  // boarding
        };

        var manual = new List<ManualAllocation>
        {
            new ManualAllocation(2, 300m, "USD"), // clear boarding first
            new ManualAllocation(1, 300m, "USD")
        };

        var (alloc, credit) = _svc.AllocatePayment(600m, "USD", invoices, manual);

        Assert.Equal(2, alloc.Count);
        Assert.True(alloc.All(a=>a.IsManualOverride));
        Assert.Equal(300m, alloc.First(a=>a.InvoiceId==2).AllocatedAmount);
        Assert.Equal(300m, alloc.First(a=>a.InvoiceId==1).AllocatedAmount);
        Assert.Equal(0m, credit);
    }

    [Fact]
    public void AllocatePayment_ManualSumExceedsPayment_Throws()
    {
        var invoices = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV001", 500m, "USD", DateTime.UtcNow, DateTime.UtcNow)
        };
        var manual = new List<ManualAllocation>
        {
            new ManualAllocation(1, 600m, "USD")
        };

        Assert.Throws<InvalidOperationException>(() => _svc.AllocatePayment(500m, "USD", invoices, manual));
    }

    [Fact]
    public void AllocatePayment_CurrencyMismatch_Throws()
    {
        var invoices = new List<OutstandingInvoice>
        {
            new OutstandingInvoice(1, "INV001", 500m, "ZWG", DateTime.UtcNow, DateTime.UtcNow)
        };

        Assert.Throws<InvalidOperationException>(() => _svc.AllocatePayment(500m, "USD", invoices, null));
    }

    // Arrears

    [Fact]
    public void ComputeArrearsAsAt_FilterByDate()
    {
        var invoices = new List<InvoiceForArrears>
        {
            new InvoiceForArrears(1, 500m, new DateTime(2026,1,10), new DateTime(2026,2,10)),
            new InvoiceForArrears(2, 500m, new DateTime(2026,5,10), new DateTime(2026,6,10))
        };

        var allocations = new List<PaymentAllocationForArrears>
        {
            new PaymentAllocationForArrears(1, 300m, new DateTime(2026,2,15))
        };

        // As at 2026-03-01: only first invoice issued 500, paid 300, arrears 200
        var arrearsMar = _svc.ComputeArrearsAsAt(invoices, allocations, new DateTime(2026,3,1));
        Assert.Equal(200m, arrearsMar);

        // As at 2026-07-01: both invoices issued 1000, paid 300, arrears 700
        var arrearsJul = _svc.ComputeArrearsAsAt(invoices, allocations, new DateTime(2026,7,1));
        Assert.Equal(700m, arrearsJul);
    }

    [Fact]
    public void ComputeOverdueArrears_FilterDueDate()
    {
        var invoices = new List<InvoiceForArrears>
        {
            new InvoiceForArrears(1, 500m, new DateTime(2026,1,10), new DateTime(2026,2,10)), // due Feb
            new InvoiceForArrears(2, 500m, new DateTime(2026,5,10), new DateTime(2026,6,10))  // due Jun
        };

        var allocations = new List<PaymentAllocationForArrears>
        {
            new PaymentAllocationForArrears(1, 500m, new DateTime(2026,2,15)) // paid first fully
        };

        // As at Mar 1, overdue should be 0 because first paid, second not yet due
        var overdueMar = _svc.ComputeOverdueArrearsAsAt(invoices, allocations, new DateTime(2026,3,1));
        Assert.Equal(0m, overdueMar);

        // As at Jul 1, second invoice due Jun not paid, overdue 500
        var overdueJul = _svc.ComputeOverdueArrearsAsAt(invoices, allocations, new DateTime(2026,7,1));
        Assert.Equal(500m, overdueJul);
    }

    // Prorating

    [Fact]
    public void ProratedJoiner_Daily_Calc_Exact()
    {
        // Term 90 days, 31 days remaining
        var amount = _svc.CalculateProratedAmount(900m, 31, 90);
        // 900*31/90 = 310 exact
        Assert.Equal(310.00m, amount);

        // 27 days = 270 exact
        Assert.Equal(270.00m, _svc.CalculateProratedAmount(900m, 27, 90));

        // 0 days =0
        Assert.Equal(0m, _svc.CalculateProratedAmount(900m, 0, 90));

        // Full =90 days = full
        Assert.Equal(900m, _svc.CalculateProratedAmount(900m, 90, 90));

        // With rounding: 900 * 1/3 =300 exact, 900*1/6=150 exact, but 1000*1/3=333.3333 -> 333.33
        var tricky = _svc.CalculateProratedAmount(1000m, 1, 3);
        Assert.Equal(333.33m, tricky); // 1000*0.3333... =333.33 rounded AwayFromZero
    }

    [Fact]
    public void Discount_Percentage_And_Fixed_Stacking()
    {
        var lines = new List<decimal> { 500m, 50m }; // 550
        var discounts = new List<(decimal value, bool isPercentage)>
        {
            (10m, true), // 10% =>55
            (20m, false) // fixed 20 =>20
        };
        var (subtotal, discountTotal, total) = _svc.CalculateInvoiceTotals(lines, discounts, "USD");
        Assert.Equal(550m, subtotal);
        Assert.Equal(75m, discountTotal); // 55+20
        Assert.Equal(475m, total);
    }

    [Fact]
    public void No_Float_Used_Proof()
    {
        // This test would fail if we used double: 0.1+0.2 !=0.3
        decimal a = 0.1m;
        decimal b = 0.2m;
        decimal c = 0.3m;
        Assert.Equal(c, a+b);

        // Simulate fee calc with 0.1 increments that would break double
        var lines = new List<decimal> { 0.1m, 0.2m };
        var (subtotal, _, total) = _svc.CalculateInvoiceTotals(lines, new List<(decimal,bool)>(), "USD");
        Assert.Equal(0.3m, subtotal);
        Assert.Equal(0.3m, total);
    }
}
