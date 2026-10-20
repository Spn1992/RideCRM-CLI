using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;
using AmusementRideCRM.Middleware;

namespace AmusementRideCRM.Tests;

public class AuditLogAndExceptionMiddlewareTests
{
    private class MockAuditUserProvider : IAuditUserProvider
    {
        public int? UserId { get; set; } = 42;
        public string UserName { get; set; } = "auditor_user";
        public string? IpAddress { get; set; } = "192.168.1.100";
        public Guid CorrelationId { get; set; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

        public int? GetUserId() => UserId;
        public string GetUserName() => UserName;
        public string? GetIpAddress() => IpAddress;
        public Guid GetCorrelationId() => CorrelationId;
    }

    private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Part_Update_WritesAuditLogsWithOldAndNewValues()
    {
        // Arrange
        var options = GetDbContextOptions();
        var userProvider = new MockAuditUserProvider();

        using (var context = new ApplicationDbContext(options, userProvider))
        {
            var part = new Part
            {
                PartId = 1,
                PartNumber = "PART-100",
                Description = "High Voltage Relay",
                UnitOfMeasure = "Ea",
                QuantityOnHand = 10m,
                ReservedQuantity = 2m,
                SellingPriceUsd = 150.00m
            };
            context.Part.Add(part);
            await context.SaveChangesAsync();

            // Act
            part.QuantityOnHand = 25m;
            part.SellingPriceUsd = 175.50m;
            await context.SaveChangesAsync();

            // Assert
            var auditLogs = await context.AuditLog
                .Where(a => a.EntityName == "Part" && a.EntityId == "1" && a.ActionType == "Modified")
                .ToListAsync();

            Assert.NotEmpty(auditLogs);

            var qtyAudit = auditLogs.FirstOrDefault(a => a.PropertyName == "QuantityOnHand");
            Assert.NotNull(qtyAudit);
            Assert.Equal("10", qtyAudit.OldValue);
            Assert.Equal("25", qtyAudit.NewValue);
            Assert.Equal("Modified", qtyAudit.ActionType);
            Assert.Equal(42, qtyAudit.UserId);
            Assert.Equal("auditor_user", qtyAudit.UserName);
            Assert.Equal("192.168.1.100", qtyAudit.IpAddress);

            var priceAudit = auditLogs.FirstOrDefault(a => a.PropertyName == "SellingPriceUsd");
            Assert.NotNull(priceAudit);
            Assert.Equal("150.00", priceAudit.OldValue);
            Assert.Equal("175.50", priceAudit.NewValue);
        }
    }

    [Fact]
    public async Task QBSyncQueue_StatusChange_CapturesAuditLog()
    {
        // Arrange
        var options = GetDbContextOptions();
        var userProvider = new MockAuditUserProvider();

        using (var context = new ApplicationDbContext(options, userProvider))
        {
            var syncItem = new QBSyncQueue
            {
                SyncId = 5,
                EntityType = "VendorInvoice",
                EntityId = 1001,
                SyncAction = "Create",
                Status = "Pending"
            };
            context.QBSyncQueue.Add(syncItem);
            await context.SaveChangesAsync();

            // Act
            syncItem.Status = "In Progress";
            syncItem.ErrorMessage = "Retrying SOAP request";
            await context.SaveChangesAsync();

            // Assert
            var statusAudit = await context.AuditLog
                .FirstOrDefaultAsync(a => a.EntityName == "QBSyncQueue" && a.EntityId == "5" && a.PropertyName == "Status" && a.ActionType == "Modified");

            Assert.NotNull(statusAudit);
            Assert.Equal("Pending", statusAudit.OldValue);
            Assert.Equal("In Progress", statusAudit.NewValue);
            Assert.Equal("Modified", statusAudit.ActionType);
            Assert.Equal("VendorInvoice", statusAudit.RelatedEntity);
            Assert.Equal("1001", statusAudit.RelatedEntityId);
        }
    }

    [Fact]
    public async Task ReceivingWorkflow_AuditEntriesLinkStockUpdatesToPOLine()
    {
        // Arrange
        var options = GetDbContextOptions();
        var userProvider = new MockAuditUserProvider();

        using (var context = new ApplicationDbContext(options, userProvider))
        {
            var part = new Part
            {
                PartId = 10,
                PartNumber = "BEARING-50",
                Description = "Roller Bearing",
                UnitOfMeasure = "Ea",
                QuantityOnHand = 5m,
                ReservedQuantity = 0m
            };
            var poLine = new PurchaseOrderLine
            {
                POLineId = 20,
                PurchaseOrderId = 200,
                PartId = 10,
                Quantity = 10m,
                QuantityReceived = 0m,
                UnitCostEur = 50.00m
            };
            var receivingLine = new ReceivingLine
            {
                ReceivingLineId = 30,
                ReceivingId = 300,
                POLineId = 20,
                PartId = 10,
                QuantityExpected = 10m,
                QuantityReceived = 5m
            };

            context.Part.Add(part);
            context.PurchaseOrderLine.Add(poLine);
            context.ReceivingLine.Add(receivingLine);
            await context.SaveChangesAsync();

            // Act - run receiving workflow
            await ReceivingWorkflow.ProcessReceivingLineAsync(context, receivingLine, null, "auditor_user");

            // Assert
            var partAudits = await context.AuditLog
                .Where(a => a.EntityName == "Part" && a.EntityId == "10" && a.ActionType == "Modified")
                .ToListAsync();

            Assert.NotEmpty(partAudits);
            var qtyAudit = partAudits.FirstOrDefault(a => a.PropertyName == "QuantityOnHand");
            Assert.NotNull(qtyAudit);
            Assert.Equal("5", qtyAudit.OldValue);
            Assert.Equal("10", qtyAudit.NewValue);
            Assert.True(qtyAudit.RelatedEntity == "PurchaseOrderLine" || qtyAudit.RelatedEntity == "ReceivingLine");
            Assert.Equal("20", qtyAudit.RelatedEntityId);

            var poLineAudits = await context.AuditLog
                .Where(a => a.EntityName == "PurchaseOrderLine" && a.EntityId == "20" && a.ActionType == "Modified")
                .ToListAsync();

            Assert.NotEmpty(poLineAudits);
            var poQtyAudit = poLineAudits.FirstOrDefault(a => a.PropertyName == "QuantityReceived");
            Assert.NotNull(poQtyAudit);
            Assert.Equal("0", poQtyAudit.OldValue);
            Assert.Equal("5", poQtyAudit.NewValue);
        }
    }

