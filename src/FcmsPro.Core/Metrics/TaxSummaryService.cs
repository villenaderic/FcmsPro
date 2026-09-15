using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Metrics;

public record CategoryTotal(ExpenseCategory Category, decimal Total);

public record QuarterTotal(int Quarter, decimal Income, decimal Expenses, decimal Net);

public record TaxSummary(
    int Year,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetProfit,
    IReadOnlyList<QuarterTotal> Quarters,
    IReadOnlyList<CategoryTotal> ExpensesByCategory);

/// <summary>
/// Turns Payment/Expense records the user is already entering for other
/// reasons into a one-click year-end/quarterly summary - the kind of thing
/// that's genuinely useful at tax time without needing any new data entry.
/// Deliberately not tax advice or a specific jurisdiction's form - just
/// totals and a category breakdown, the raw numbers an accountant or tax
/// software would actually want.
/// </summary>
public class TaxSummaryService
{
    private readonly IUnitOfWork _uow;

    public TaxSummaryService(IUnitOfWork uow) => _uow = uow;

    public async Task<TaxSummary> GetSummaryAsync(int year, CancellationToken ct = default)
    {
        var allCommissions = await _uow.Commissions.GetAllAsync(ct);
        var allExpenses = await _uow.Expenses.GetAllAsync(ct);

        // Income for a given year = down payments made that year (from
        // commissions created that year) + payments recorded that year -
        // matches how MetricsService.GetDashboardKpisAsync treats income
        // elsewhere (down payment counted at the commission, subsequent
        // payments counted at their own date).
        var incomeByQuarter = new decimal[4];
        var totalIncome = 0m;

        foreach (var c in allCommissions.Where(c => c.DateAdded.Year == year))
        {
            var q = QuarterOf(c.DateAdded.Month);
            incomeByQuarter[q - 1] += c.DownPayment;
            totalIncome += c.DownPayment;
        }

        foreach (var c in allCommissions)
        {
            var payments = await _uow.Payments.GetByCommissionIdAsync(c.Id, ct);
            foreach (var p in payments.Where(p => p.Date.Year == year))
            {
                var q = QuarterOf(p.Date.Month);
                incomeByQuarter[q - 1] += p.Amount;
                totalIncome += p.Amount;
            }
        }

        var expensesThisYear = allExpenses.Where(e => e.Date.Year == year).ToList();
        var expensesByQuarter = new decimal[4];
        foreach (var e in expensesThisYear)
            expensesByQuarter[QuarterOf(e.Date.Month) - 1] += e.Amount;

        var totalExpenses = expensesThisYear.Sum(e => e.Amount);

        var quarters = Enumerable.Range(1, 4)
            .Select(q => new QuarterTotal(q, incomeByQuarter[q - 1], expensesByQuarter[q - 1], incomeByQuarter[q - 1] - expensesByQuarter[q - 1]))
            .ToList();

        var byCategory = expensesThisYear
            .GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(e => e.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();

        return new TaxSummary(year, totalIncome, totalExpenses, totalIncome - totalExpenses, quarters, byCategory);
    }

    /// <summary>Every year that has at least one commission or expense, newest first - populates the year picker without the user having to guess which years have data.</summary>
    public async Task<List<int>> GetAvailableYearsAsync(CancellationToken ct = default)
    {
        var commissions = await _uow.Commissions.GetAllAsync(ct);
        var expenses = await _uow.Expenses.GetAllAsync(ct);

        var years = commissions.Select(c => c.DateAdded.Year)
            .Concat(expenses.Select(e => e.Date.Year))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        if (years.Count == 0)
            years.Add(DateTime.Today.Year);

        return years;
    }

    private static int QuarterOf(int month) => (month - 1) / 3 + 1;
}
