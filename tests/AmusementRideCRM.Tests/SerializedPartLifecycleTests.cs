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
    public class SerializedPartLifecycleTests
    {
        private DbContextOptions<ApplicationDbContext> GetDbContextOptions()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public void PartModel_SerializationFlagAndWarrantyDuration_SetCorrectly()
        {
            var part = new Part
            {
                PartId = 1,
                PartNumber = "SAFETY-HARNESS-001",
                Description = "Critical Safety Restraint Harness",
                UnitOfMeasure = "Ea",
                IsSerialized = true,
                WarrantyDurationMonths = 24
            };

            Assert.True(part.IsSerialized);
            Assert.Equal(24, part.WarrantyDurationMonths);
        }

        [Fact]
        public async Task ReceivingWorkflow_CreatesSerializedUnitsAndZplPrintJobs()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 101,
                    PartNumber = "BRAKE-CALIPER-X1",
                    Description = "High Velocity Coaster Brake Caliper",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 0m,
                    ReservedQuantity = 0m,
                    IsSerialized = true,
                    WarrantyDurationMonths = 12
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();
            }

            // Receive 2 serialized units
            using (var context = new ApplicationDbContext(options))
            {
                var recLine = new ReceivingLine
                {
                    ReceivingLineId = 10,
                    ReceivingId = 100,
                    PartId = 101,
                    QuantityExpected = 2m,
                    QuantityReceived = 2m
                };
                context.ReceivingLine.Add(recLine);
                await context.SaveChangesAsync();

                var serials = new List<string> { "SN-CAL-0001", "SN-CAL-0002" };
                await ReceivingWorkflow.ProcessReceivingLineAsync(context, recLine, serials, "Inspector Gadget");
            }

            // Assert units and print jobs
            using (var context = new ApplicationDbContext(options))
            {
                var units = await context.SerializedPartUnit
                    .Where(u => u.PartId == 101)
                    .OrderBy(u => u.SerialNumber)
                    .ToListAsync();

                Assert.Equal(2, units.Count);
                Assert.Equal("SN-CAL-0001", units[0].SerialNumber);
                Assert.Equal("SN-CAL-0002", units[1].SerialNumber);
                Assert.Equal("InStock", units[0].Status);
                Assert.Equal("InStock", units[1].Status);

                var printJobs = await context.ZplPrintJob
                    .Where(j => j.PartId == 101)
                    .ToListAsync();

                Assert.Equal(2, printJobs.Count);
                Assert.All(printJobs, j => Assert.Equal("Pending", j.Status));
                Assert.Contains("SN-CAL-0001", printJobs[0].ZplCommand);
            }
        }

        [Fact]
        public async Task DuplicateSerialNumber_ThrowsException()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartId = 201,
                    PartNumber = "SENSOR-OPT-01",
                    Description = "Optical Position Sensor",
                    UnitOfMeasure = "Ea",
                    IsSerialized = true
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();

                var existingUnit = new SerializedPartUnit
                {
                    PartId = 201,
                    SerialNumber = "SN-SENS-888",
                    Status = "InStock"
                };
                context.SerializedPartUnit.Add(existingUnit);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var duplicateSerials = new List<string> { "SN-SENS-888" };
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await SerializedPartService.ValidateSerialUniquenessAsync(context, 201, duplicateSerials);
                });
            }
        }

        [Fact]
        public async Task ShippingWorkflow_AttachesSerialToCustomerRideAndSetsWarranty()
        {
            var options = GetDbContextOptions();
            int assignedRideId;
            int assignedUnitId;
            int assignedShipmentLineId;

            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartNumber = "VALVE-HYD-50",
                    Description = "Hydraulic Safety Lock Valve",
                    UnitOfMeasure = "Ea",
                    QuantityOnHand = 5m,
                    IsSerialized = true,
                    WarrantyDurationMonths = 18
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();

                var ride = new CustomerRide
                {
                    CustomerId = 1,
                    RideManufacturer = "Vekoma",
                    RideModel = "Custom Coaster",
                    SerialNumber = "VEK-2024-99",
                    IsActive = true
                };
                context.CustomerRide.Add(ride);
                await context.SaveChangesAsync();
                assignedRideId = ride.RideId;

                var unit = new SerializedPartUnit
                {
                    PartId = part.PartId,
                    SerialNumber = "SN-VALVE-777",
                    Status = "InStock"
                };
                context.SerializedPartUnit.Add(unit);
                await context.SaveChangesAsync();
                assignedUnitId = unit.SerializedPartUnitId;

                var shipmentLine = new ShipmentLine
                {
                    ShipmentId = 888,
                    PartId = part.PartId,
                    QuantityShipped = 1m
                };
                context.ShipmentLine.Add(shipmentLine);
                await context.SaveChangesAsync();
                assignedShipmentLineId = shipmentLine.ShipmentLineId;
            }

            // Ship unit to assignedRideId
            using (var context = new ApplicationDbContext(options))
            {
                var shippedUnits = await SerializedPartService.ShipSerializedUnitsAsync(
                    context,
                    shipmentLineId: assignedShipmentLineId,
                    shipmentId: 888,
                    rideId: assignedRideId,
                    unitIds: new List<int> { assignedUnitId },
                    modifiedBy: "Shipping Lead");

                Assert.Single(shippedUnits);
            }

            // Verify lifecycle state and warranty dates
            using (var context = new ApplicationDbContext(options))
            {
                var unit = await context.SerializedPartUnit
                    .Include(u => u.CustomerRide)
                    .FirstAsync(u => u.SerializedPartUnitId == assignedUnitId);

                Assert.Equal("Installed", unit.Status);
                Assert.Equal(assignedRideId, unit.RideId);
                Assert.NotNull(unit.CustomerRide);
                Assert.Equal("Vekoma", unit.CustomerRide.RideManufacturer);
                Assert.NotNull(unit.WarrantyStartDate);
                Assert.NotNull(unit.WarrantyEndDate);

                var expectedEndDate = unit.WarrantyStartDate.Value.AddMonths(18);
                Assert.Equal(expectedEndDate.Date, unit.WarrantyEndDate.Value.Date);
            }
        }

        [Fact]
        public async Task SafetyRecallSearch_FindsAffectedRidesByPartAndSerial()
        {
            var options = GetDbContextOptions();
            using (var context = new ApplicationDbContext(options))
            {
                var part = new Part
                {
                    PartNumber = "BOLT-CRIT-M20",
                    Description = "Titanium Critical Track Bolt",
                    UnitOfMeasure = "Ea",
                    IsSerialized = true,
                    WarrantyDurationMonths = 12
                };
                context.Part.Add(part);
                await context.SaveChangesAsync();

                var ride1 = new CustomerRide
                {
                    CustomerId = 1,
                    RideManufacturer = "B&M",
                    RideModel = "Hyper Coaster",
                    SerialNumber = "BM-HC-001"
                };

                var ride2 = new CustomerRide
                {
                    CustomerId = 2,
                    RideManufacturer = "Intamin",
                    RideModel = "Giga Coaster",
                    SerialNumber = "INT-GC-002"
                };

                context.CustomerRide.AddRange(ride1, ride2);
                await context.SaveChangesAsync();

                var now = DateTime.UtcNow;

                var unit1 = new SerializedPartUnit
                {
                    PartId = part.PartId,
                    SerialNumber = "BATCH-2026A-01",
                    Status = "Installed",
                    RideId = ride1.RideId,
                    WarrantyStartDate = now.AddMonths(-2),
                    WarrantyEndDate = now.AddMonths(10)
                };

                var unit2 = new SerializedPartUnit
                {
                    PartId = part.PartId,
                    SerialNumber = "BATCH-2026A-02",
                    Status = "Installed",
                    RideId = ride2.RideId,
                    WarrantyStartDate = now.AddMonths(-2),
                    WarrantyEndDate = now.AddMonths(10)
                };

                context.SerializedPartUnit.AddRange(unit1, unit2);
                await context.SaveChangesAsync();
            }

            // Query safety recall for batch "BATCH-2026A"
            using (var context = new ApplicationDbContext(options))
            {
                var recallResults = await SerializedPartService.SearchRecallAndWarrantyAsync(
                    context,
                    partNumber: "BOLT-CRIT-M20",
                    serialSearch: "2026A",
                    warrantyStartFrom: null,
                    warrantyStartTo: null,
                    activeWarrantyOnly: true);

                Assert.Equal(2, recallResults.Count);
                Assert.Contains(recallResults, u => u.CustomerRide?.RideManufacturer == "B&M");
                Assert.Contains(recallResults, u => u.CustomerRide?.RideManufacturer == "Intamin");
            }
        }
    }
}
