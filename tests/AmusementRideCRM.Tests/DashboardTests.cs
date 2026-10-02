using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Pages;

namespace AmusementRideCRM.Tests
{
    public class DashboardTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public void OnGet_PopulatesCountsCorrectly()
        {
            // Arrange
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                context.CustomerOrder.AddRange(
                    new CustomerOrder { OrderId = 1, OrderNumber = "1" },
                    new CustomerOrder { OrderId = 2, OrderNumber = "2" }
                );

                context.Part.AddRange(
                    new Part { PartId = 1, PartNumber = "P1", Description = "Desc1", UnitOfMeasure = "Ea" },
                    new Part { PartId = 2, PartNumber = "P2", Description = "Desc2", UnitOfMeasure = "Ea" },
                    new Part { PartId = 3, PartNumber = "P3", Description = "Desc3", UnitOfMeasure = "Ea" }
                );

                context.PurchaseOrder.AddRange(
                    new PurchaseOrder { PurchaseOrderId = 1, PONumber = "PO-001", Status = "Draft", Currency = "USD" },
                    new PurchaseOrder { PurchaseOrderId = 2, PONumber = "PO-002", Status = "Sent", Currency = "USD" },
                    new PurchaseOrder { PurchaseOrderId = 3, PONumber = "PO-003", Status = "Received", Currency = "USD" },
                    new PurchaseOrder { PurchaseOrderId = 4, PONumber = "PO-004", Status = "Draft", Currency = "USD" }
                );

                context.Customer.AddRange(
                    new Customer { CustomerId = 1, CompanyName = "Six Flags" },
                    new Customer { CustomerId = 2, CompanyName = "Cedar Point" }
                );

                context.SaveChanges();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var mockLogger = new Mock<ILogger<IndexModel>>();
                var pageModel = new IndexModel(mockLogger.Object, context);

                // Act
                pageModel.OnGet();

                // Assert
                Assert.Equal(2, pageModel.OrderCount);
                Assert.Equal(3, pageModel.PartCount);
                Assert.Equal(4, pageModel.PurchaseOrderCount);
                Assert.Equal(2, pageModel.CustomerCount);
            }
        }
    }
}
