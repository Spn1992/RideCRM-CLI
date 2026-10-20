using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Tests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> HandlerFunc { get; set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (HandlerFunc != null)
            {
                return HandlerFunc(request, cancellationToken);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    public class UpsShippingServiceTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private async Task SeedUpsConfigurationsAsync(ApplicationDbContext context, string clientId = "TEST_CLIENT_ID", string clientSecret = "TEST_SECRET", int timeout = 5)
        {
            context.SystemConfiguration.AddRange(
                new SystemConfiguration { ConfigKey = "UPS_ClientId", ConfigValue = clientId, DataType = "String" },
                new SystemConfiguration { ConfigKey = "UPS_ClientSecret", ConfigValue = clientSecret, DataType = "String" },
                new SystemConfiguration { ConfigKey = "UPS_AccountNumber", ConfigValue = "A12345", DataType = "String" },
                new SystemConfiguration { ConfigKey = "UPS_BaseUrl", ConfigValue = "https://wwwcie.ups.com", DataType = "String" },
                new SystemConfiguration { ConfigKey = "UPS_ApiTimeoutSeconds", ConfigValue = timeout.ToString(), DataType = "Integer" }
            );
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetRateQuoteAsync_WithMockConfig_ReturnsMockRatesSuccessfully()
        {
            // Arrange
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            await SeedUpsConfigurationsAsync(context, clientId: "MOCK_CLIENT_ID");

            var service = new UpsShippingService(new HttpClient(), context);

            var request = new ShippingRateRequest
            {
                Carrier = "UPS",
                ServiceType = "03",
                ShipTo = new ShippingAddressDto { Name = "Park Center", AddressLine1 = "1 Park Rd", City = "Orlando", State = "FL", PostalCode = "32801", Country = "US" },
                Packages = new List<ShippingPackageDto>
                {
                    new ShippingPackageDto { PackageId = 1, PackageNumber = 1, WeightLbs = 10m, LengthIn = 12m, WidthIn = 10m, HeightIn = 8m }
                }
            };

            // Act
            var result = await service.GetRateQuoteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("UPS", result.Carrier);
            Assert.True(result.TotalEstimatedCost > 0m);
            Assert.NotNull(result.RatesByService);
            Assert.True(result.RatesByService.ContainsKey("03"));
        }

        [Fact]
        public async Task GetRateQuoteAsync_WithInvalidDimensions_ReturnsValidationError()
        {
            // Arrange
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            await SeedUpsConfigurationsAsync(context);

            var service = new UpsShippingService(new HttpClient(), context);

            var request = new ShippingRateRequest
            {
                Carrier = "UPS",
                ServiceType = "03",
                ShipTo = new ShippingAddressDto { Name = "Customer" },
                Packages = new List<ShippingPackageDto>
                {
                    new ShippingPackageDto { PackageId = 1, PackageNumber = 1, WeightLbs = 0m, LengthIn = 10m, WidthIn = 10m, HeightIn = 10m }
                }
            };

            // Act
            var result = await service.GetRateQuoteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("positive dimensions and weight", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateShipmentLabelAsync_WithMockEndpoint_ReturnsTrackingAndLabelUrl()
        {
            // Arrange
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            await SeedUpsConfigurationsAsync(context, clientId: "MOCK_CLIENT_ID");

            var service = new UpsShippingService(new HttpClient(), context);

            var dispatchReq = new ShipmentDispatchRequest
            {
                ShipmentId = 101,
                ShipmentNumber = "SHP-2026-001",
                Carrier = "UPS",
                ServiceType = "03",
                ShipTo = new ShippingAddressDto { Name = "Roller Coaster World", AddressLine1 = "500 Coaster Way", City = "Tampa", State = "FL", PostalCode = "33601" },
                Packages = new List<ShippingPackageDto>
                {
                    new ShippingPackageDto { PackageId = 1, PackageNumber = 1, WeightLbs = 15.5m, LengthIn = 14m, WidthIn = 12m, HeightIn = 10m }
                }
            };

            // Act
            var result = await service.CreateShipmentLabelAsync(dispatchReq);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.TrackingNumber);
            Assert.StartsWith("1Z99999903", result.TrackingNumber);
            Assert.NotNull(result.ShippingLabelUrl);
            Assert.Single(result.PackageTrackingNumbers);
        }

        [Fact]
        public async Task CreateShipmentLabelAsync_WhenUpsApiTimesOut_ReturnsTimeoutErrorMessage()
        {
            // Arrange
            var options = GetDbContextOptions();
            using var context = new ApplicationDbContext(options);
            await SeedUpsConfigurationsAsync(context, clientId: "REAL_CLIENT_ID", timeout: 1);

            var handler = new MockHttpMessageHandler
            {
                HandlerFunc = async (req, ct) =>
                {
                    await Task.Delay(2000, ct); // Simulate slow network exceeding 1 sec timeout
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
            };

            var client = new HttpClient(handler);
            var service = new UpsShippingService(client, context);

            var dispatchReq = new ShipmentDispatchRequest
            {
                ShipmentId = 102,
                ShipmentNumber = "SHP-2026-002",
                Packages = new List<ShippingPackageDto>
                {
                    new ShippingPackageDto { PackageId = 1, PackageNumber = 1, WeightLbs = 5m, LengthIn = 10m, WidthIn = 10m, HeightIn = 10m }
                }
            };

            // Act
            var result = await service.CreateShipmentLabelAsync(dispatchReq);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }
}
