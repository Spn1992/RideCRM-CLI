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
            }
        }
    }
}
