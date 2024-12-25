using NSubstitute;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace HolidayTests;

[TestFixture]
public class BudgetTest
{
    private BudgetService _budgetService;
    private IBudgetRepository _budgetRepository = Substitute.For<IBudgetRepository>();

    [SetUp]
    public void SetUp()
    {
        _budgetService = new BudgetService(_budgetRepository);
    }

    [Test]
    public void invalid_period()
    {
        BudgetShouldBe(0, new DateTime(2024, 1, 31), new DateTime(2024, 1, 1));
    }

    [Test]
    public void january_no_budget()
    {
        GivenBudget();
        BudgetShouldBe(0, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));
    }
    
    [Test]
    public void in_month()
    {
        GivenBudget(new Budget() { YearMonth = "202401", Amount = 31000 });
        BudgetShouldBe(10000, new DateTime(2024, 1, 1), new DateTime(2024, 1, 10));
    }
    
    [Test]
    public void whole_month()
    {
        GivenBudget(new Budget() { YearMonth = "202401", Amount = 31000 });
        BudgetShouldBe(31000, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));
    }
   
    [Test]
    public void cross_month()
    {
        GivenBudget(new Budget() { YearMonth = "202401", Amount = 31000 }, new Budget() { YearMonth = "202402", Amount = 29000 });
        BudgetShouldBe(4000, new DateTime(2024, 1, 30), new DateTime(2024, 2, 2));
    }

    private void BudgetShouldBe(int expected, DateTime start, DateTime end)
    {
        Assert.That(_budgetService.GetBudget(start, end), Is.EqualTo(expected));
    }

    private void GivenBudget(params Budget[] budget)
    {
        _budgetRepository.GetAll().Returns(budget.ToList());
    }
}

public interface IBudgetRepository
{
    List<Budget> GetAll();
}

public class Budget
{
    public string YearMonth { get; set; }
    public int Amount { get; set; }
}

public class BudgetService(IBudgetRepository budgetRepository)
{
    public decimal GetBudget(DateTime start, DateTime end)
    {
        if (start > end)
        {
            return 0;
        }

        return CalculateBudgetForPeriod(GetMonthlyCoverage(start, end), budgetRepository.GetAll());
    }

    private List<(int year, int month, int coveredDays)>
        GetMonthlyCoverage(DateTime start, DateTime end)
    {
        var coverageList = new List<(int year, int month, int coveredDays)>();

        var currentMonth = new DateTime(start.Year, start.Month, 1);

        while (currentMonth <= end)
        {
            var year = currentMonth.Year;
            var month = currentMonth.Month;

            var firstDayOfMonth = new DateTime(year, month, 1);
            var lastDayOfMonth = new DateTime(year, month,
                DateTime.DaysInMonth(year, month));

            var actualStartDay = (start > firstDayOfMonth)
                ? start
                : firstDayOfMonth;
            var actualEndDay = (end < lastDayOfMonth)
                ? end
                : lastDayOfMonth;

            if (actualStartDay > actualEndDay)
            {
                currentMonth = currentMonth.AddMonths(1);
                continue;
            }

            var coveredDays = (actualEndDay - actualStartDay).Days + 1;

            coverageList.Add((year, month, coveredDays));

            currentMonth = currentMonth.AddMonths(1);
        }

        return coverageList;
    }

    private decimal CalculateBudgetForPeriod(
        List<(int Year, int Month, int CoveredDays)> monthlyCoverage,
        List<Budget> budgets)
    {
        var total = 0m;

        foreach (var (year, month, coveredDays) in monthlyCoverage)
        {
            var yearMonth = $"{year}{month:00}";

            var budget = budgets.FirstOrDefault(b => b.YearMonth == yearMonth);
            if (budget != null)
            {
                var daysInMonth = DateTime.DaysInMonth(year, month);

                total += (decimal)(budget.Amount / daysInMonth) * coveredDays;
            }
        }

        return total;
    }
}