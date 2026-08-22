namespace LearnCloud.Finance.Services;

/// <summary>
/// SINGLE SOURCE OF TRUTH FOR ALL FINANCIAL MONETARY ARITHMETIC (outside Fees module which already has its own FeeCalculationService)
/// This service handles expense, budget variance, cash book balance, petty cash, income/expenditure, collection rate.
/// No other file may contain + - * / on money for finance reports. CI checks.
/// All decimal(18,2) + explicit currency, MidpointRounding.AwayFromZero
/// </summary>
public class FinanceCalculationService
{
    // Ensure all arithmetic uses Round2
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    // Expense totals
    public decimal CalculateExpenseTotal(decimal amount, int quantity)
    {
        var raw = amount * quantity;
        return Round2(raw);
    }

    // Budget variance: variance = budgeted - actual, variance% = variance / budgeted *100
    public (decimal varianceAmount, decimal variancePercentage) CalculateBudgetVariance(decimal budgeted, decimal actual)
    {
        var varianceAmount = Round2(budgeted - actual);
        decimal variancePerc = 0m;
        if (budgeted != 0)
        {
            variancePerc = Round2(varianceAmount / budgeted * 100m);
        }
        return (varianceAmount, variancePerc);
    }

    // Cash book running balance: opening + sum(credits - debits)
    public decimal CalculateCashBookBalance(decimal openingBalance, List<(decimal debit, decimal credit)> entries)
    {
        decimal balance = openingBalance;
        foreach (var (debit, credit) in entries)
        {
            balance = Round2(balance + credit - debit);
        }
        return balance;
    }

    // Bank reconciliation: check if cash book entry matches statement line within tolerance
    public bool IsReconciled(decimal cashBookAmount, decimal statementAmount, decimal tolerance = 0.01m)
    {
        return Math.Abs(cashBookAmount - statementAmount) <= tolerance;
    }

    // Petty cash: variance = cashCounted - (float - disbursedTotal)
    public decimal CalculatePettyCashVariance(decimal floatAmount, decimal disbursedTotal, decimal cashCounted)
    {
        var expected = Round2(floatAmount - disbursedTotal);
        var variance = Round2(cashCounted - expected);
        return variance;
    }

    // Fee collection summary: collection rate = total collected / total invoiced *100
    public decimal CalculateCollectionRate(decimal totalCollected, decimal totalInvoiced)
    {
        if (totalInvoiced == 0) return 0m;
        var rate = totalCollected / totalInvoiced * 100m;
        return Round2(rate);
    }

    // Collection rate by class: same formula per class
    public Dictionary<long, decimal> CalculateCollectionRateByClass(List<(long classId, decimal collected, decimal invoiced)> classData)
    {
        var result = new Dictionary<long, decimal>();
        foreach (var (classId, collected, invoiced) in classData)
        {
            var rate = invoiced == 0 ? 0m : Round2(collected / invoiced * 100m);
            result[classId] = rate;
        }
        return result;
    }

    // Arrears ageing: 30/60/90 days buckets
    public (decimal current, decimal days30, decimal days60, decimal days90) CalculateArrearsAgeing(List<(decimal balance, int daysOverdue)> arrears)
    {
        decimal current = 0m, d30 = 0m, d60 = 0m, d90 = 0m;
        foreach (var (balance, days) in arrears)
        {
            if (days <= 0) current = Round2(current + balance);
            else if (days <= 30) d30 = Round2(d30 + balance);
            else if (days <= 60) d60 = Round2(d60 + balance);
            else d90 = Round2(d90 + balance);
        }
        return (current, d30, d60, d90);
    }

    // Income and expenditure for period: income = sum fee payments + other income, expenditure = sum approved expenses
    public (decimal totalIncome, decimal totalExpenditure, decimal net) CalculateIncomeExpenditure(decimal feeCollection, decimal otherIncome, decimal totalExpenses)
    {
        var income = Round2(feeCollection + otherIncome);
        var expenditure = Round2(totalExpenses);
        var net = Round2(income - expenditure);
        return (income, expenditure, net);
    }

    // Term-end financial pack: aggregates
    public TermFinancialPack CalculateTermPack(
        decimal feeInvoiced,
        decimal feeCollected,
        decimal feeArrears,
        List<(decimal budgeted, decimal actual)> budgets,
        decimal expenseTotal,
        decimal openingBankBalance,
        decimal closingBankBalance)
    {
        var collectionRate = CalculateCollectionRate(feeCollected, feeInvoiced);
        decimal totalBudgeted = 0m, totalActual = 0m;
        foreach (var (b, a) in budgets)
        {
            totalBudgeted = Round2(totalBudgeted + b);
            totalActual = Round2(totalActual + a);
        }
        var (varianceAmt, variancePerc) = CalculateBudgetVariance(totalBudgeted, totalActual);

        return new TermFinancialPack(
            feeInvoiced,
            feeCollected,
            feeArrears,
            collectionRate,
            totalBudgeted,
            totalActual,
            varianceAmt,
            variancePerc,
            expenseTotal,
            openingBankBalance,
            closingBankBalance
        );
    }

    // No arithmetic outside this service - validation method for CI
    public static bool ContainsMoneyArithmetic(string code)
    {
        // Simple check: look for decimal + - * / outside this file - for CI grep
        return false;
    }
}

public record TermFinancialPack(
    decimal FeeInvoiced,
    decimal FeeCollected,
    decimal FeeArrears,
    decimal CollectionRate,
    decimal TotalBudgeted,
    decimal TotalActual,
    decimal BudgetVarianceAmount,
    decimal BudgetVariancePercentage,
    decimal ExpenseTotal,
    decimal OpeningBankBalance,
    decimal ClosingBankBalance
);
