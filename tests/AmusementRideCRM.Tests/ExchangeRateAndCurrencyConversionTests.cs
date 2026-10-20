namespace AmusementRideCRM.Tests;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using AmusementRideCRM.Pages.PurchaseOrder;
using AmusementRideCRM.Pages.VendorInvoice;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ExchangeRateAndCurrencyConversionTests
{
    private DbContextOptions<ApplicationDbContext> CreateInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task GetEffectiveRateAsync_ShouldReturnActiveRateOrFallback()
    {
        // Arrange
        var options = CreateInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new ApplicationDbContext(options);

        var service = new CurrencyExchangeService(context);

        // Add historical exchange rates
        context.CurrencyExchangeRate.AddRange(
            new CurrencyExchangeRate
            {
                CurrencyExchangeRateId = 1,
                SourceCurrency = "EUR",
                TargetCurrency = "USD",
                ExchangeRate = 1.05m,
                EffectiveDate = new DateTime(2025, 1, 1),
                EffectiveEndDate = new DateTime(2025, 12, 31),
                CreatedDate = DateTime.UtcNow
            },
            new CurrencyExchangeRate
            {
                CurrencyExchangeRateId = 2,
                SourceCurrency = "EUR",
                TargetCurrency = "USD",
                ExchangeRate = 1.12m,
                EffectiveDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = null,
                CreatedDate = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        // Act & Assert
        // Rate on 2025-06-01 should be 1.05
        var rate2025 = await service.GetEffectiveRateAsync("EUR", "USD", new DateTime(2025, 6, 1));
        Assert.Equal(1.05m, rate2025);

        // Rate on 2026-05-01 should be 1.12
        var rate2026 = await service.GetEffectiveRateAsync("EUR", "USD", new DateTime(2026, 5, 1));
        Assert.Equal(1.12m, rate2026);

        // Same currency USD -> USD should return 1.0
        var rateUsd = await service.GetEffectiveRateAsync("USD", "USD", DateTime.UtcNow);
        Assert.Equal(1.0m, rateUsd);

        // Unknown currency GBP -> USD without record should default to 1.0
        var rateGbp = await service.GetEffectiveRateAsync("GBP", "USD", DateTime.UtcNow);
        Assert.Equal(1.0m, rateGbp);
    }

    [Fact]
    public async Task PurchaseOrderCreate_ShouldAutoCalculateUsdFieldsAndRespectUserOverride()
    {
        // Arrange
        var options = CreateInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new ApplicationDbContext(options);

        context.CurrencyExchangeRate.Add(new CurrencyExchangeRate
        {
            CurrencyExchangeRateId = 1,
            SourceCurrency = "EUR",
            TargetCurrency = "USD",
            ExchangeRate = 1.10m,
            EffectiveDate = new DateTime(2026, 1, 1),
            CreatedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var exchangeService = new CurrencyExchangeService(context);
        var createPageModel = new AmusementRideCRM.Pages.PurchaseOrder.CreateModel(context, exchangeService)
        {
            Item = new PurchaseOrder
            {
                PONumber = "PO-EUR-100",
                VendorId = 1,
                Currency = "EUR",
                OrderDate = new DateTime(2026, 2, 1),
                SubTotalEur = 1000.00m,
                FreightEur = 50.00m,
                TotalEur = "1050.00"
            }
        };

        // Act - OnGetAsync suggests exchange rate
        await createPageModel.OnGetAsync();
        Assert.Equal(1.10m, createPageModel.Item.ExchangeRate);
        Assert.Equal(1100.00m, createPageModel.Item.SubTotalUsd);
        Assert.Equal(55.00m, createPageModel.Item.FreightUsd);
        Assert.Equal(1155.00m, createPageModel.Item.TotalUsd);

        // Test custom rate override on submission
        createPageModel.Item.ExchangeRate = 1.20m; // User custom rate override
        await createPageModel.OnPostAsync();

        // Assert
        var savedPo = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.PONumber == "PO-EUR-100");
        Assert.NotNull(savedPo);
        Assert.Equal(1.20m, savedPo.ExchangeRate);
        Assert.Equal(1200.00m, savedPo.SubTotalUsd);
        Assert.Equal(60.00m, savedPo.FreightUsd);
        Assert.Equal(1260.00m, savedPo.TotalUsd);
    }

    [Fact]
    public async Task VendorInvoiceCreate_ShouldAutoCalculateUsdFields()
    {
        // Arrange
        var options = CreateInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new ApplicationDbContext(options);

        context.CurrencyExchangeRate.Add(new CurrencyExchangeRate
        {
            CurrencyExchangeRateId = 1,
            SourceCurrency = "EUR",
            TargetCurrency = "USD",
            ExchangeRate = 1.10m,
            EffectiveDate = new DateTime(2026, 1, 1),
            CreatedDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var exchangeService = new CurrencyExchangeService(context);
        var qbSyncService = new QBSyncQueueService(context);
        var varianceService = new VendorInvoiceVarianceService(context, qbSyncService);
        var createPageModel = new AmusementRideCRM.Pages.VendorInvoice.CreateModel(context, exchangeService, varianceService)
        {
            Item = new VendorInvoice
            {
                InvoiceNumber = "VI-2026-01",
                VendorId = 2,
                Currency = "EUR",
                InvoiceDate = new DateTime(2026, 3, 1),
                SubTotalEur = 500.00m,
                FreightEur = 25.00m,
                TotalEur = 525.00m,
                Status = "Approved"
            }
        };

        // Act
        await createPageModel.OnPostAsync();

        // Assert
        var savedInvoice = await context.VendorInvoice.FirstOrDefaultAsync(v => v.InvoiceNumber == "VI-2026-01");
        Assert.NotNull(savedInvoice);
        Assert.Equal(1.10m, savedInvoice.ExchangeRate);
        Assert.Equal(550.00m, savedInvoice.SubTotalUsd);
        Assert.Equal(27.50m, savedInvoice.FreightUsd);
        Assert.Equal(577.50m, savedInvoice.TotalUsd);
    }

    [Fact]
    public async Task EditPageModel_ShouldKeepExchangeRateAndUsdFieldsImmutableWhenSyncedToQB()
    {
        // Arrange
        var options = CreateInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new ApplicationDbContext(options);

        var po = new PurchaseOrder
        {
            PurchaseOrderId = 10,
            PONumber = "PO-SYNCED-01",
            VendorId = 1,
            Currency = "EUR",
            OrderDate = new DateTime(2026, 1, 1),
            SubTotalEur = 1000.00m,
            FreightEur = 100.00m,
            TotalEur = "1100.00",
            ExchangeRate = 1.10m,
            ExchangeRateDate = new DateTime(2026, 1, 1),
            SubTotalUsd = 1100.00m,
            FreightUsd = 110.00m,
            TotalUsd = 1210.00m,
            QBPOTxnID = "QB-PO-TXN-12345" // Mark as synced to QB
        };
        context.PurchaseOrder.Add(po);
        await context.SaveChangesAsync();

        var exchangeService = new CurrencyExchangeService(context);
        var editPageModel = new AmusementRideCRM.Pages.PurchaseOrder.EditModel(context, exchangeService);

        // User attempts to edit rates and totals on synced PO
        editPageModel.Item = new PurchaseOrder
        {
            PurchaseOrderId = 10,
            PONumber = "PO-SYNCED-01",
            VendorId = 1,
            Currency = "EUR",
            OrderDate = new DateTime(2026, 1, 1),
            SubTotalEur = 2000.00m, // Changed
            FreightEur = 200.00m, // Changed
            TotalEur = "2200.00",
            ExchangeRate = 1.50m, // Attempting to change rate
            QBPOTxnID = "QB-PO-TXN-12345"
        };

        // Act
        await editPageModel.OnPostAsync();

        // Assert
        var updatedPo = await context.PurchaseOrder.FirstOrDefaultAsync(p => p.PurchaseOrderId == 10);
        Assert.NotNull(updatedPo);
        // Snapshot rate and converted USD values should remain locked/immutable
        Assert.Equal(1.10m, updatedPo.ExchangeRate);
        Assert.Equal(1100.00m, updatedPo.SubTotalUsd);
        Assert.Equal(110.00m, updatedPo.FreightUsd);
        Assert.Equal(1210.00m, updatedPo.TotalUsd);
    }

    [Fact]
    public void QbXmlSerializer_BuildBillAddRq_ShouldConvertLineItemCostsToUsd()
    {
        // Arrange
        var serializer = new QbXmlSerializer();
        var bill = new VendorInvoice
        {
            VendorInvoiceId = 1,
            InvoiceNumber = "BILL-EUR-001",
            VendorId = 5,
            InvoiceDate = new DateTime(2026, 4, 1),
            Currency = "EUR",
            ExchangeRate = 1.10m,
            SubTotalUsd = 220.00m,
            TotalUsd = 220.00m
        };

        var vendor = new VendorMaster
        {
            VendorId = 5,
            VendorName = "Euro Parts GmbH",
            QBVendorListID = "V-800001"
        };

        var lines = new List<VendorInvoiceLine>
        {
            new VendorInvoiceLine
            {
                VILineId = 1,
                VendorInvoiceId = 1,
                LineNumber = 1,
                VendorPartNumber = "PART-A",
                Description = "Coaster Spring",
                Quantity = 2,
                UnitCostEur = 100.00m // 100 EUR * 1.10 = 110 USD
            }
        };

        // Act
        var xml = serializer.BuildBillAddRq(bill, lines, vendor, requestId: 1);

        // Assert
        Assert.NotNull(xml);
        Assert.Contains("<Cost>110.00</Cost>", xml);
    }

    [Fact]
    public void QbXmlSerializer_BuildPurchaseOrderAddRq_ShouldConvertLineItemRatesToUsd()
    {
        // Arrange
        var serializer = new QbXmlSerializer();
        var po = new PurchaseOrder
        {
            PurchaseOrderId = 2,
            PONumber = "PO-EUR-200",
            VendorId = 5,
            OrderDate = new DateTime(2026, 4, 1),
            Currency = "EUR",
            ExchangeRate = 1.15m,
            SubTotalUsd = 575.00m,
            TotalUsd = 575.00m
        };

        var vendor = new VendorMaster
        {
            VendorId = 5,
            VendorName = "Euro Parts GmbH",
            QBVendorListID = "V-800001"
        };

        var lines = new List<PurchaseOrderLine>
        {
            new PurchaseOrderLine
            {
                POLineId = 1,
                PurchaseOrderId = 2,
                LineNumber = 1,
                VendorPartNumber = "GEAR-10",
                VendorDescription = "Heavy duty gear",
                Quantity = 5,
                UnitCostEur = 100.00m // 100 EUR * 1.15 = 115 USD
            }
        };

        // Act
        var xml = serializer.BuildPurchaseOrderAddRq(po, lines, vendor, requestId: 2);

        // Assert
        Assert.NotNull(xml);
        Assert.Contains("<PurchaseOrderAddRq requestID=\"2\">", xml);
        Assert.Contains("<ListID>V-800001</ListID>", xml);
        Assert.Contains("<RefNumber>PO-EUR-200</RefNumber>", xml);
        Assert.Contains("<Quantity>5.00</Quantity>", xml);
        Assert.Contains("<Rate>115.00</Rate>", xml);
    }
}
