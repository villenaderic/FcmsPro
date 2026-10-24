using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Metrics;
using FcmsPro.Pdf;
using Moq;
using Xunit;

namespace FcmsPro.Tests;

public class TaxSummaryServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICommissionRepository> _commissions = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IExpenseRepository> _expenses = new();

    public TaxSummaryServiceTests()
    {
        _uow.SetupGet(u => u.Commissions).Returns(_commissions.Object);
        _uow.SetupGet(u => u.Payments).Returns(_payments.Object);
        _uow.SetupGet(u => u.Expenses).Returns(_expenses.Object);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesCorrectIncomeAndExpensesByQuarter()
    {
        var commId = Guid.NewGuid();
        var commList = new List<Commission>
        {
            new()
            {
                Id = commId,
                Title = "Web Dev",
                Price = 5000,
                DownPayment = 1000,
                DateAdded = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero), // Q4
                IsDeleted = false
            }
        };

        var payList = new List<Payment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CommissionId = commId,
                Amount = 4000,
                Date = new DateOnly(2026, 10, 21), // Q4
                IsDeleted = false
            }
        };

        var expList = new List<Expense>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Description = "Hosting",
                Amount = 500,
                Category = ExpenseCategory.SoftwareTools,
                Date = new DateOnly(2026, 10, 10), // Q4
                IsDeleted = false
            }
        };

        _commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(commList);
        _payments.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(payList);
        _expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expList);

        var service = new TaxSummaryService(_uow.Object);
        var summary = await service.GetSummaryAsync(2026);

        Assert.Equal(2026, summary.Year);
        Assert.Equal(5000m, summary.TotalIncome);
        Assert.Equal(500m, summary.TotalExpenses);
        Assert.Equal(4500m, summary.NetProfit);

        Assert.Equal(4, summary.Quarters.Count);
        Assert.Equal(0m, summary.Quarters[0].Income); // Q1
        Assert.Equal(0m, summary.Quarters[1].Income); // Q2
        Assert.Equal(0m, summary.Quarters[2].Income); // Q3
        Assert.Equal(5000m, summary.Quarters[3].Income); // Q4
        Assert.Equal(500m, summary.Quarters[3].Expenses);
        Assert.Equal(4500m, summary.Quarters[3].Net);
    }

    [Fact]
    public async Task GetSummaryAsync_IgnoresSoftDeletedEntities()
    {
        var commActiveId = Guid.NewGuid();
        var commDeletedId = Guid.NewGuid();

        var commList = new List<Commission>
        {
            new()
            {
                Id = commActiveId,
                Title = "Active Commission",
                Price = 2000,
                DownPayment = 500,
                DateAdded = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                IsDeleted = false
            },
            new()
            {
                Id = commDeletedId,
                Title = "Deleted Commission",
                Price = 3000,
                DownPayment = 1000,
                DateAdded = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                IsDeleted = true
            }
        };

        var payList = new List<Payment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CommissionId = commActiveId,
                Amount = 1500,
                Date = new DateOnly(2026, 10, 5),
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                CommissionId = commActiveId,
                Amount = 9999,
                Date = new DateOnly(2026, 10, 5),
                IsDeleted = true // Deleted payment on active commission
            },
            new()
            {
                Id = Guid.NewGuid(),
                CommissionId = commDeletedId,
                Amount = 2000,
                Date = new DateOnly(2026, 10, 5),
                IsDeleted = false // Payment on deleted commission
            }
        };

        var expList = new List<Expense>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Description = "Deleted Expense",
                Amount = 2500,
                Category = ExpenseCategory.OfficeSupplies,
                Date = new DateOnly(2026, 10, 1),
                IsDeleted = true
            }
        };

        _commissions.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(commList);
        _payments.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(payList);
        _expenses.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expList);

        var service = new TaxSummaryService(_uow.Object);
        var summary = await service.GetSummaryAsync(2026);

        // Only active down payment (500) + active payment on active commission (1500) = 2000
        Assert.Equal(2000m, summary.TotalIncome);
        Assert.Equal(0m, summary.TotalExpenses);
        Assert.Equal(2000m, summary.NetProfit);
    }

    [Fact]
    public async Task TaxSummaryRenderer_RendersValidPdfBytes()
    {
        var quarters = new List<QuarterTotal>
        {
            new(1, 0, 0, 0),
            new(2, 0, 0, 0),
            new(3, 0, 0, 0),
            new(4, 12300, 0, 12300)
        };
        var summary = new TaxSummary(2026, 12300, 0, 12300, quarters, new List<CategoryTotal>());
        var settings = new AppSettings
        {
            BusinessName = "My Freelance Studio",
            CurrencySymbol = "₱"
        };

        var renderer = new TaxSummaryRenderer();
        var pdfBytes = await renderer.RenderPdfAsync(summary, settings);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        // PDF header magic bytes %PDF-
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }
}