    [Fact]
    public async Task SensitiveFields_LikeRowVersion_AreExcludedFromAuditLog()
    {
        // Arrange
        var options = GetDbContextOptions();
        using (var context = new ApplicationDbContext(options))
        {
            var part = new Part
            {
                PartId = 100,
                PartNumber = "TEST-ROWVER",
                Description = "Test Part",
                UnitOfMeasure = "Ea",
                RowVersion = new byte[] { 1, 2, 3, 4 }
            };
            context.Part.Add(part);
            await context.SaveChangesAsync();

            // Act
            part.RowVersion = new byte[] { 5, 6, 7, 8 };
            part.Description = "Test Part Updated";
            await context.SaveChangesAsync();

            // Assert
            var rowVersionAudit = await context.AuditLog
                .FirstOrDefaultAsync(a => a.EntityName == "Part" && a.EntityId == "100" && a.PropertyName == "RowVersion");

            Assert.Null(rowVersionAudit);

            var descriptionAudit = await context.AuditLog
                .FirstOrDefaultAsync(a => a.EntityName == "Part" && a.EntityId == "100" && a.PropertyName == "Description" && a.ActionType == "Modified");

            Assert.NotNull(descriptionAudit);
            Assert.Equal("Test Part Updated", descriptionAudit.NewValue);
        }
    }

    [Fact]
    public async Task SystemDefaultUser_UsedWhenNoHttpContextPresent()
    {
        // Arrange
        var options = GetDbContextOptions();
        using (var context = new ApplicationDbContext(options))
        {
            var part = new Part
            {
                PartId = 200,
                PartNumber = "SYS-USER-TEST",
                Description = "System User Test",
                UnitOfMeasure = "Ea"
            };
            context.Part.Add(part);
            await context.SaveChangesAsync();

            part.Description = "System User Test Updated";
            await context.SaveChangesAsync();

            // Assert
            var audit = await context.AuditLog
                .FirstOrDefaultAsync(a => a.EntityName == "Part" && a.EntityId == "200" && a.PropertyName == "Description" && a.ActionType == "Modified");

            Assert.NotNull(audit);
            Assert.Equal("System", audit.UserName);
            Assert.Equal(0, audit.UserId);
            Assert.Equal("127.0.0.1", audit.IpAddress);
        }
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_RethrowsForPageRequestsAndLogsToDatabase()
    {
        // Arrange
        var options = GetDbContextOptions();
        var userProvider = new MockAuditUserProvider();

        using (var context = new ApplicationDbContext(options, userProvider))
        {
            RequestDelegate next = (innerHttpContext) =>
            {
                throw new InvalidOperationException("Simulated unhandled CRM exception during page handler.");
            };

            var middleware = new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/CustomerOrder/Details";
            httpContext.Request.Method = "GET";
            httpContext.Request.QueryString = new QueryString("?id=123");

            // Act & Assert exception is rethrown for ASP.NET Core exception page handling
            await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext, context, userProvider));

            // Assert error record was persisted in dbo.ErrorLogging
            var errorRecord = await context.ErrorLogging.FirstOrDefaultAsync();

            Assert.NotNull(errorRecord);
            Assert.Equal("Error", errorRecord.Severity);
            Assert.Equal("Simulated unhandled CRM exception during page handler.", errorRecord.ErrorMessage);
            Assert.Equal("/CustomerOrder/Details", errorRecord.RequestPath);
            Assert.Equal("GET", errorRecord.RequestMethod);
            Assert.Equal("?id=123", errorRecord.QueryString);
            Assert.Equal(500, errorRecord.StatusCode);
            Assert.Equal("auditor_user", errorRecord.UserName);
            Assert.Equal(userProvider.CorrelationId, errorRecord.CorrelationId);
            Assert.NotNull(errorRecord.StackTrace);
        }
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_ReturnsJsonForApiRequestsAndLogsToDatabase()
    {
        // Arrange
        var options = GetDbContextOptions();
        var userProvider = new MockAuditUserProvider();

        using (var context = new ApplicationDbContext(options, userProvider))
        {
            RequestDelegate next = (innerHttpContext) =>
            {
                throw new InvalidOperationException("Simulated unhandled API exception.");
            };

            var middleware = new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/api/receiving/process";
            httpContext.Request.Method = "POST";
            httpContext.Response.Body = new MemoryStream();

            // Act - should not rethrow for API request, but write JSON 500 response
            await middleware.InvokeAsync(httpContext, context, userProvider);

            // Assert
            Assert.Equal(500, httpContext.Response.StatusCode);
            Assert.StartsWith("application/json", httpContext.Response.ContentType);

            var errorRecord = await context.ErrorLogging.FirstOrDefaultAsync();
            Assert.NotNull(errorRecord);
            Assert.Equal("Simulated unhandled API exception.", errorRecord.ErrorMessage);
            Assert.Equal("/api/receiving/process", errorRecord.RequestPath);
        }
    }
}
