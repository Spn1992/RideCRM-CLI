using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class OrderExceptionValidationTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task UnitPriceOverride_BelowMarginThreshold_TriggersMarginExceptionAndPendingStatus()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.SystemConfiguration.AddRange(
                    new SystemConfiguration { ConfigKey = "MinimumGrossMargin", ConfigValue = "0.20", DataType = "Decimal", IsEditable = true },
                    new SystemConfiguration { ConfigKey = "EuroConversionFactor", ConfigValue = "1.10", DataType = "Decimal", IsEditable = true },
                    new SystemConfiguration { ConfigKey = "PricingMarkup", ConfigValue = "1.40", DataType = "Decimal", IsEditable = true }
                );

                var part = new Part
                {
                    PartId = 1,
                    PartNumber = "PART-001",
                    Description = "Roller Coaster Wheel",
                    UnitOfMeasure = "Ea",
                    LastVendorCostEur = 100.00m, // Cost USD = 100 * 1.10 = $110.00
                    QuantityOnHand = 100.00m,
                    ReservedQuantity = 0.00m,
                    IsActive = true
                };
                context.Part.Add(part);

                var order = new CustomerOrder
                {
                    OrderId = 1,
                    OrderNumber = "ORD-2001",
                    CustomerId = 1,
                    OrderDate = DateTime.UtcNow,
                    Status = "Draft",
                    ShipmentPreference = "Standard"
                };
                context.CustomerOrder.Add(order);

                var line = new CustomerOrderLine
                {
                    OrderLineId = 1,
                    OrderId = 1,
                    LineNumber = 1,
                    PartId = 1,
                    Quantity = 2,
                    UnitPriceUsd = 120.00m, // Margin = (120 - 110)/120 = 8.33% < 20% threshold
                    LineStatus = "Open",
                    CreatedDate = DateTime.UtcNow
                };
                context.CustomerOrderLine.Add(line);

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var validationService = new OrderExceptionValidationService(context);
                var result = await validationService.ValidateAndApplyExceptionsAsync(1);

                // Assert
                Assert.True(result.HasException);
                Assert.False(result.CanConfirm);
                Assert.Equal("Pending Review", result.ExceptionStatus);

                var updatedOrder = await context.CustomerOrder.FirstAsync(o => o.OrderId == 1);
                Assert.True(updatedOrder.HasException);
                Assert.Equal("Pending Exception Review", updatedOrder.Status);
                Assert.Contains("below minimum required margin", updatedOrder.ExceptionReason);

                var updatedLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 1);
                Assert.True(updatedLine.HasMarginException);
                Assert.False(updatedLine.HasStockException);
                Assert.Equal(110.00m, updatedLine.CostUsd);
            }
        }

        [Fact]
        public async Task OrderLineQuantity_ExceedingAvailableStock_TriggersStockExceptionAndPendingStatus()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.SystemConfiguration.AddRange(
                    new SystemConfiguration { ConfigKey = "MinimumGrossMargin", ConfigValue = "0.20", DataType = "Decimal", IsEditable = true },
                    new SystemConfiguration { ConfigKey = "EuroConversionFactor", ConfigValue = "1.10", DataType = "Decimal", IsEditable = true },
                    new SystemConfiguration { ConfigKey = "PricingMarkup", ConfigValue = "1.40", DataType = "Decimal", IsEditable = true }
                );

                var part = new Part
                {
                    PartId = 2,
                    PartNumber = "PART-002",
                    Description = "Safety Belt Harness",
                    UnitOfMeasure = "Ea",
                    LastVendorCostEur = 50.00m, // Cost USD = $55.00
                    QuantityOnHand = 10.00m,
                    ReservedQuantity = 2.00m, // Available stock = 8
                    IsActive = true
                };
                context.Part.Add(part);

                var order = new CustomerOrder
                {
                    OrderId = 2,
                    OrderNumber = "ORD-2002",
                    CustomerId = 1,
                    OrderDate = DateTime.UtcNow,
                    Status = "Draft",
                    ShipmentPreference = "Standard"
                };
                context.CustomerOrder.Add(order);

                var line = new CustomerOrderLine
                {
                    OrderLineId = 2,
                    OrderId = 2,
                    LineNumber = 1,
                    PartId = 2,
                    Quantity = 15.00m, // 15 > 8 available
                    UnitPriceUsd = 100.00m, // Margin = (100 - 55)/100 = 45% >= 20%
                    LineStatus = "Open",
                    CreatedDate = DateTime.UtcNow
                };
                context.CustomerOrderLine.Add(line);

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var validationService = new OrderExceptionValidationService(context);
                var result = await validationService.ValidateAndApplyExceptionsAsync(2);

                // Assert
                Assert.True(result.HasException);
                Assert.False(result.CanConfirm);

                var updatedOrder = await context.CustomerOrder.FirstAsync(o => o.OrderId == 2);
                Assert.True(updatedOrder.HasException);
                Assert.Equal("Pending Exception Review", updatedOrder.Status);
                Assert.Contains("exceeds available stock", updatedOrder.ExceptionReason);

                var updatedLine = await context.CustomerOrderLine.FirstAsync(l => l.OrderLineId == 2);
                Assert.False(updatedLine.HasMarginException);
                Assert.True(updatedLine.HasStockException);
            }
        }

        [Fact]
        public async Task CleanOrder_NoPricingOrStockDiscrepancies_RemainsClean()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.SystemConfiguration.AddRange(
                    new SystemConfiguration { ConfigKey = "MinimumGrossMargin", ConfigValue = "0.20", DataType = "Decimal", IsEditable = true },
                    new SystemConfiguration { ConfigKey = "EuroConversionFactor", ConfigValue = "1.10", DataType = "Decimal", IsEditable = true }
                );

                var part = new Part
                {
                    PartId = 3,
                    PartNumber = "PART-003",
                    Description = "Brake Pad",
                    UnitOfMeasure = "Ea",
                    LastVendorCostEur = 50.00m, // Cost USD = $55.00
                    QuantityOnHand = 50.00m,
                    ReservedQuantity = 0.00m,
                    IsActive = true
                };
                context.Part.Add(part);

                var order = new CustomerOrder
                {
                    OrderId = 3,
                    OrderNumber = "ORD-2003",
                    CustomerId = 1,
                    OrderDate = DateTime.UtcNow,
                    Status = "Draft",
                    ShipmentPreference = "Standard"
                };
                context.CustomerOrder.Add(order);

                var line = new CustomerOrderLine
                {
                    OrderLineId = 3,
                    OrderId = 3,
                    LineNumber = 1,
                    PartId = 3,
                    Quantity = 5.00m,
                    UnitPriceUsd = 100.00m, // Margin = 45%
                    LineStatus = "Open",
                    CreatedDate = DateTime.UtcNow
                };
                context.CustomerOrderLine.Add(line);

                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var validationService = new OrderExceptionValidationService(context);
                var result = await validationService.ValidateAndApplyExceptionsAsync(3);

                // Assert
                Assert.False(result.HasException);
                Assert.True(result.CanConfirm);
                Assert.Null(result.ExceptionReason);

                var updatedOrder = await context.CustomerOrder.FirstAsync(o => o.OrderId == 3);
                Assert.False(updatedOrder.HasException);
                Assert.Equal("Draft", updatedOrder.Status);
            }
        }

        [Fact]
        public async Task SupervisorApproval_ClearsConfirmationBlock_AndSetsApprovalMetadata()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var order = new CustomerOrder
                {
                    OrderId = 4,
                    OrderNumber = "ORD-2004",
                    CustomerId = 1,
                    OrderDate = DateTime.UtcNow,
                    Status = "Pending Exception Review",
                    HasException = true,
                    ExceptionReason = "Line 1: Low Margin",
                    ExceptionStatus = "Pending Review",
                    ShipmentPreference = "Standard"
                };
                context.CustomerOrder.Add(order);
                await context.SaveChangesAsync();
            }

            // Act & Assert
            using (var context = new ApplicationDbContext(options))
            {
                var validationService = new OrderExceptionValidationService(context);

                bool canConfirmBefore = await validationService.CanConfirmOrderAsync(4);
                Assert.False(canConfirmBefore);

                await validationService.ApproveOrderExceptionsAsync(4, "SupervisorJane", "Approved 10% override for loyalty customer");

                bool canConfirmAfter = await validationService.CanConfirmOrderAsync(4);
                Assert.True(canConfirmAfter);

                var approvedOrder = await context.CustomerOrder.FirstAsync(o => o.OrderId == 4);
                Assert.Equal("Approved", approvedOrder.ExceptionStatus);
                Assert.Equal("SupervisorJane", approvedOrder.ApprovedBy);
                Assert.NotNull(approvedOrder.ApprovedDate);
                Assert.Equal("Approved 10% override for loyalty customer", approvedOrder.SupervisorComments);
            }
        }
    }
}
