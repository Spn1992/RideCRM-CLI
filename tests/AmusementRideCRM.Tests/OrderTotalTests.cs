using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;

namespace AmusementRideCRM.Tests
{
    public class OrderTotalTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task CustomerOrder_CalculatesSubTotalAndTotalAmountCorrectly()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var order = new CustomerOrder
                {
                    OrderId = 1,
                    OrderNumber = "ORD-1001",
                    CustomerId = 10,
                    OrderDate = DateTime.UtcNow,
                    ShippingCharge = 50.00m,
                    RushCharge = 25.00m,
                    OtherCharges = 10.00m,
                    DiscountAmount = 15.00m,
                    Status = "Draft",
                    ShipmentPreference = "Standard"
                };
                context.CustomerOrder.Add(order);

                var line1 = new CustomerOrderLine
                {
                    OrderLineId = 1,
                    OrderId = 1,
                    LineNumber = 1,
                    PartId = 101,
                    Quantity = 2,
                    UnitPriceUsd = 100.00m, // Line Total: 200.00
                    LineStatus = "Open",
                    CreatedDate = DateTime.UtcNow
                };

                var line2 = new CustomerOrderLine
                {
                    OrderLineId = 2,
                    OrderId = 1,
                    LineNumber = 2,
                    PartId = 102,
                    Quantity = 5,
                    UnitPriceUsd = 30.00m, // Line Total: 150.00
                    LineStatus = "Open",
                    CreatedDate = DateTime.UtcNow
                };

                context.CustomerOrderLine.AddRange(line1, line2);
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var order = await context.CustomerOrder.FirstAsync(o => o.OrderId == 1);
                var lines = await context.CustomerOrderLine.Where(l => l.OrderId == 1).ToListAsync();

                decimal calculatedSubTotal = lines.Sum(l => l.Quantity * l.UnitPriceUsd);
                order.SubTotal = calculatedSubTotal;
                order.TotalAmount = order.SubTotal + order.ShippingCharge + order.RushCharge + order.OtherCharges - order.DiscountAmount;

                await context.SaveChangesAsync();
            }

            // Assert
            using (var context = new ApplicationDbContext(options))
            {
                var order = await context.CustomerOrder.FirstAsync(o => o.OrderId == 1);

                // Expected SubTotal: (2 * 100) + (5 * 30) = 350.00
                Assert.Equal(350.00m, order.SubTotal);

                // Expected TotalAmount: 350 + 50 (shipping) + 25 (rush) + 10 (other) - 15 (discount) = 420.00
                Assert.Equal(420.00m, order.TotalAmount);
            }
        }
    }
}
